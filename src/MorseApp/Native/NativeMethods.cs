using System;
using System.Runtime.InteropServices;
namespace MorseApp.Native
{
    public enum MorseToken : uint
    {
        None = 0,
        Dit = 1,
        Dah = 2,
        Invalid = 3
    }
    public enum GapAction : uint
    {
        Continue = 0,
        CommitCharacter = 1,
        CommitSpace = 2
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PackedMorseChar
    {
        public byte Count;
        public byte Pattern;
        public bool IsValid => Count > 0;
        public string ToDitDahString()
        {
            if (Count == 0) return string.Empty;
            char[] chars = new char[Count];
            for (int i = 0; i < Count; i++)
            {
                chars[i] = ((Pattern >> (Count - 1 - i)) & 1) == 1 ? '-' : '.';
            }
            return new string(chars);
        }
    }
    public static class NativeMethods
    {
        private const string DllName = "MorseCore.dll";
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ushort Morse_EncodeChar(uint asciiChar);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Morse_EncodeString(
            [MarshalAs(UnmanagedType.LPStr)] string inputStr,
            [Out] ushort[] outputBuffer,
            int maxWords);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern byte Morse_DecodeChar(byte count, byte pattern);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern MorseToken Morse_QuantizePress(ulong durationUs, ulong unitUs);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern GapAction Morse_EvaluateGap(ulong gapUs, ulong unitUs);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong Morse_GetBenchmarkCycles();
    }
}
