using System;
using MorseApp.Native;
namespace MorseApp.Services
{
    /// <summary>
    /// The single managed gateway to MorseCore.dll. All Morse semantics (encoding, decoding,
    /// press classification and gap evaluation) are computed by the MASM routines; this class only
    /// converts units and unpacks results. No timing thresholds live in managed code.
    /// Layering: MainWindow -> MorseEngineService -> NativeMethods (P/Invoke) -> MorseCore.dll.
    /// </summary>
    public static class MorseEngineService
    {
        /// <summary>Morse unit T in microseconds for a given speed: T = 1,200,000 / WPM.</summary>
        public static ulong UnitUs(int wpm) => (ulong)(1200000 / Math.Max(5, wpm));

        // ---- encoding / decoding ------------------------------------------------------------

        /// <summary>Raw bit-packed word from the native encoder (AL = element count, AH = pattern; 0 = unmapped).</summary>
        public static ushort EncodePacked(char c) => NativeMethods.Morse_EncodeChar(c);

        public static PackedMorseChar EncodeChar(char c)
        {
            ushort packed = EncodePacked(c);
            return new PackedMorseChar
            {
                Count = (byte)(packed & 0xFF),
                Pattern = (byte)((packed >> 8) & 0xFF)
            };
        }

        /// <summary>Encodes up to <paramref name="maxWords"/> characters of an ASCII string with Morse_EncodeString.</summary>
        public static ushort[] EncodeString(string text, int maxWords)
        {
            var buffer = new ushort[Math.Max(0, maxWords)];
            int written = NativeMethods.Morse_EncodeString(text, buffer, buffer.Length);
            Array.Resize(ref buffer, written);
            return buffer;
        }

        public static byte DecodeRaw(byte count, byte pattern) => NativeMethods.Morse_DecodeChar(count, pattern);

        public static char DecodeChar(byte count, byte pattern) => (char)DecodeRaw(count, pattern);

        // ---- timing classification (native) -------------------------------------------------

        public static MorseToken QuantizePress(ulong durationUs, ulong unitUs) => NativeMethods.Morse_QuantizePress(durationUs, unitUs);
        public static MorseToken QuantizePress(ulong durationUs, int wpm) => QuantizePress(durationUs, UnitUs(wpm));

        public static GapAction EvaluateGap(ulong gapUs, ulong unitUs) => NativeMethods.Morse_EvaluateGap(gapUs, unitUs);
        public static GapAction EvaluateGap(ulong gapUs, int wpm) => EvaluateGap(gapUs, UnitUs(wpm));

        /// <summary>Token (1 dit, 2 dah, 0 none, 3 invalid) as a plain integer for UI code.</summary>
        public static uint QuantizePressUs(ulong durationUs, ulong unitUs) => (uint)QuantizePress(durationUs, unitUs);

        /// <summary>Gap action (0 continue, 1 commit character, 2 commit space) as a plain integer for UI code.</summary>
        public static uint EvaluateGapUs(ulong gapUs, ulong unitUs) => (uint)EvaluateGap(gapUs, unitUs);

        // ---- telemetry ----------------------------------------------------------------------

        /// <summary>RDTSC cycle counter read by the native core.</summary>
        public static ulong GetCycles() => NativeMethods.Morse_GetBenchmarkCycles();
    }
}
