using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;


namespace Ephemera.Win32.Test
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] _)
        {
            // Info about the source window.
            IntPtr hwnd = WindowManagement.ForegroundWindow;
            var info = WindowManagement.GetAppWindowInfo(hwnd);
            var process = Process.GetProcessById(info.Pid);
            var procName = process.ProcessName;
            var appPath = process.MainModule!.FileName;
            var appName = Path.GetFileName(appPath);

            Console.WriteLine(info.Title);

        }
    }
}
