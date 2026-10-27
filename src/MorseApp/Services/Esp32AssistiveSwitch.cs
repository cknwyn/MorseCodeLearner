using System;
using System.IO.Ports;
using System.Threading;
using System.Windows;

namespace MorseApp.Services
{
    public sealed class Esp32AssistiveSwitch : IDisposable
    {
        public event EventHandler<bool>? PressChanged;

        private readonly SerialPort _port;
        private readonly Thread _readerThread;
        private volatile bool _disposed;
        private bool _lastState;

        public Esp32AssistiveSwitch(string portName, int baudRate = 115200)
        {
            _port = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            _readerThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "Esp32AssistiveSwitchReader"
            };
        }

        public void Open()
        {
            if (_port.IsOpen)
                return;

            _port.Open();
            _readerThread.Start();
        }

        public void Close()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _port.Close();
            }
            catch
            {
            }

            try
            {
                _readerThread.Join(250);
            }
            catch
            {
            }
        }

        private void ReadLoop()
        {
            try
            {
                while (!_disposed && _port.IsOpen)
                {
                    string? line = null;

                    try
                    {
                        line = _port.ReadLine();
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string value = line.Trim();
                    bool pressed = value.Equals("PRESSED", StringComparison.OrdinalIgnoreCase);

                    if (pressed != _lastState)
                    {
                        _lastState = pressed;
                        PressChanged?.Invoke(this, pressed);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ESP32 serial read error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Close();
            _port.Dispose();
        }
    }
}
