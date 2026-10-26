using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MorseApp.Audio;
using MorseApp.Services;
namespace MorseApp
{
    public enum ActiveMode
    {
        Learning,
        SentenceChallenge,
        Sandbox,
        Listen
    }
    public partial class MainWindow : Window
    {
        [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);
        // Native PCM Audio Engine
        private readonly PcmAudioEngine _audioEngine = new(700);
        // State Machine
        private ActiveMode _currentMode = ActiveMode.Learning;
        private int _wpm = 15;
        private ulong _unitUs => MorseEngineService.UnitUs(_wpm);
        // Hardware Switch Timers
        private readonly Stopwatch _pressTimer = new();
        private readonly Stopwatch _idleTimer = new();
        private readonly DispatcherTimer _gapTimer = new();
        private bool _isKeyPressed = false;
        private byte _elementCount = 0;
        private byte _patternMask = 0;
        private bool _spaceCommittedForPause = false;
        // Visual Colors
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
            _activeTab = TabLearningBtn;
            timeBeginPeriod(1);
            KeyScope.LiveClassifier = ms => (int)MorseEngineService.QuantizePressUs((ulong)(ms * 1000), _unitUs);
            UpdateScopeUnit();
            LoadProgress();
            ConfigureScope();
            BuildHub();
            SetListenView(ListenView.Hub);
            BuildLearnChips();
            BuildReference();
            BuildTicker();
            SizeChanged += (_, _) => ApplyResponsive();
        }
        #region Key scope helpers
        private static string TokenName(int token) => token switch { 1 => "DIT", 2 => "DAH", _ => "TOO LONG" };

        private void UpdateScopeUnit()
        {
            double u = _unitUs / 1000.0;
            KeyScope.UnitMs = u;
            KeyReadout.Text = $"1 unit = {u:0} ms  \u00B7  dit \u2248 {u:0}  \u00B7  dah \u2248 {u * 3:0}";
        }
        #endregion
        #region Mode 4: Ear Training (study path + free practice)
        private sealed record Lesson(int Number, string Title, string Chars, string Blurb, string Tip);

        private const string LetterSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string NumberSet = "0123456789";
        private const int MasteryTarget = 3;
        private enum ListenView { Hub, Guide, Drill }

        private static readonly Lesson[] Lessons =
        {
            new(1, "Foundation: E & T", "ET", "The two shortest signals: one dit, one dah.",
                "Start with the two shortest signals. E is a single quick tone and T is a single long tone. Listen for length, not for dots on a page: a dah lasts three times as long as a dit."),
            new(2, "Common: I M S O", "IMSO", "Patterns that repeat the same sound.",
                "These four repeat one sound. I is two dits, S is three dits, M is two dahs and O is three dahs. Count the repeats and you have the letter. S and O together are the famous SOS."),
            new(3, "Expanding: A N R W", "ANRW", "Mirror pairs and their cousins.",
                "A (dit-dah) and N (dah-dit) are mirror images. R (dit-dah-dit) starts like A, and W (dit-dah-dah) is A with an extra dah. Learn them as pairs."),
            new(4, "Building speed: D K G U", "DKGU", "Three-element letters.",
                "All four have three elements. D is a dah then two dits, and U is the reverse: two dits then a dah. K wraps a dit inside two dahs, and G is two dahs then a dit."),
            new(5, "Advanced: L P J B", "LPJB", "Four-element letters, part one.",
                "L and P both open with dit-dah. L finishes with two dits and P with dah-dit. J is a dit followed by three dahs, and B is its mirror: a dah followed by three dits."),
            new(6, "Advanced: X C Y Z", "XCYZ", "Four-element letters, part two.",
                "These all lean on dahs. X is dah-dit-dit-dah, and Y turns X's second dit into a dah. C alternates dah-dit-dah-dit. Z is two dahs then two dits."),
            new(7, "Completing: Q F H V", "QFHV", "The last four letters.",
                "H is four dits and V is H with a dah on the end (V for Victory). F is dit-dit-dah-dit, and Q is dah-dah-dit-dah."),
            new(8, "Numbers 1 to 5", "12345", "Numbers count up in dits.",
                "Every number has five elements. Through 1 to 5 the dits take over one at a time: 1 has one dit, 2 has two, 3 has three, 4 has four and 5 is all dits."),
            new(9, "Numbers 6 to 0", "67890", "Numbers count up in dahs.",
                "6 to 0 mirror the first half, with the dahs taking over: 6 starts with one dah, 7 with two, 8 with three, 9 with four, and 0 is all dahs."),
            new(10, "Full review", LetterSet + NumberSet, "Everything, shuffled.",
                "The whole set, weakest characters first. Keep going until every character shows full mastery.")
        };

        private readonly Random _rng = new();
        private ListenView _listenView = ListenView.Hub;
        private int _lesson;                       // 0 = free practice, 1..10 = lesson
        private bool _useLetters = true, _useNumbers = true;
        private List<char> _pool = new(), _review = new(), _answerSet = new();
        private readonly Dictionary<char, int> _mastery = new();
        private bool _lessonCelebrated;

        private char _listenTarget = '\0';
        private bool _listenAnswered, _listenMissed, _listenMissedByReveal;
        private int _listenRounds, _listenFirstTry, _listenStreak;
        private readonly Dictionary<char, Border> _listenChips = new();
        private readonly HashSet<char> _listenWrong = new();
        private CancellationTokenSource? _listenCts;

        // ---------- persistence ----------
        private static readonly string ProgressPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MorseCodeLearner", "ear-progress.json");

        private void LoadProgress()
        {
            try
            {
                if (!System.IO.File.Exists(ProgressPath)) return;
                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(System.IO.File.ReadAllText(ProgressPath));
                if (data == null) return;
                foreach (var (k, v) in data)
                    if (k.Length == 1) _mastery[k[0]] = Math.Clamp(v, 0, MasteryTarget);
            }
            catch { /* corrupt or unreadable progress file: start fresh */ }
        }

        private void SaveProgress()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ProgressPath)!);
                System.IO.File.WriteAllText(ProgressPath, System.Text.Json.JsonSerializer.Serialize(_mastery.ToDictionary(p => p.Key.ToString(), p => p.Value)));
            }
            catch { /* progress is a convenience; never crash the trainer over it */ }
        }

        private int Level(char c) => _mastery.TryGetValue(c, out int v) ? v : 0;
        private void AdjustMastery(char c, int delta) { _mastery[c] = Math.Clamp(Level(c) + delta, 0, MasteryTarget); SaveProgress(); }
        private double LessonProgress(Lesson l) => l.Chars.Average(ch => Level(ch) / (double)MasteryTarget);

        // ---------- small UI helpers ----------
        private TextBlock Tb(string text, double size, string fg, bool bold = false, bool mono = false, bool display = false)
        {
            var t = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
            if (bold) t.FontWeight = FontWeights.Bold;
            if (mono) t.FontFamily = MonoFont; else if (display) t.FontFamily = (FontFamily)FindResource("Display");
            Ref(t, TextBlock.ForegroundProperty, fg);
            return t;
        }

        private Button DynButton(string text, bool primary, Action onClick, double padH = 14, double padV = 7)
        {
            var b = new Button { Content = text, Style = (Style)FindResource(primary ? "SignalBtn" : "KeyBtn"), Padding = new Thickness(padH, padV, padH, padV), FontSize = 12.5 };
            b.Click += (_, _) => onClick();
            return b;
        }

        private FrameworkElement MorseShapes(char c, double dit)
        {
            var wrap = new WrapPanel();
            foreach (string part in MorseOf(c).Split(' '))
            {
                bool dah = part == "─";
                FrameworkElement shape = dah
                    ? new System.Windows.Shapes.Rectangle { Width = dit * 3, Height = dit, RadiusX = dit / 2, RadiusY = dit / 2 }
                    : new System.Windows.Shapes.Ellipse { Width = dit, Height = dit };
                Ref(shape, System.Windows.Shapes.Shape.FillProperty, dah ? "Ice" : "Signal");
                shape.Margin = new Thickness(0, 0, 6, 4);
                wrap.Children.Add(shape);
            }
            return wrap;
        }

        private string Relations(char c)
        {
            string[] parts = MorseOf(c).Split(' ');
            string rev = string.Join(" ", parts.Reverse());
            string inv = string.Join(" ", parts.Select(p => p == "─" ? "•" : "─"));
            var rels = new List<string>();
            foreach (char o in LetterSet + NumberSet)
            {
                if (o == c) continue;
                string q = MorseOf(o);
                if (q == rev) rels.Add($"Reverse of {o}");
                if (q == inv) rels.Add($"Opposite of {o}");
            }
            return string.Join("  ·  ", rels);
        }

        private static bool TryKeyToChar(Key key, out char c)
        {
            c = '\0';
            if (key >= Key.A && key <= Key.Z) c = (char)('A' + (key - Key.A));
            else if (key >= Key.D0 && key <= Key.D9) c = (char)('0' + (key - Key.D0));
            else if (key >= Key.NumPad0 && key <= Key.NumPad9) c = (char)('0' + (key - Key.NumPad0));
            return c != '\0';
        }

        // ---------- views ----------
        private void SetListenView(ListenView v)
        {
            _listenView = v;
            ListenHubView.Visibility = v == ListenView.Hub ? Visibility.Visible : Visibility.Collapsed;
            ListenGuideView.Visibility = v == ListenView.Guide ? Visibility.Visible : Visibility.Collapsed;
            ListenDrillView.Visibility = v == ListenView.Drill ? Visibility.Visible : Visibility.Collapsed;
            bool pathActive = v != ListenView.Drill || _lesson > 0;
            foreach (var (btn, on) in new[] { (ListenTabPathBtn, pathActive), (ListenTabPracticeBtn, !pathActive) })
            {
                Ref(btn, Control.BackgroundProperty, on ? "Signal" : "Raised");
                Ref(btn, Control.ForegroundProperty, on ? "OnSignal" : "Text");
            }
            switch (v)
            {
                case ListenView.Hub: ListenEyebrow.Text = "LEVEL 4  ·  EAR TRAINING"; ListenTitle.Text = "Study path"; break;
                case ListenView.Guide: ListenEyebrow.Text = $"LESSON {_lesson}  ·  STUDY GUIDE"; ListenTitle.Text = Lessons[_lesson - 1].Title; break;
                default: ListenEyebrow.Text = "LEVEL 4  ·  EAR TRAINING"; ListenTitle.Text = _lesson > 0 ? $"Lesson {_lesson}: drills" : "Free practice"; break;
            }
        }

        private void OnListenTabPath(object sender, RoutedEventArgs e)
        {
            _listenCts?.Cancel();
            BuildHub();
            SetListenView(ListenView.Hub);
        }

        private void OnListenTabPractice(object sender, RoutedEventArgs e)
        {
            _lesson = 0;
            ConfigureScope();
            SetListenView(ListenView.Drill);
            NewListenRound(true);
        }

        private void StartLesson(int n)
        {
            _listenCts?.Cancel();
            _lesson = n;
            if (n == 10) { ConfigureScope(); SetListenView(ListenView.Drill); NewListenRound(true); return; }
            BuildGuide(n);
            SetListenView(ListenView.Guide);
        }

        private void OnGuideBegin(object sender, RoutedEventArgs e)
        {
            ConfigureScope();
            SetListenView(ListenView.Drill);
            NewListenRound(true);
        }

        private void OnGuideHearAll(object sender, RoutedEventArgs e) => PlaySequence(Lessons[_lesson - 1].Chars, 120, false);

        private void OnDrillGuide(object sender, RoutedEventArgs e)
        {
            _listenCts?.Cancel();
            if (_lesson is < 1 or > 9) { OnListenTabPath(sender, e); return; }
            BuildGuide(_lesson);
            SetListenView(ListenView.Guide);
        }

        private void OnNextLesson(object sender, RoutedEventArgs e) { if (_lesson < Lessons.Length) StartLesson(_lesson + 1); }

        private void OnListenResetProgress(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Clear all ear-training mastery and start the study path from lesson 1?", "Reset progress", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _mastery.Clear();
            SaveProgress();
            BuildHub();
        }

        private void OnToggleLetters(object sender, RoutedEventArgs e)
        {
            _useLetters = !_useLetters;
            if (!_useLetters && !_useNumbers) _useNumbers = true;
            ConfigureScope();
            NewListenRound(true);
        }

        private void OnToggleNumbers(object sender, RoutedEventArgs e)
        {
            _useNumbers = !_useNumbers;
            if (!_useLetters && !_useNumbers) _useLetters = true;
            ConfigureScope();
            NewListenRound(true);
        }

        // ---------- study path hub ----------
        private void BuildHub()
        {
            ListenLessonWrap.Children.Clear();
            string all = LetterSet + NumberSet;
            int mastered = all.Count(c => Level(c) >= MasteryTarget);
            double overall = all.Average(c => Level(c) / (double)MasteryTarget);
            ListenHubSummary.Text = $"{mastered} / {all.Length} characters mastered  ·  {Math.Round(overall * 100)}% overall";
            int nextUp = Array.FindIndex(Lessons, l => LessonProgress(l) < 0.999) + 1;

            foreach (var l in Lessons)
            {
                double pct = LessonProgress(l);
                var card = new Border { Width = 252, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(16), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1) };
                Ref(card, Border.BackgroundProperty, "Well");
                Ref(card, Border.BorderBrushProperty, l.Number == nextUp ? "Signal" : "Line");
                var stack = new StackPanel();

                var top = new DockPanel();
                string? badge = pct >= 0.999 ? "MASTERED" : (l.Number == nextUp ? "NEXT UP" : null);
                if (badge != null)
                {
                    var b = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2, 8, 2), Child = Tb(badge, 10, pct >= 0.999 ? "OnMint" : "OnSignal", bold: true, mono: true) };
                    Ref(b, Border.BackgroundProperty, pct >= 0.999 ? "Mint" : "Signal");
                    DockPanel.SetDock(b, Dock.Right);
                    top.Children.Add(b);
                }
                top.Children.Add(Tb($"LESSON {l.Number}", 12, "Accent", bold: true, display: true));
                stack.Children.Add(top);

                var title = Tb(l.Title, 17, "Text", bold: true, display: true); title.Margin = new Thickness(0, 4, 0, 0);
                var blurb = Tb(l.Blurb, 12.5, "Muted"); blurb.Margin = new Thickness(0, 4, 0, 10); blurb.MinHeight = 34;
                stack.Children.Add(title); stack.Children.Add(blurb);

                var chars = new WrapPanel { MinHeight = 56 };
                if (l.Chars.Length <= 10)
                {
                    foreach (char ch in l.Chars)
                    {
                        bool done = Level(ch) >= MasteryTarget;
                        var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                        var letter = Tb(ch.ToString(), 15, done ? "Mint" : "Text", bold: true, mono: true); letter.TextAlignment = TextAlignment.Center;
                        var m = Tb(MorseOf(ch).Replace(" ", ""), 9, "Muted", mono: true); m.TextAlignment = TextAlignment.Center;
                        cell.Children.Add(letter); cell.Children.Add(m);
                        var chip = new Border { Width = 42, Padding = new Thickness(0, 5, 0, 5), CornerRadius = new CornerRadius(9), Margin = new Thickness(0, 0, 5, 5), Child = cell, BorderThickness = new Thickness(1) };
                        Ref(chip, Border.BackgroundProperty, "Raised");
                        Ref(chip, Border.BorderBrushProperty, done ? "Mint" : "Line");
                        chars.Children.Add(chip);
                    }
                }
                else chars.Children.Add(Tb("All 36 letters and numbers", 12.5, "Text", display: true));
                stack.Children.Add(chars);

                var bar = new Grid { Height = 6, Margin = new Thickness(0, 8, 0, 4) };
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, pct), GridUnitType.Star) });
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, 1 - pct), GridUnitType.Star) });
                var track = new Border { CornerRadius = new CornerRadius(3) }; Grid.SetColumnSpan(track, 2); Ref(track, Border.BackgroundProperty, "Raised");
                var fill = new Border { CornerRadius = new CornerRadius(3) }; Ref(fill, Border.BackgroundProperty, pct >= 0.999 ? "Mint" : "Signal");
                bar.Children.Add(track); bar.Children.Add(fill);
                stack.Children.Add(bar);
                stack.Children.Add(Tb($"{Math.Round(pct * 100)}% mastered", 11.5, "Muted", mono: true));

                int n = l.Number;
                var go = DynButton(pct <= 0 ? "Start lesson  →" : (pct < 0.999 ? "Continue  →" : "Review  →"), l.Number == nextUp || pct <= 0, () => StartLesson(n), 16, 8);
                go.Margin = new Thickness(0, 12, 0, 0);
                go.HorizontalAlignment = HorizontalAlignment.Left;
                stack.Children.Add(go);

                card.Child = stack;
                ListenLessonWrap.Children.Add(card);
            }
        }

        // ---------- lesson study guide ----------
        private void BuildGuide(int n)
        {
            var l = Lessons[n - 1];
            GuideTip.Text = l.Tip + "  Hear each one, say its sound aloud (dit / dah), then start the drills.";
            GuideCardsWrap.Children.Clear();
            foreach (char ch in l.Chars)
            {
                var card = new Border { Width = 200, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(16, 14, 16, 14), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1) };
                Ref(card, Border.BackgroundProperty, "Well");
                Ref(card, Border.BorderBrushProperty, "Line");
                var stack = new StackPanel();
                stack.Children.Add(Tb(ch.ToString(), 46, "Text", bold: true, display: true));
                var shapes = MorseShapes(ch, 12); shapes.Margin = new Thickness(0, 2, 0, 4);
                stack.Children.Add(shapes);
                stack.Children.Add(Tb(GetPhoneticHint(ch), 13, "Muted", display: true));
                string rel = Relations(ch);
                if (rel.Length > 0) { var r = Tb(rel, 12, "Accent", bold: true, display: true); r.Margin = new Thickness(0, 6, 0, 0); stack.Children.Add(r); }
                var hear = DynButton("▶  Hear", false, () => PlaySequence(ch.ToString(), 100, false), 12, 6);
                hear.Margin = new Thickness(0, 12, 0, 0); hear.HorizontalAlignment = HorizontalAlignment.Left;
                stack.Children.Add(hear);
                card.Child = stack;
                GuideCardsWrap.Children.Add(card);
            }
        }

        // ---------- drill scope ----------
        private void ConfigureScope()
        {
            string order = LetterSet + NumberSet;
            if (_lesson == 0)
            {
                _pool = ((_useLetters ? LetterSet : "") + (_useNumbers ? NumberSet : "")).ToList();
                _review = new List<char>();
                ListenScopeLabel.Text = _useLetters && _useNumbers ? "FREE PRACTICE  ·  LETTERS + NUMBERS" : (_useLetters ? "FREE PRACTICE  ·  LETTERS ONLY" : "FREE PRACTICE  ·  NUMBERS ONLY");
                foreach (var (btn, on) in new[] { (ListenLettersBtn, _useLetters), (ListenNumbersBtn, _useNumbers) })
                {
                    Ref(btn, Control.BackgroundProperty, on ? "Signal" : "Raised");
                    Ref(btn, Control.ForegroundProperty, on ? "OnSignal" : "Muted");
                }
            }
            else
            {
                var l = Lessons[_lesson - 1];
                _pool = l.Chars.ToList();
                _review = _lesson == 10 ? new List<char>() : Lessons.Take(_lesson - 1).SelectMany(x => x.Chars).Distinct().ToList();
                ListenScopeLabel.Text = $"LESSON {_lesson}  ·  {string.Join(" ", _pool.Take(10))}" + (_pool.Count > 10 ? " ..." : "");
            }
            _answerSet = _pool.Union(_review).OrderBy(c => order.IndexOf(c)).ToList();
            ListenFilterPanel.Visibility = _lesson == 0 ? Visibility.Visible : Visibility.Collapsed;
            ListenLessonBar.Visibility = _lesson == 0 ? Visibility.Collapsed : Visibility.Visible;
            ListenNextLessonBtn.Visibility = Visibility.Collapsed;
            _lessonCelebrated = false;
            _listenTarget = '\0';
            _listenRounds = _listenFirstTry = _listenStreak = 0;

            ListenChipsWrap.Children.Clear();
            _listenChips.Clear();
            foreach (char ch in _answerSet)
            {
                var tb = new TextBlock { Text = ch.ToString(), FontFamily = MonoFont, FontSize = 14, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var chip = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 5, 5), Cursor = Cursors.Hand, Child = tb };
                char guess = ch;
                chip.MouseLeftButtonUp += (_, _) => SubmitGuess(guess);
                _listenChips[ch] = chip;
                ListenChipsWrap.Children.Add(chip);
            }
            RefreshMasteryStrip();
            RefreshListenChips();
        }

        private void RefreshMasteryStrip()
        {
            ListenMasteryWrap.Children.Clear();
            if (_lesson == 0) return;
            foreach (char ch in _pool.Take(10))
            {
                var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var letter = Tb(ch.ToString(), 13, Level(ch) >= MasteryTarget ? "Mint" : "Text", bold: true, mono: true); letter.TextAlignment = TextAlignment.Center;
                cell.Children.Add(letter);
                var pips = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
                for (int i = 0; i < MasteryTarget; i++)
                {
                    var pip = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Margin = new Thickness(1, 0, 1, 0) };
                    Ref(pip, System.Windows.Shapes.Shape.FillProperty, i < Level(ch) ? "Mint" : "Line");
                    pips.Children.Add(pip);
                }
                cell.Children.Add(pips);
                ListenMasteryWrap.Children.Add(new Border { Width = 30, Padding = new Thickness(0, 3, 0, 4), Margin = new Thickness(0, 0, 3, 0), Child = cell, ToolTip = $"{ch}: {Level(ch)} / {MasteryTarget}" });
            }
        }

        private void RefreshListenChips()
        {
            foreach (var (ch, chip) in _listenChips)
            {
                bool right = _listenAnswered && ch == _listenTarget && !_listenMissedByReveal;
                bool wrong = _listenWrong.Contains(ch);
                Ref(chip, Border.BackgroundProperty, right ? "Mint" : "Raised");
                Ref(chip, Border.BorderBrushProperty, right ? "Mint" : (wrong ? "Alert" : "Line"));
                Ref((TextBlock)chip.Child, TextBlock.ForegroundProperty, right ? "OnMint" : (wrong ? "Alert" : "Muted"));
                chip.Opacity = wrong ? 0.6 : 1;
            }
        }

        // ---------- the drill itself ----------
        private double Weight(char c) => MasteryTarget + 1 - Level(c);

        private char PickNext()
        {
            var source = _review.Count > 0 && _rng.NextDouble() < 0.2 ? _review : _pool;
            var cands = source.Where(c => c != _listenTarget).ToList();
            if (cands.Count == 0) cands = _pool.Where(c => c != _listenTarget).ToList();
            if (cands.Count == 0) return _pool.Count > 0 ? _pool[0] : 'E';
            double r = _rng.NextDouble() * cands.Sum(Weight);
            foreach (char c in cands) { r -= Weight(c); if (r <= 0) return c; }
            return cands[^1];
        }

        private void NewListenRound(bool autoPlay)
        {
            _listenTarget = PickNext();
            _listenAnswered = false; _listenMissed = false; _listenMissedByReveal = false;
            _listenWrong.Clear();
            ListenAnswerChar.Text = "?";
            ListenDiagramPanel.Visibility = Visibility.Collapsed;
            ListenDiagramHint.Visibility = Visibility.Visible;
            ListenNextBtn.IsEnabled = false;
            ListenRevealBtn.IsEnabled = true;
            ListenFeedbackText.Text = "Listening...";
            Ref(ListenFeedbackText, TextBlock.ForegroundProperty, "Ice");
            RefreshListenChips();
            UpdateListenScore();
            if (autoPlay) PlayListenSound(300);
        }

        private void UpdateListenScore()
        {
            int pct = _listenRounds == 0 ? 0 : (int)Math.Round(100.0 * _listenFirstTry / _listenRounds);
            ListenScoreText.Text = $"First try: {_listenFirstTry} / {_listenRounds} ({pct}%)  ·  Streak: {_listenStreak}";
        }

        private void SubmitGuess(char guess)
        {
            if (_currentMode != ActiveMode.Listen || _listenView != ListenView.Drill || _listenAnswered || _listenTarget == '\0') return;
            guess = char.ToUpper(guess);
            if (!_answerSet.Contains(guess))
            {
                ListenFeedbackText.Text = $"{guess} is not in this practice set.";
                Ref(ListenFeedbackText, TextBlock.ForegroundProperty, "Muted");
                return;
            }
            if (guess == _listenTarget)
            {
                _listenAnswered = true;
                _listenRounds++;
                bool first = !_listenMissed;
                if (first) { _listenFirstTry++; _listenStreak++; AdjustMastery(_listenTarget, +1); }
                ListenFeedbackText.Text = first ? $"Correct on the first try: {_listenTarget}" : $"Correct: it was {_listenTarget}";
                Ref(ListenFeedbackText, TextBlock.ForegroundProperty, "Mint");
                RevealListen();
                CheckLessonComplete();
            }
            else
            {
                if (!_listenMissed) AdjustMastery(_listenTarget, -1);
                _listenWrong.Add(guess);
                _listenMissed = true;
                _listenStreak = 0;
                ListenFeedbackText.Text = $"Not {guess}. Replay the sound and try again.";
                Ref(ListenFeedbackText, TextBlock.ForegroundProperty, "Alert");
                RefreshListenChips();
                RefreshMasteryStrip();
                UpdateListenScore();
            }
        }

        private void CheckLessonComplete()
        {
            RefreshMasteryStrip();
            if (_lesson == 0 || _lessonCelebrated || !_pool.All(c => Level(c) >= MasteryTarget)) return;
            _lessonCelebrated = true;
            ListenFeedbackText.Text += _lesson < Lessons.Length ? "   Lesson mastered!" : "   Every character mastered!";
            if (_lesson < Lessons.Length) ListenNextLessonBtn.Visibility = Visibility.Visible;
        }

        private void RevealListen()
        {
            ListenAnswerChar.Text = _listenTarget.ToString();
            ListenDiagram.Children.Clear();
            string morse = MorseOf(_listenTarget);
            foreach (string part in morse.Split(' '))
            {
                bool dah = part == "─";
                FrameworkElement shape = dah
                    ? new System.Windows.Shapes.Rectangle { Width = 62, Height = 20, RadiusX = 10, RadiusY = 10 }
                    : new System.Windows.Shapes.Ellipse { Width = 20, Height = 20 };
                Ref(shape, System.Windows.Shapes.Shape.FillProperty, dah ? "Ice" : "Signal");
                shape.Margin = new Thickness(0, 0, 12, 4);
                ListenDiagram.Children.Add(shape);
            }
            ListenPhonetic.Text = $"{_listenTarget}   {morse}   ·   sounds like \"{GetPhoneticHint(_listenTarget)}\"";
            ListenDiagramHint.Visibility = Visibility.Collapsed;
            ListenDiagramPanel.Visibility = Visibility.Visible;
            ListenNextBtn.IsEnabled = true;
            ListenRevealBtn.IsEnabled = false;
            RefreshListenChips();
            UpdateListenScore();
        }

        private void OnListenReplay(object sender, RoutedEventArgs e) => PlayListenSound(150);
        private void OnListenNext(object sender, RoutedEventArgs e) { if (_listenAnswered) NewListenRound(true); }
        private void OnListenReveal(object sender, RoutedEventArgs e)
        {
            if (_listenAnswered) return;
            if (!_listenMissed) AdjustMastery(_listenTarget, -1);
            _listenAnswered = true;
            _listenMissedByReveal = true;
            _listenRounds++;
            _listenStreak = 0;
            ListenFeedbackText.Text = $"It was {_listenTarget}. Listen again and match the sound to the diagram.";
            Ref(ListenFeedbackText, TextBlock.ForegroundProperty, "Ice");
            RevealListen();
            RefreshMasteryStrip();
        }

        private void PlayListenSound(int startDelayMs = 150)
        {
            if (_listenTarget != '\0') PlaySequence(_listenTarget.ToString(), startDelayMs, true);
        }

        // Plays characters at the current WPM on a background thread; every key-down / key-up is also
        // fed to the key scope so the sound can be seen as well as heard.
        private void PlaySequence(string chars, int startDelayMs, bool updateButton)
        {
            _listenCts?.Cancel();
            var cts = new CancellationTokenSource();
            _listenCts = cts;
            var ct = cts.Token;
            double unitMs = _unitUs / 1000.0;
            if (updateButton) ListenPlayBtn.Content = "▶  Playing...";
            Task.Run(() =>
            {
                try
                {
                    if (!PreciseDelay(startDelayMs, ct)) return;
                    for (int k = 0; k < chars.Length; k++)
                    {
                        ushort packed = MorseEngineService.EncodePacked(chars[k]);
                        int count = packed & 0xFF;
                        byte pattern = (byte)((packed >> 8) & 0xFF);
                        for (int i = count - 1; i >= 0; i--)
                        {
                            bool dah = ((pattern >> i) & 1) == 1;
                            long t0 = Stopwatch.GetTimestamp();
                            _audioEngine.StartTone();
                            Dispatcher.BeginInvoke(() => KeyScope.Begin(t0));
                            bool ok = PreciseDelay(dah ? unitMs * 3 : unitMs, ct);
                            _audioEngine.StopTone();
                            long t1 = Stopwatch.GetTimestamp();
                            Dispatcher.BeginInvoke(() => KeyScope.End(dah ? 2 : 1, t1));
                            if (!ok) return;
                            if (i > 0 && !PreciseDelay(unitMs, ct)) return;
                        }
                        if (k < chars.Length - 1 && !PreciseDelay(unitMs * 3, ct)) return;
                    }
                }
                finally
                {
                    if (updateButton && _listenCts == cts)
                        Dispatcher.BeginInvoke(() => ListenPlayBtn.Content = "▶  Play sound");
                }
            });
        }

        private static bool PreciseDelay(double ms, CancellationToken ct)
        {
            long end = Stopwatch.GetTimestamp() + (long)(ms * Stopwatch.Frequency / 1000.0);
            while (true)
            {
                if (ct.IsCancellationRequested) return false;
                double remaining = (end - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
                if (remaining <= 0) return true;
                if (remaining > 3) Thread.Sleep(1); else Thread.SpinWait(100);
            }
        }
        #endregion
        #region Filler content: alphabet chips, reference chart, full-morse line
        private readonly HashSet<char> _mastered = new();
        private readonly List<Border> _chips = new();
        private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");

        private string MorseOf(char c)
        {
            if (c == ' ') return "/";
            ushort packed = MorseEngineService.EncodePacked(c);
            int count = packed & 0xFF;
            byte pattern = (byte)((packed >> 8) & 0xFF);
            var parts = new List<string>();
            for (int i = count - 1; i >= 0; i--) parts.Add(((pattern >> i) & 1) == 1 ? "\u2500" : "\u2022");
            return string.Join(" ", parts);
        }

        private void BuildLearnChips()
        {
            for (int idx = 0; idx < _alphabet.Length; idx++)
            {
                char ch = _alphabet[idx];
                var tb = new TextBlock { Text = ch.ToString(), FontFamily = MonoFont, FontSize = 13, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var chip = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 5, 5), Cursor = Cursors.Hand, Child = tb, ToolTip = $"{ch}   {MorseOf(ch)}" };
                int target = idx;
                chip.MouseLeftButtonUp += (_, _) => { _learnIndex = target; LoadLearningChar(); };
                _chips.Add(chip);
                LearnChipsWrap.Children.Add(chip);
            }
            RefreshLearnChips();
        }

        private void RefreshLearnChips()
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                bool cur = i == _learnIndex, done = _mastered.Contains(_alphabet[i]);
                Ref(_chips[i], Border.BackgroundProperty, cur ? "Signal" : "Raised");
                Ref(_chips[i], Border.BorderBrushProperty, cur ? "Signal" : (done ? "Mint" : "Line"));
                Ref((TextBlock)_chips[i].Child, TextBlock.ForegroundProperty, cur ? "OnSignal" : (done ? "Mint" : "Muted"));
            }
            if (LearnChipsLabel != null)
                LearnChipsLabel.Text = $"ALPHABET  \u00B7  {_mastered.Count} / {_alphabet.Length} MASTERED  \u00B7  CLICK ANY CHARACTER TO JUMP TO IT";
        }

        private void BuildReference()
        {
            foreach (char ch in _alphabet)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Width = 100, Margin = new Thickness(0, 0, 0, 8) };
                var l = new TextBlock { Text = ch.ToString(), FontFamily = (FontFamily)FindResource("Display"), FontSize = 16, FontWeight = FontWeights.Bold, Width = 24, VerticalAlignment = VerticalAlignment.Center };
                var m = new TextBlock { Text = MorseOf(ch), FontFamily = MonoFont, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
                Ref(l, TextBlock.ForegroundProperty, "Accent");
                Ref(m, TextBlock.ForegroundProperty, "Text");
                row.Children.Add(l); row.Children.Add(m);
                ReferenceWrap.Children.Add(row);
            }
        }

        private void OnSandboxTextChanged(object sender, TextChangedEventArgs e)
        {
            SandboxHint.Visibility = string.IsNullOrEmpty(SandboxStreamBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion
        #region Theme, Rail, Responsive Layout
        private Button _activeTab = null!;
        private bool _isDark;
        private bool _railCollapsed;
        private int _widthBand = -1;
        private const double RailOpen = 256, RailClosed = 78;

        private static readonly (string Key, string Light, string Dark)[] Palette =
        {
            ("Bg",       "#E8E3D6", "#0A0B0E"),
            ("Rail",     "#F0ECE0", "#0E1014"),
            ("Surface",  "#E8F4F0E6", "#CC14171D"),
            ("Raised",   "#E4DECF", "#1A1D25"),
            ("Line",     "#CBC2AD", "#262A35"),
            ("Text",     "#1B1A17", "#E8EAF0"),
            ("Muted",    "#55524A", "#9AA1B4"),
            ("Signal",   "#FF6B2C", "#B8FF4A"),
            ("Accent",   "#AE3700", "#B8FF4A"),
            ("Ice",      "#1A4FB4", "#7CC4FF"),
            ("Mint",     "#0A6B49", "#4AD6A8"),
            ("Alert",    "#B42318", "#FF5D5D"),
            ("OnSignal", "#1B0F07", "#0A0B0E"),
            ("OnMint",   "#FFFFFF", "#0A0B0E"),
            ("Well",     "#FAF7EF", "#0D0F13"),
        };

        // Resource references use SetResourceReference, so they follow the palette automatically.
        private void Ref(DependencyObject target, DependencyProperty prop, string key)
        {
            if (target is FrameworkElement fe) fe.SetResourceReference(prop, key);
        }

        private readonly DispatcherTimer _themeTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private Color[] _themeFrom = Array.Empty<Color>(), _themeTo = Array.Empty<Color>();
        private DateTime _themeStart;
        private const double ThemeMs = 280;

        private void SetPalette(Color[] colors)
        {
            for (int i = 0; i < Palette.Length; i++)
                Resources[Palette[i].Key] = new SolidColorBrush(colors[i]);
            Resources["SignalColor"] = colors[Array.FindIndex(Palette, p => p.Key == "Signal")];
            Resources["IceColor"] = colors[Array.FindIndex(Palette, p => p.Key == "Ice")];
            Resources["RaisedColor"] = colors[Array.FindIndex(Palette, p => p.Key == "Raised")];
            Resources["WellColor"] = colors[Array.FindIndex(Palette, p => p.Key == "Well")];
        }

        private Color[] CurrentPalette() =>
            Array.ConvertAll(Palette, p => ((SolidColorBrush)Resources[p.Key]).Color);

        private void ApplyTheme(bool dark, bool animate)
        {
            _themeFrom = CurrentPalette();
            _themeTo = Array.ConvertAll(Palette, p => (Color)ColorConverter.ConvertFromString(dark ? p.Dark : p.Light));
            _themeStart = DateTime.UtcNow;
            _themeTimer.Tick -= OnThemeTick;
            _themeTimer.Tick += OnThemeTick;
            if (animate) _themeTimer.Start(); else SetPalette(_themeTo);
            ThemeIcon.Text = dark ? "\u2600" : "\u263E";
            ThemeLabel.Text = dark ? "Light mode" : "Dark mode";
            BlobA.Opacity = dark ? 0.11 : 0.16;
            BlobB.Opacity = dark ? 0.1 : 0.12;
        }

        private void OnThemeTick(object? s, EventArgs e)
        {
            double t = Math.Min(1, (DateTime.UtcNow - _themeStart).TotalMilliseconds / ThemeMs);
            double k = 1 - Math.Pow(1 - t, 3);
            var mix = new Color[Palette.Length];
            for (int i = 0; i < mix.Length; i++)
            {
                Color a = _themeFrom[i], b = _themeTo[i];
                mix[i] = Color.FromArgb(
                    (byte)(a.A + (b.A - a.A) * k), (byte)(a.R + (b.R - a.R) * k),
                    (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
            }
            SetPalette(mix);
            if (t >= 1) _themeTimer.Stop();
        }
        private void OnToggleTheme(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;
            ApplyTheme(_isDark, true);
        }

        private void OnToggleRail(object sender, RoutedEventArgs e) => SetRail(!_railCollapsed);

        private void SetRail(bool collapsed)
        {
            _railCollapsed = collapsed;
            var vis = collapsed ? Visibility.Collapsed : Visibility.Visible;
            foreach (var el in new FrameworkElement[] { NavLbl1, NavLbl2, NavLbl3, NavLbl4, NavSectionLabel, BrandText, ThemeLabel, CollapseLabel })
                el.Visibility = vis;
            foreach (var b in new[] { TabLearningBtn, TabSentenceBtn, TabSandboxBtn, TabListenBtn, ThemeBtn, CollapseBtn })
            {
                b.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                b.Padding = collapsed ? new Thickness(0, 12, 0, 12) : new Thickness(14, 12, 14, 12);
            }
            CollapseIcon.Text = collapsed ? "»" : "«";
            RailBorder.BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(collapsed ? RailClosed : RailOpen, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            });
            ApplyResponsive();
        }

        // Auto-collapse the rail on narrow windows and reflow panels for the space that is left.
        private void ApplyResponsive()
        {
            if (!IsLoaded && ActualWidth == 0) return;
            int band = ActualWidth < 1120 ? 0 : 1;
            if (band != _widthBand)
            {
                _widthBand = band;
                bool want = band == 0;
                if (want != _railCollapsed) { SetRail(want); return; }
            }
            double main = ActualWidth - (_railCollapsed ? RailClosed : RailOpen) - 52;
            bool compactLearn = main < 640;
            bool compactConsole = main < 860;
            bool shortWin = ActualHeight < 720;
            LampScale.ScaleX = LampScale.ScaleY = shortWin ? 0.62 : 0.8;
            LearnHeader.Visibility = shortWin ? Visibility.Collapsed : Visibility.Visible;
            LearnChipsPanel.Visibility = shortWin ? Visibility.Collapsed : Visibility.Visible;
            SandboxRef.Visibility = main < 780 ? Visibility.Collapsed : Visibility.Visible;
            double tile = shortWin ? 150 : (compactLearn ? 170 : Math.Clamp(ActualHeight - (compactConsole ? 660 : 600), 120, 230));
            LearnGlyphTile.Width = LearnGlyphTile.Height = tile;
            Grid.SetColumnSpan(LearnGlyphTile, compactLearn ? 2 : 1);
            Grid.SetColumn(LearnGlyphTile, 0);
            LearnGlyphTile.HorizontalAlignment = compactLearn ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            LearnGlyphTile.Margin = compactLearn ? new Thickness(0, 0, 0, 20) : new Thickness(0, 0, 44, 0);
            Grid.SetRow(LearnInfoPanel, compactLearn ? 1 : 0);
            Grid.SetColumn(LearnInfoPanel, compactLearn ? 0 : 1);
            Grid.SetColumnSpan(LearnInfoPanel, compactLearn ? 2 : 1);
            LearnInfoPanel.MinWidth = compactLearn ? 0 : 320;

            ListenTile.Width = ListenTile.Height = shortWin ? 150 : (compactLearn ? 170 : Math.Clamp(ActualHeight - (compactConsole ? 690 : 630), 120, 210));
            Grid.SetColumnSpan(ListenTile, compactLearn ? 2 : 1);
            ListenTile.HorizontalAlignment = compactLearn ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            ListenTile.Margin = compactLearn ? new Thickness(0, 0, 0, 20) : new Thickness(0, 0, 40, 0);
            Grid.SetRow(ListenInfoPanel, compactLearn ? 1 : 0);
            Grid.SetColumn(ListenInfoPanel, compactLearn ? 0 : 1);
            Grid.SetColumnSpan(ListenInfoPanel, compactLearn ? 2 : 1);
            ListenInfoPanel.MinWidth = compactLearn ? 0 : 380;
            ListenTitleBlock.Visibility = shortWin ? Visibility.Collapsed : Visibility.Visible;
            ListenChipsPanel.Visibility = shortWin ? Visibility.Collapsed : Visibility.Visible;

            
            Grid.SetRow(TelemetryBar, compactConsole ? 1 : 0);
            Grid.SetColumn(TelemetryBar, compactConsole ? 0 : 2);
            Grid.SetColumnSpan(TelemetryBar, compactConsole ? 3 : 1);
            TelemetryBar.HorizontalAlignment = compactConsole ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            TelemetryBar.Margin = compactConsole ? new Thickness(0, 14, 0, 0) : new Thickness(0);
        }

        // Endless dit/dah ticker spelling the app name, drawn from real shapes so the loop is seamless.
        private void BuildTicker()
        {
            const string msg = "MORSELING";
            const double dit = 6, dah = 20, sym = 6, letter = 16, word = 34;
            var cycle = new List<FrameworkElement>();
            double width = 0;
            void Add(FrameworkElement el, double w) { cycle.Add(el); width += w; }
            foreach (char c in msg)
            {
                if (c == ' ') { Add(new Border { Width = word }, word); continue; }
                ushort packed = MorseEngineService.EncodePacked(c);
                int count = packed & 0xFF; byte pattern = (byte)((packed >> 8) & 0xFF);
                for (int i = count - 1; i >= 0; i--)
                {
                    bool isDah = ((pattern >> i) & 1) == 1;
                    FrameworkElement shape = isDah
                        ? new System.Windows.Shapes.Rectangle { Width = dah, Height = dit, RadiusX = 3, RadiusY = 3 }
                        : new System.Windows.Shapes.Ellipse { Width = dit, Height = dit };
                    Ref(shape, System.Windows.Shapes.Shape.FillProperty, "Text");
                    shape.VerticalAlignment = VerticalAlignment.Center;
                    Add(shape, isDah ? dah : dit);
                    Add(new Border { Width = sym }, sym);
                }
                Add(new Border { Width = letter }, letter);
            }
            Add(new Border { Width = word }, word);
            int repeats = (int)Math.Ceiling(2400 / width) + 1;
            for (int r = 0; r < repeats; r++)
            {
                for (int k = 0; k < cycle.Count; k++)
                {
                    var src = cycle[k];
                    FrameworkElement copy = src switch
                    {
                        System.Windows.Shapes.Rectangle rect => new System.Windows.Shapes.Rectangle { Width = rect.Width, Height = rect.Height, RadiusX = 3, RadiusY = 3, VerticalAlignment = VerticalAlignment.Center },
                        System.Windows.Shapes.Ellipse el => new System.Windows.Shapes.Ellipse { Width = el.Width, Height = el.Height, VerticalAlignment = VerticalAlignment.Center },
                        _ => new Border { Width = src.Width }
                    };
                    if (copy is System.Windows.Shapes.Shape) Ref(copy, System.Windows.Shapes.Shape.FillProperty, "Text");
                    TickerStrip.Children.Add(copy);
                }
            }
            TickerShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, -width, TimeSpan.FromSeconds(width / 28)) { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
        }
        #endregion
        #region Navigation & Settings Toggle
        private void OnSelectLearningMode(object sender, RoutedEventArgs e) => ShowMode(ActiveMode.Learning, TabLearningBtn, "LEARNING");
        private void OnSelectSentenceMode(object sender, RoutedEventArgs e) => ShowMode(ActiveMode.SentenceChallenge, TabSentenceBtn, "SENTENCE");
        private void OnSelectSandboxMode(object sender, RoutedEventArgs e) => ShowMode(ActiveMode.Sandbox, TabSandboxBtn, "SANDBOX");
        private void OnSelectListenMode(object sender, RoutedEventArgs e) => ShowMode(ActiveMode.Listen, TabListenBtn, "EAR TRAINING");

        private void ShowMode(ActiveMode mode, Button tab, string metric)
        {
            if (_currentMode == ActiveMode.Listen && mode != ActiveMode.Listen) _listenCts?.Cancel();
            _currentMode = mode;
            SetTabColors(tab);
            PanelLearning.Visibility = mode == ActiveMode.Learning ? Visibility.Visible : Visibility.Collapsed;
            PanelSentence.Visibility = mode == ActiveMode.SentenceChallenge ? Visibility.Visible : Visibility.Collapsed;
            PanelSandbox.Visibility = mode == ActiveMode.Sandbox ? Visibility.Visible : Visibility.Collapsed;
            PanelListen.Visibility = mode == ActiveMode.Listen ? Visibility.Visible : Visibility.Collapsed;
            ModeMetric.Text = metric;
            if (mode == ActiveMode.Listen)
            {
                if (_listenView == ListenView.Drill) NewListenRound(true);
                else if (_listenView == ListenView.Hub) BuildHub();
            }
        }        private void SetTabColors(Button activeBtn)
        {
            _activeTab = activeBtn;
            foreach (var b in new[] { TabLearningBtn, TabSentenceBtn, TabSandboxBtn, TabListenBtn })
            {
                bool on = b == activeBtn;
                if (on) Ref(b, Control.BackgroundProperty, "Signal"); else b.Background = Brushes.Transparent;
                Ref(b, Control.ForegroundProperty, on ? "OnSignal" : "Muted");
            }
            Keyboard.ClearFocus();
            this.Focus();
        }
        private void OnToggleTelemetry(object sender, RoutedEventArgs e)
        {
            if (TelemetryBar.Visibility == Visibility.Visible)
            {
                TelemetryBar.Visibility = Visibility.Collapsed;
                ToggleStatsBtn.Content = "Telemetry  OFF";
                Ref(ToggleStatsBtn, Control.ForegroundProperty, "Muted");
            }
            else
            {
                TelemetryBar.Visibility = Visibility.Visible;
                ToggleStatsBtn.Content = "Telemetry  ON";
                Ref(ToggleStatsBtn, Control.ForegroundProperty, "Accent");
            }
        }
        private void OnDecreaseWpm(object sender, RoutedEventArgs e)
        {
            if (_wpm > 5) { _wpm--; WpmDisplay.Text = $"{_wpm} WPM"; UpdateScopeUnit(); }
        }
        private void OnIncreaseWpm(object sender, RoutedEventArgs e)
        {
            if (_wpm < 30) { _wpm++; WpmDisplay.Text = $"{_wpm} WPM"; UpdateScopeUnit(); }
        }
        private void OnDecreaseFrequency(object sender, RoutedEventArgs e)
        {
            if (_audioEngine.Frequency > 300)
            {
                _audioEngine.Frequency -= 50;
                FrequencyDisplay.Text = $"{_audioEngine.Frequency} Hz";
            }
        }
        private void OnIncreaseFrequency(object sender, RoutedEventArgs e)
        {
            if (_audioEngine.Frequency < 1500)
            {
                _audioEngine.Frequency += 50;
                FrequencyDisplay.Text = $"{_audioEngine.Frequency} Hz";
            }
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
                    Ref(SignalBulb, System.Windows.Shapes.Shape.FillProperty, "Signal");
                    SignalGlow.Radius = 24;
                    SignalStatusText.Text = "KEY DOWN";
                    Ref(SignalStatusText, TextBlock.ForegroundProperty, "OnSignal");
                    _audioEngine.StartTone();
                    KeyScope.Begin();
                }
                return;
            }
            if (_currentMode == ActiveMode.Listen && _listenView == ListenView.Drill && !e.IsRepeat)
            {
                if (TryKeyToChar(e.Key, out char typed)) { e.Handled = true; SubmitGuess(typed); return; }
                if (e.Key == Key.Return) { e.Handled = true; if (_listenAnswered) NewListenRound(true); else PlayListenSound(); return; }
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
                    _audioEngine.StopTone();
                    Ref(SignalBulb, System.Windows.Shapes.Shape.FillProperty, "Raised");
                    SignalGlow.Radius = 0;
                    SignalStatusText.Text = "KEY UP";
                    Ref(SignalStatusText, TextBlock.ForegroundProperty, "Muted");
                    ulong durationUs = (ulong)(_pressTimer.Elapsed.TotalMilliseconds * 1000);
                    DurationMetric.Text = $"{_pressTimer.ElapsedMilliseconds} ms";
                    // Native MASM quantizer call with RDTSC cycle telemetry
                    ulong startCycles = MorseEngineService.GetCycles();
                    uint token = MorseEngineService.QuantizePressUs(durationUs, _unitUs);
                    ulong elapsedCycles = MorseEngineService.GetCycles() - startCycles;
                    LatencyMetric.Text = $"{elapsedCycles} cycles";
                    KeyScope.End((int)token);
                    KeyReadout.Text = $"LAST {_pressTimer.ElapsedMilliseconds} ms  \u2192  {TokenName((int)token)}   \u00B7   1 unit = {_unitUs / 1000.0:0} ms";

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
            if (_isKeyPressed)
            {
                double held = _pressTimer.Elapsed.TotalMilliseconds;
                int liveKind = (int)MorseEngineService.QuantizePressUs((ulong)(held * 1000), _unitUs);
                KeyReadout.Text = $"HOLD {held:0} ms  \u2192  {TokenName(liveKind)}";
                return;
            }
            ulong idleUs = (ulong)(_idleTimer.Elapsed.TotalMilliseconds * 1000);
            uint gapAction = MorseEngineService.EvaluateGapUs(idleUs, _unitUs);

            // Phase 1: Inter-Character Gap (Commit Character)
            if (_elementCount > 0 && (gapAction == 1 || gapAction == 2))
            {
                byte decodedAscii = MorseEngineService.DecodeRaw(_elementCount, _patternMask);
                char decoded = (char)decodedAscii;
                if (decoded != '\0')
                {
                    DispatchDecodedCharacter(decoded);
                }
                _elementCount = 0;
                _patternMask = 0;
                CurrentTokenBuffer.Text = string.Empty;
            }
            // Phase 2: Word Space Gap (Commit Space)
            if (_elementCount == 0 && gapAction == 2 && !_spaceCommittedForPause)
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
                case ActiveMode.Listen:
                    SubmitGuess(c);
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
            ushort packed = MorseEngineService.EncodePacked(target);
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
            RefreshLearnChips();
            Ref(LearnFeedbackText, TextBlock.ForegroundProperty, "Ice");
        }
        private void EvaluateLearningInput(char entered)
        {
            _learnAttempts++;
            char expected = _alphabet[_learnIndex];
            if (char.ToUpper(entered) == expected)
            {
                _learnCorrect++;
                _mastered.Add(expected);
                RefreshLearnChips();
                _learnStreak++;
                LearnFeedbackText.Text = $"Correct: letter '{expected}'";
                Ref(LearnFeedbackText, TextBlock.ForegroundProperty, "Mint");
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
                LearnFeedbackText.Text = $"Not quite: you sent '{entered}'. Target was '{expected}' ({LearnTargetMorse.Text})";
                Ref(LearnFeedbackText, TextBlock.ForegroundProperty, "Alert");
            }
            int pct = (int)((double)_learnCorrect / _learnAttempts * 100);
            LearnScoreText.Text = $"Score: {_learnCorrect} / {_learnAttempts} ({pct}%)  ·  Streak: {_learnStreak}";
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
                    
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(11, 6, 11, 6),
                    Margin = new Thickness(3)
                };
                var tb = new TextBlock
                {
                    Text = ch == ' ' ? "␣" : ch.ToString(),
                    FontSize = 26, FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontWeight = FontWeights.Bold
                };
                Ref(border, Border.BackgroundProperty, i == _sentenceCharIndex ? "Signal" : (i < _sentenceCharIndex ? "Mint" : "Raised"));
                Ref(tb, TextBlock.ForegroundProperty, i == _sentenceCharIndex ? "OnSignal" : (i < _sentenceCharIndex ? "OnMint" : "Muted"));
                border.Child = tb;
                SentenceLettersWrap.Children.Add(border);
            }
            UpdateSentenceTargetHint();
        }
        private void UpdateSentenceTargetHint()
        {
            SentenceFullMorse.Text = string.Join("   ", _targetSentence.Select(MorseOf));
            if (_sentenceCharIndex < _targetSentence.Length)
            {
                char active = _targetSentence[_sentenceCharIndex];
                if (active == ' ')
                {
                    SentenceActiveChar.Text = "[SPACE]";
                    SentenceActiveMorse.Text = $"pause {_unitUs * 6 / 1000.0:0} ms (6 units)";
                }
                else
                {
                    SentenceActiveChar.Text = active.ToString();
                    ushort packed = MorseEngineService.EncodePacked(active);
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
                SentenceActiveMorse.Text = "well sent";
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
                    ushort packed = MorseEngineService.EncodePacked(c);
                    byte count = (byte)(packed & 0xFF);
                    byte pattern = (byte)((packed >> 8) & 0xFF);
                    for (int i = count - 1; i >= 0; i--)
                    {
                        int toneDuration = (((pattern >> i) & 1) == 1) ? unitMs * 3 : unitMs;
                        _audioEngine.StartTone();
                        System.Threading.Thread.Sleep(toneDuration);
                        _audioEngine.StopTone();
                        System.Threading.Thread.Sleep(unitMs);
                    }
                    System.Threading.Thread.Sleep(unitMs * 3);
                }
            });
        }
        protected override void OnClosed(EventArgs e)
        {
            _listenCts?.Cancel();
            timeEndPeriod(1);
            _audioEngine.Dispose();
            base.OnClosed(e);
        }
        #endregion
    }
}