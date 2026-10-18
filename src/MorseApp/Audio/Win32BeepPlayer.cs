using System.Runtime.InteropServices;
using System.Threading.Tasks;
namespace MorseApp.Audio
{
    public class Win32BeepPlayer
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Beep(uint dwFreq, uint dwDuration);
        public void PlayTone(int frequencyHz, int durationMs)
        {
            Task.Run(() => Beep((uint)frequencyHz, (uint)durationMs));
        }
    }
}
