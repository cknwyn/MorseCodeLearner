using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace MorseApp
{
    public enum ActiveMode
    {
        Learning,
        SentenceChallenge,
        Sandbox
    }
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Beep(uint dwFreq, uint dwDuration);
        [DllImport("MorseCore.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern ushort Morse_EncodeChar(uint asciiChar);
        [DllImport("MorseCore.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern byte Morse_DecodeChar(byte count, byte pattern);
        [DllImport("MorseCore.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint Morse_QuantizePress(ulong durationUs, ulong unitUs);
        [DllImport("MorseCore.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint Morse_EvaluateGap(ulong gapUs, ulong unitUs);
        // State Machine
        private ActiveMode _currentMode = ActiveMode.Learning;
        private int _wpm = 15;
        private ulong _unitUs => (ulong)(1200000 / _wpm);
        // Hardware Switch Timers
        private readonly Stopwatch _pressTimer = new();
        private readonly Stopwatch _idleTimer = new();
        private readonly DispatcherTimer _gapTimer = new();
        private bool _isKeyPressed = false;
        private byte _elementCount = 0;
        private byte _patternMask = 0;
        private bool _spaceCommittedForPause = false;
        // Visual Colors
        private static readonly SolidColorBrush ActiveYellow = new(Color.FromRgb(0xFF, 0xD6, 0x00));
        private static readonly SolidColorBrush IdleDark = new(Color.FromRgb(0x2A, 0x2A, 0x2A));
        private static readonly SolidColorBrush TextDim = new(Color.FromRgb(0x70, 0x70, 0x70));
        private static readonly SolidColorBrush CorrectGreen = new(Color.FromRgb(0x00, 0xE6, 0x76));
        private static readonly SolidColorBrush PendingGray = new(Color.FromRgb(0xA0, 0xA0, 0xA0));
        private static readonly SolidColorBrush AccentCyan = new(Color.FromRgb(0x00, 0xE5, 0xFF));
        // Mode 1: Learning State
        private readonly char[] _alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
        private int _learnIndex = 0;
        private int _learnAttempts = 0;
        private int _learnCorrect = 0;
        private int _learnStreak = 0;
        // Mode 2: Sentence Challenge State
        private string _targetSentence = "HELLO WORLD";
        private int _sentenceCharIndex = 0;
        public MainWindow()
        {
            InitializeComponent();
            _gapTimer.Interval = TimeSpan.FromMilliseconds(20);
            _gapTimer.Tick += OnGapTick;
            _gapTimer.Start();
            LoadLearningChar();
            BuildSentenceUI();
        }
        #region Navigation & Settings Toggle
        private void OnSelectLearningMode(object sender, RoutedEventArgs e)
        {
            _currentMode = ActiveMode.Learning;
            SetTabColors(TabLearningBtn);
            PanelLearning.Visibility = Visibility.Visible;
            PanelSentence.Visibility = Visibility.Collapsed;
            PanelSandbox.Visibility = Visibility.Collapsed;
            ModeMetric.Text = "LEARNING";
        }
        private void OnSelectSentenceMode(object sender, RoutedEventArgs e)
        {
            _currentMode = ActiveMode.SentenceChallenge;
            SetTabColors(TabSentenceBtn);
            PanelLearning.Visibility = Visibility.Collapsed;
            PanelSentence.Visibility = Visibility.Visible;
            PanelSandbox.Visibility = Visibility.Collapsed;
            ModeMetric.Text = "SENTENCE";
        }
        private void OnSelectSandboxMode(object sender, RoutedEventArgs e)
        {
            _currentMode = ActiveMode.Sandbox;
            SetTabColors(TabSandboxBtn);
            PanelLearning.Visibility = Visibility.Collapsed;
            PanelSentence.Visibility = Visibility.Collapsed;
            PanelSandbox.Visibility = Visibility.Visible;
            ModeMetric.Text = "SANDBOX";
        }
        private void SetTabColors(Button activeBtn)
        {
            TabLearningBtn.Background = Brushes.Transparent;
            TabLearningBtn.Foreground = PendingGray;
            TabSentenceBtn.Background = Brushes.Transparent;
            TabSentenceBtn.Foreground = PendingGray;
            TabSandboxBtn.Background = Brushes.Transparent;
            TabSandboxBtn.Foreground = PendingGray;
            activeBtn.Background = ActiveYellow;
            activeBtn.Foreground = Brushes.Black;
            Keyboard.ClearFocus();
            this.Focus();
        }
        private void OnToggleTelemetry(object sender, RoutedEventArgs e)
        {
            if (TelemetryBar.Visibility == Visibility.Visible)
            {
                TelemetryBar.Visibility = Visibility.Collapsed;
                ToggleStatsBtn.Content = "⚙ Statistics: OFF";
                ToggleStatsBtn.Foreground = TextDim;
            }
            else
            {
                TelemetryBar.Visibility = Visibility.Visible;
                ToggleStatsBtn.Content = "⚙ Statistics: ON";
                ToggleStatsBtn.Foreground = ActiveYellow;
            }
        }
        private void OnDecreaseWpm(object sender, RoutedEventArgs e)
        {
            if (_wpm > 5) { _wpm--; WpmDisplay.Text = $"{_wpm} WPM"; }
        }
        private void OnIncreaseWpm(object sender, RoutedEventArgs e)
        {
            if (_wpm < 30) { _wpm++; WpmDisplay.Text = $"{_wpm} WPM"; }
        }
        #endregion
        #region Hardware Key Event Processing
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (CustomPhraseInput.IsFocused)
                {
                    base.OnPreviewKeyDown(e);
                    return;
                }
                e.Handled = true;
                if (!e.IsRepeat && !_isKeyPressed)
                {
                    _isKeyPressed = true;
                    _idleTimer.Stop();
                    _pressTimer.Restart();
                    _spaceCommittedForPause = false; // Reset space latch for new keypress
                    SignalBulb.Fill = ActiveYellow;
                    SignalGlow.Radius = 24;
                    SignalStatusText.Text = "KEY DOWN";
                    SignalStatusText.Foreground = Brushes.Black;
                    Task.Run(() => Beep(700, 1000));
                }
                return;
            }
            base.OnPreviewKeyDown(e);
        }
        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (CustomPhraseInput.IsFocused)
                {
                    base.OnPreviewKeyUp(e);
                    return;
                }
                e.Handled = true;
                if (_isKeyPressed)
                {
                    _isKeyPressed = false;
                    _pressTimer.Stop();
                    SignalBulb.Fill = IdleDark;
                    SignalGlow.Radius = 0;
                    SignalStatusText.Text = "KEY UP";
                    SignalStatusText.Foreground = TextDim;
                    ulong durationUs = (ulong)(_pressTimer.Elapsed.TotalMilliseconds * 1000);
                    DurationMetric.Text = $"{_pressTimer.ElapsedMilliseconds} ms";
                    // Native MASM quantizer call
                    uint token = Morse_QuantizePress(durationUs, _unitUs);
                    if (token == 1) // Dit (0)
                    {
                        TokenMetric.Text = "DIT (•)";
                        _patternMask = (byte)((_patternMask << 1) | 0);
                        _elementCount++;
                        CurrentTokenBuffer.Text += "• ";
                    }
                    else if (token == 2) // Dah (1)
                    {
                        TokenMetric.Text = "DAH (─)";
                        _patternMask = (byte)((_patternMask << 1) | 1);
                        _elementCount++;
                        CurrentTokenBuffer.Text += "─ ";
                    }
                    _idleTimer.Restart();
                }
                return;
            }
            base.OnPreviewKeyUp(e);
        }
        private void OnGapTick(object? sender, EventArgs e)
        {
            if (_isKeyPressed) return;
            long idleMs = _idleTimer.ElapsedMilliseconds;
            // Phase 1: Inter-Character Gap Evaluation (Commit Letter when idle > 300ms)
            if (_elementCount > 0 && idleMs >= 300)
            {
                byte decodedAscii = Morse_DecodeChar(_elementCount, _patternMask);
                char decoded = (char)decodedAscii;
                if (decoded != '\0')
                {
                    DispatchDecodedCharacter(decoded);
                }
                _elementCount = 0;
                _patternMask = 0;
                CurrentTokenBuffer.Text = string.Empty;
                // Note: Keep _idleTimer running to evaluate word space gap!
            }
            // Phase 2: Word Space Gap Evaluation (Wait 1 second = 1000ms)
            if (_elementCount == 0 && idleMs >= 1000 && !_spaceCommittedForPause)
            {
                _spaceCommittedForPause = true; // Prevents repeating spaces
                DispatchSpace();
            }
        }
        private void DispatchDecodedCharacter(char c)
        {
            switch (_currentMode)
            {
                case ActiveMode.Learning:
                    EvaluateLearningInput(c);
                    break;
                case ActiveMode.SentenceChallenge:
                    EvaluateSentenceInput(c);
                    break;
                case ActiveMode.Sandbox:
                    SandboxStreamBox.Text += c;
                    SandboxStreamBox.ScrollToEnd();
                    break;
            }
        }
        private void DispatchSpace()
        {
            switch (_currentMode)
            {
                case ActiveMode.SentenceChallenge:
                    // If target is a space, advance!
                    if (_sentenceCharIndex < _targetSentence.Length && _targetSentence[_sentenceCharIndex] == ' ')
                    {
                        _sentenceCharIndex++;
                        BuildSentenceUI();
                    }
                    break;
                case ActiveMode.Sandbox:
                    // Add space between words
                    if (!string.IsNullOrEmpty(SandboxStreamBox.Text) && !SandboxStreamBox.Text.EndsWith(" "))
                    {
                        SandboxStreamBox.Text += " ";
                        SandboxStreamBox.ScrollToEnd();
                    }
                    break;
                case ActiveMode.Learning:
                    // Single character drills do not require word spaces
                    break;
            }
        }
        #endregion
        #region Mode 1: Learning Logic
        private void LoadLearningChar()
        {
            char target = _alphabet[_learnIndex];
            LearnTargetChar.Text = target.ToString();
            // Encode using native MASM
            ushort packed = Morse_EncodeChar((uint)target);
            byte count = (byte)(packed & 0xFF);
            byte pattern = (byte)((packed >> 8) & 0xFF);
            string morseStr = "";
            for (int i = count - 1; i >= 0; i--)
            {
                morseStr += (((pattern >> i) & 1) == 1) ? "─ " : "• ";
            }
            LearnTargetMorse.Text = morseStr.Trim();
            LearnTargetPhonetic.Text = $"Phonetic: {GetPhoneticHint(target)}";
            LearnFeedbackText.Text = $"Key in [ {morseStr.Trim()} ] using the Spacebar";
            LearnFeedbackText.Foreground = AccentCyan;
        }
        private void EvaluateLearningInput(char entered)
        {
            _learnAttempts++;
            char expected = _alphabet[_learnIndex];
            if (char.ToUpper(entered) == expected)
            {
                _learnCorrect++;
                _learnStreak++;
                LearnFeedbackText.Text = $"✔ Excellent! Correct letter '{expected}'";
                LearnFeedbackText.Foreground = CorrectGreen;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    _learnIndex = (_learnIndex + 1) % _alphabet.Length;
                    LoadLearningChar();
                };
                timer.Start();
            }
            else
            {
                _learnStreak = 0;
                LearnFeedbackText.Text = $"✖ You entered '{entered}'. Target was '{expected}' ({LearnTargetMorse.Text})";
                LearnFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x3D, 0x00));
            }
            int pct = (int)((double)_learnCorrect / _learnAttempts * 100);
            LearnScoreText.Text = $"Score: {_learnCorrect} / {_learnAttempts} ({pct}%)  │  Streak: {_learnStreak} 🔥";
        }
        private void OnNextLearnChar(object sender, RoutedEventArgs e)
        {
            _learnIndex = (_learnIndex + 1) % _alphabet.Length;
            LoadLearningChar();
        }
        private string GetPhoneticHint(char c) => c switch
        {
            'A' => "di-dah",
            'B' => "dah-di-di-dit",
            'C' => "dah-di-dah-dit",
            'D' => "dah-di-dit",
            'E' => "dit",
            'F' => "di-di-dah-dit",
            'G' => "dah-dah-dit",
            'H' => "di-di-di-dit",
            'I' => "di-dit",
            'J' => "di-dah-dah-dah",
            'K' => "dah-di-dah",
            'L' => "di-dah-di-dit",
            'M' => "dah-dah",
            'N' => "dah-dit",
            'O' => "dah-dah-dah",
            'P' => "di-dah-dah-dit",
            'Q' => "dah-dah-di-dah",
            'R' => "di-dah-dit",
            'S' => "di-di-dit",
            'T' => "dah",
            'U' => "di-di-dah",
            'V' => "di-di-di-dah",
            'W' => "di-dah-dah",
            'X' => "dah-di-di-dah",
            'Y' => "dah-di-dah-dah",
            'Z' => "dah-dah-di-dit",
            '0' => "dah-dah-dah-dah-dah",
            '1' => "di-dah-dah-dah-dah",
            '2' => "di-di-dah-dah-dah",
            '3' => "di-di-di-dah-dah",
            '4' => "di-di-di-di-dah",
            '5' => "di-di-di-di-dit",
            '6' => "dah-di-di-di-dit",
            '7' => "dah-dah-di-di-dit",
            '8' => "dah-dah-dah-di-dit",
            '9' => "dah-dah-dah-dah-dit",
            _ => "telegraph tone"
        };
        #endregion
        #region Mode 2: Sentence Challenge Logic
        private void BuildSentenceUI()
        {
            SentenceLettersWrap.Children.Clear();
            for (int i = 0; i < _targetSentence.Length; i++)
            {
                char ch = _targetSentence[i];
                var border = new Border
                {
                    Background = (i == _sentenceCharIndex) ? ActiveYellow : (i < _sentenceCharIndex ? CorrectGreen : IdleDark),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(3)
                };
                var tb = new TextBlock
                {
                    Text = ch == ' ' ? "␣" : ch.ToString(),
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = (i == _sentenceCharIndex || i < _sentenceCharIndex) ? Brushes.Black : PendingGray
                };
                border.Child = tb;
                SentenceLettersWrap.Children.Add(border);
            }
            UpdateSentenceTargetHint();
        }
        private void UpdateSentenceTargetHint()
        {
            if (_sentenceCharIndex < _targetSentence.Length)
            {
                char active = _targetSentence[_sentenceCharIndex];
                if (active == ' ')
                {
                    SentenceActiveChar.Text = "[SPACE]";
                    SentenceActiveMorse.Text = "⏳ Pause for 1 second";
                }
                else
                {
                    SentenceActiveChar.Text = active.ToString();
                    ushort packed = Morse_EncodeChar((uint)active);
                    byte count = (byte)(packed & 0xFF);
                    byte pattern = (byte)((packed >> 8) & 0xFF);
                    string m = "";
                    for (int i = count - 1; i >= 0; i--)
                    {
                        m += (((pattern >> i) & 1) == 1) ? "─ " : "• ";
                    }
                    SentenceActiveMorse.Text = m.Trim();
                }
                int pct = (int)((double)_sentenceCharIndex / _targetSentence.Length * 100);
                SentenceProgressText.Text = $"Progress: {_sentenceCharIndex} / {_targetSentence.Length} Characters ({pct}%)";
            }
            else
            {
                SentenceActiveChar.Text = "COMPLETE!";
                SentenceActiveMorse.Text = "🎉 Excellent Work!";
                SentenceProgressText.Text = "Challenge 100% Complete!";
            }
        }
        private void EvaluateSentenceInput(char entered)
        {
            if (_sentenceCharIndex >= _targetSentence.Length) return;
            char expected = _targetSentence[_sentenceCharIndex];
            if (char.ToUpper(entered) == char.ToUpper(expected))
            {
                _sentenceCharIndex++;
                BuildSentenceUI();
            }
        }
        private void OnPresetSelected(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
            {
                _targetSentence = b.Content.ToString()!.ToUpper();
                _sentenceCharIndex = 0;
                BuildSentenceUI();
            }
        }
        private void OnSetCustomSentence(object sender, RoutedEventArgs e)
        {
            string custom = CustomPhraseInput.Text.Trim().ToUpper();
            if (!string.IsNullOrEmpty(custom))
            {
                _targetSentence = custom;
                _sentenceCharIndex = 0;
                BuildSentenceUI();
            }
        }
        private void OnResetSentence(object sender, RoutedEventArgs e)
        {
            _sentenceCharIndex = 0;
            BuildSentenceUI();
        }
        #endregion
        #region Mode 3: Sandbox Actions
        private void OnClearSandbox(object sender, RoutedEventArgs e)
        {
            SandboxStreamBox.Text = string.Empty;
        }
        private void OnCopySandboxText(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SandboxStreamBox.Text))
            {
                Clipboard.SetText(SandboxStreamBox.Text);
                MessageBox.Show("Decoded Morse text copied to clipboard!", "Telegraph Sandbox", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void OnPlaySandboxMorse(object sender, RoutedEventArgs e)
        {
            string text = SandboxStreamBox.Text.ToUpper();
            if (string.IsNullOrWhiteSpace(text)) return;
            Task.Run(() =>
            {
                int unitMs = (int)(_unitUs / 1000);
                foreach (char c in text)
                {
                    if (c == ' ')
                    {
                        System.Threading.Thread.Sleep(unitMs * 7);
                        continue;
                    }
                    ushort packed = Morse_EncodeChar((uint)c);
                    byte count = (byte)(packed & 0xFF);
                    byte pattern = (byte)((packed >> 8) & 0xFF);
                    for (int i = count - 1; i >= 0; i--)
                    {
                        int toneDuration = (((pattern >> i) & 1) == 1) ? unitMs * 3 : unitMs;
                        Beep(700, (uint)toneDuration);
                        System.Threading.Thread.Sleep(unitMs);
                    }
                    System.Threading.Thread.Sleep(unitMs * 3);
                }
            });
        }
        #endregion
    }
}