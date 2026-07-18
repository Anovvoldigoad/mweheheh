using NodeNetwork;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Diagnostics;
using Xceed.Wpf.AvalonDock.Themes;

namespace NSC_ModManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    public class RelayCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }


    public partial class App : Application
    {

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        public App()
        {
            // Winlator (Wine di atas Android) sering punya driver GPU translation
            // (Turnip/DXVK/D3D9-on-D3D11) yang tidak stabil untuk WPF hardware
            // compositing. Memaksa software rendering menghilangkan sumber crash/
            // layar-hitam paling umum, dengan trade-off performa render sedikit
            // lebih berat - untuk UI form-based seperti ini dampaknya kecil.
            // Bisa dimatikan dengan set environment variable NSC_MM_FORCE_HWRENDER=1
            // (misalnya kalau dijalankan native di Windows asli dan mau full GPU).
            if (Environment.GetEnvironmentVariable("NSC_MM_FORCE_HWRENDER") != "1")
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            // Lapis pengaman kedua (selain Setter di WinlatorStyle.xaml) untuk
            // masalah UriFormatException di font-cache pipeline "Ideal"/DirectWrite -
            // paksa semua Window baru pakai pipeline "Display" (GDI-compatible) sejak
            // sebelum window manapun sempat dibuat/ditampilkan.
            TextOptions.TextFormattingModeProperty.OverrideMetadata(
                typeof(Window), new FrameworkPropertyMetadata(TextFormattingMode.Display));

            InitializeComponent();

            // Global exception handlers - tanpa ini, unhandled exception di Wine/
            // Winlator bisa bikin proses mati senyap tanpa jejak sama sekali.
            // Sekarang minimal tercatat ke crash_log.txt di folder aplikasi.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogAndShowCrash(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
            this.DispatcherUnhandledException += (s, e) =>
            {
                LogAndShowCrash(e.Exception, "DispatcherUnhandledException");
                e.Handled = true; // jangan biarkan WPF ikut menjatuhkan proses kalau masih bisa diselamatkan
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogAndShowCrash(e.Exception, "UnobservedTaskException");
                e.SetObserved();
            };
        }

        private static readonly HashSet<string> _shownCrashSignatures = new HashSet<string>();
        private static readonly object _crashLock = new object();

        private static void LogAndShowCrash(Exception ex, string source)
        {
            string signature = source + "|" + (ex?.GetType().FullName ?? "?") + "|" + (ex?.TargetSite?.Name ?? "?");
            bool alreadyShown;
            lock (_crashLock)
            {
                alreadyShown = !_shownCrashSignatures.Add(signature);
            }

            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                // Error yang sama berulang cuma dicatat ringkas setelah kemunculan pertama,
                // supaya file log tidak membengkak jadi ribuan baris identik seperti sebelumnya.
                string entry = alreadyShown
                    ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source} (repeat, same as before): {ex?.GetType().Name}: {ex?.Message}\n"
                    : $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
                File.AppendAllText(logPath, entry);
            }
            catch { /* kalau nulis log pun gagal, jangan sampai handler ini ikut crash */ }

            if (alreadyShown) return; // sudah pernah ditampilkan - jangan spam popup lagi

            try
            {
                System.Windows.MessageBox.Show(
                    "Terjadi error tak terduga:\n" + (ex?.Message ?? "(unknown)") +
                    "\n\nDetail tersimpan di crash_log.txt\n\n(Pesan ini hanya muncul sekali per jenis error; error yang sama berikutnya akan langsung dicatat ke log tanpa popup.)",
                    "NSC-ModManager - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }

        private static bool IsDllPresent(string dllName)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = LoadLibrary(dllName);
                return h != IntPtr.Zero;
            } finally
            {
                if (h != IntPtr.Zero)
                {
                    FreeLibrary(h);
                }
            }
        }

        /// <summary>
        /// Попытаться запустить локальный инсталлятор vcredist_x86.exe с повышением прав.
        /// Возвращает true, если инсталлятор был запущен и завершился с кодом 0.
        /// </summary>
        private static bool TryRunBundledInstaller(string installerFileName, int timeoutMilliseconds = 120000)
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, installerFileName);
            if (!File.Exists(exePath)) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "/q /norestart",
                    UseShellExecute = true,
                    Verb = "runas", // запрос UAC, если требуется
                    CreateNoWindow = true
                };

                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    bool exited = p.WaitForExit(timeoutMilliseconds);
                    if (!exited)
                    {
                        try { p.Kill(); } catch { }
                        return false;
                    }
                    return p.ExitCode == 0;
                }
            } catch
            {
                return false;
            }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            const string requiredDll = "MSVCP100.dll";
            const string installerName = "vcredist_x86.exe";

            // Проверяем наличие DLL
            if (!IsDllPresent(requiredDll))
            {
                // Попробовать тихо запустить bundled installer (если он рядом)
                bool installerRun = TryRunBundledInstaller(installerName);

                // После установки проверяем снова
                if (!IsDllPresent(requiredDll))
                {
                    string msg;
                    if (installerRun)
                    {
                        msg = "Microsoft Visual C++ 2010 Redistributable was run, but the required library MSVCP100.dll was not found.\n\nInstall the Redistributable manually or place a correct vcredist_x86.exe next to the application.";
                    } else
                    {
                        msg = "Microsoft Visual C++ 2010 Redistributable (x86) is required. Place \"vcredist_x86.exe\" in the application folder or install it manually and restart the program.";
                    }


                    System.Windows.MessageBox.Show(msg, "Missing drivers", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Завершаем приложение
                    Current?.Shutdown();
                    return;
                }
            }

            // Всё в порядке — продолжаем инициализацию
            NNViewRegistrar.RegisterSplat();
        }
    }
}