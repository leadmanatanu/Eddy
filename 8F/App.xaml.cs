using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace _8F
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                try { File.WriteAllText(@"D:\New folder\Eddy\8F\crash.log", $"AppDomain Unhandled: {e.ExceptionObject}"); } catch { }
            };
            DispatcherUnhandledException += (s, e) => {
                try { File.WriteAllText(@"D:\New folder\Eddy\8F\crash.log", $"Dispatcher Unhandled: {e.Exception}"); } catch { }
            };
            TaskScheduler.UnobservedTaskException += (s, e) => {
                try { File.WriteAllText(@"D:\New folder\Eddy\8F\crash.log", $"TaskScheduler Unhandled: {e.Exception}"); } catch { }
            };
        }
    }
}
