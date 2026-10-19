using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace MorseApp.Audio
{
    public class PcmAudioEngine : IDisposable
    {
        private const int WAVE_FORMAT_PCM = 1;
        private const int WHDR_PREPARED = 0x00000002;
        private const int WHDR_BEGINLOOP = 0x00000004;
        private const int WHDR_ENDLOOP = 0x00000008;

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutOpen(out IntPtr hWaveOut, uint uDeviceID, ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint fdwOpen);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, uint uSize);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutWrite(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, uint uSize);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutReset(IntPtr hWaveOut);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WAVEHDR lpWaveHdr, uint uSize);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern int waveOutClose(IntPtr hWaveOut);

        private IntPtr _hWaveOut = IntPtr.Zero;
        private IntPtr _pBuffer = IntPtr.Zero;
        private WAVEHDR _waveHdr;
        private bool _isPrepared = false;
        private bool _isPlaying = false;
        private readonly object _lock = new();

        private const int SampleRate = 44100;
        private int _currentFrequency = 700;

        public int Frequency
        {
            get => _currentFrequency;
            set
            {
                if (value != _currentFrequency && value >= 300 && value <= 1500)
                {
                    lock (_lock)
                    {
                        bool wasPlaying = _isPlaying;
                        if (wasPlaying) StopTone();
                        _currentFrequency = value;
                        RebuildBuffer();
                        if (wasPlaying) StartTone();
                    }
                }
            }
        }

        public PcmAudioEngine(int initialFrequency = 700)
        {
            _currentFrequency = initialFrequency;
            InitializeDevice();
            RebuildBuffer();
        }

        private void InitializeDevice()
        {
            var format = new WAVEFORMATEX
            {
                wFormatTag = WAVE_FORMAT_PCM,
                nChannels = 1,
                nSamplesPerSec = (uint)SampleRate,
                nAvgBytesPerSec = (uint)(SampleRate * 2),
                nBlockAlign = 2,
                wBitsPerSample = 16,
                cbSize = 0
            };

            int res = waveOutOpen(out _hWaveOut, 0xFFFFFFFF, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            if (res != 0)
            {
                _hWaveOut = IntPtr.Zero;
            }
        }

        private void RebuildBuffer()
        {
            if (_hWaveOut == IntPtr.Zero) return;

            // Clean up existing buffer
            if (_isPrepared)
            {
                waveOutReset(_hWaveOut);
                waveOutUnprepareHeader(_hWaveOut, ref _waveHdr, (uint)Marshal.SizeOf<WAVEHDR>());
                _isPrepared = false;
            }

            if (_pBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pBuffer);
                _pBuffer = IntPtr.Zero;
            }

            // Find smooth looping cycle count
            int gcd = GreatestCommonDivisor(SampleRate, _currentFrequency);
            int baseCycleSamples = SampleRate / gcd;
            int repeatCount = Math.Max(1, (int)Math.Ceiling(4410.0 / baseCycleSamples)); // ~100ms minimum loop
            int totalSamples = baseCycleSamples * repeatCount;

            short[] samples = new short[totalSamples];
            const double amplitude = 28000; // Strong, clear, non-clipping volume
            for (int i = 0; i < totalSamples; i++)
            {
                double angle = 2.0 * Math.PI * i * _currentFrequency / SampleRate;
                samples[i] = (short)(amplitude * Math.Sin(angle));
            }

            int byteCount = totalSamples * sizeof(short);
            _pBuffer = Marshal.AllocHGlobal(byteCount);
            Marshal.Copy(samples, 0, _pBuffer, totalSamples);

            _waveHdr = new WAVEHDR
            {
                lpData = _pBuffer,
                dwBufferLength = (uint)byteCount,
                dwBytesRecorded = 0,
                dwUser = IntPtr.Zero,
                dwFlags = WHDR_BEGINLOOP | WHDR_ENDLOOP,
                dwLoops = 0xFFFFFFFF
            };

            int prepRes = waveOutPrepareHeader(_hWaveOut, ref _waveHdr, (uint)Marshal.SizeOf<WAVEHDR>());
            _isPrepared = (prepRes == 0);
        }

        public void StartTone()
        {
            lock (_lock)
            {
                if (_hWaveOut == IntPtr.Zero || !_isPrepared || _isPlaying) return;
                waveOutWrite(_hWaveOut, ref _waveHdr, (uint)Marshal.SizeOf<WAVEHDR>());
                _isPlaying = true;
            }
        }

        public void StopTone()
        {
            lock (_lock)
            {
                if (_hWaveOut == IntPtr.Zero || !_isPlaying) return;
                waveOutReset(_hWaveOut);
                _isPlaying = false;
            }
        }

        public void PlayPulse(int durationMs)
        {
            Task.Run(() =>
            {
                StartTone();
                Thread.Sleep(durationMs);
                StopTone();
            });
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isPlaying)
                {
                    StopTone();
                }

                if (_isPrepared && _hWaveOut != IntPtr.Zero)
                {
                    waveOutUnprepareHeader(_hWaveOut, ref _waveHdr, (uint)Marshal.SizeOf<WAVEHDR>());
                    _isPrepared = false;
                }

                if (_pBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_pBuffer);
                    _pBuffer = IntPtr.Zero;
                }

                if (_hWaveOut != IntPtr.Zero)
                {
                    waveOutClose(_hWaveOut);
                    _hWaveOut = IntPtr.Zero;
                }
            }
        }
    }
}
