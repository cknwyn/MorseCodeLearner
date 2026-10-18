using System;
using MorseApp.Native;
namespace MorseApp.Services
{
    public class MorseEngineService
    {
        public static PackedMorseChar EncodeChar(char c)
        {
            ushort packed = NativeMethods.Morse_EncodeChar((uint)c);
            return new PackedMorseChar
            {
                Count = (byte)(packed & 0xFF),
                Pattern = (byte)((packed >> 8) & 0xFF)
            };
        }
        public static char DecodeChar(byte count, byte pattern)
        {
            return (char)NativeMethods.Morse_DecodeChar(count, pattern);
        }
        public static MorseToken QuantizePress(ulong durationUs, int wpm)
        {
            ulong unitUs = (ulong)(1200000 / Math.Max(5, wpm));
            return NativeMethods.Morse_QuantizePress(durationUs, unitUs);
        }
        public static GapAction EvaluateGap(ulong gapUs, int wpm)
        {
            ulong unitUs = (ulong)(1200000 / Math.Max(5, wpm));
            return NativeMethods.Morse_EvaluateGap(gapUs, unitUs);
        }
    }
}
