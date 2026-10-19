using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
namespace MorseApp
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // Automatic DLL Resolver: Finds MorseCore.dll anywhere in the solution
            NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName.Equals("MorseCore.dll", StringComparison.OrdinalIgnoreCase))
                {
                    // 1. Check local directory next to MorseApp.exe
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MorseCore.dll");
                    if (File.Exists(localPath))
                    {
                        return NativeLibrary.Load(localPath);
                    }
                    // 2. Search upwards across the entire project/solution tree
                    DirectoryInfo? current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    while (current != null)
                    {
                        try
                        {
                            var matches = current.GetFiles("MorseCore.dll", SearchOption.AllDirectories);
                            if (matches.Length > 0)
                            {
                                return NativeLibrary.Load(matches[0].FullName);
                            }
                        }
                        catch { }
                        current = current.Parent;
                    }
                }
                return IntPtr.Zero;
            });
            try
            {
                var app = new Application();
                var mainWindow = new MainWindow();
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
            }
        }
    }
}