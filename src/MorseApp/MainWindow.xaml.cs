using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace MorseApp
{
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
        private readonly Stopwatch _pressTimer = new();
        private readonly Stopwatch _idleTimer = new();
        private readonly DispatcherTimer _gapTimer = new();
        private bool _isKeyPressed = false;
        private byte _elementCount = 0;
        private byte _patternMask = 0;
        private const int Wpm = 15;
        private readonly ulong _unitUs = 1200000 / Wpm; // 80 ms base unit
        private static readonly SolidColorBrush ActiveYellow = new(Color.FromRgb(0xFF, 0xD6, 0x00));
        private static readonly SolidColorBrush IdleDark = new(Color.FromRgb(0x2A, 0x2A, 0x2A));
        private static readonly SolidColorBrush TextDim = new(Color.FromRgb(0xA0, 0xA0, 0xA0));
        public MainWindow()
        {
            InitializeComponent();
            _gapTimer.Interval = TimeSpan.FromMilliseconds(25);
            _gapTimer.Tick += OnGapTick;
            _gapTimer.Start();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space && !e.IsRepeat && !_isKeyPressed)
            {
                _isKeyPressed = true;
                _idleTimer.Stop();
                _pressTimer.Restart();
                SignalBulb.Fill = ActiveYellow;
                SignalGlow.Radius = 24;
                SignalStatusText.Text = "KEY DOWN";
                SignalStatusText.Foreground = Brushes.Black;
                Task.Run(() => Beep(700, 1000));
            }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.Key == Key.Space && _isKeyPressed)
            {
                _isKeyPressed = false;
                _pressTimer.Stop();
                SignalBulb.Fill = IdleDark;
                SignalGlow.Radius = 0;
                SignalStatusText.Text = "KEY UP";
                SignalStatusText.Foreground = TextDim;
                ulong durationUs = (ulong)(_pressTimer.Elapsed.TotalMilliseconds * 1000);
                DurationMetric.Text = $"{_pressTimer.ElapsedMilliseconds} ms";
                // Invoke MASM single-switch quantizer
                 uint token = Morse_QuantizePress(durationUs, _unitUs);
                if (token == 1) // Dit
                {
                    TokenMetric.Text = "DIT (•)";
                    _patternMask |= (byte)(0 << _elementCount);
                    _elementCount++;
                    CurrentTokenBuffer.Text += "• ";
                }
                else if (token == 2) // Dah
                {
                    TokenMetric.Text = "DAH (─)";
                    _patternMask |= (byte)(1 << _elementCount);
                    _elementCount++;
                    CurrentTokenBuffer.Text += "─ ";
                }
                else
                {
                    TokenMetric.Text = "IGNORED";
                }
                _idleTimer.Restart();
            }
            base.OnKeyUp(e);
        }
        private void OnGapTick(object? sender, EventArgs e)
        {
            if (_isKeyPressed || _elementCount == 0) return;
            ulong gapUs = (ulong)(_idleTimer.Elapsed.TotalMilliseconds * 1000);
            uint action = Morse_EvaluateGap(gapUs, _unitUs);
            if (action == 1 || action == 2) // Commit Char or Space
            {
                // Invoke MASM Dichotomic Tree Decoder
                byte decodedAscii = Morse_DecodeChar(_elementCount, _patternMask);
                if (decodedAscii != 0)
                {
                    DecodedStreamText.Text += (char)decodedAscii;
                }
                if (action == 2)
                {
                    DecodedStreamText.Text += " ";
                }
                _elementCount = 0;
                _patternMask = 0;
                CurrentTokenBuffer.Text = string.Empty;
                _idleTimer.Reset();
            }
        }
    }
}