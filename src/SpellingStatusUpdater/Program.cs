using System;
using System.Diagnostics;
using System.IO;

namespace SIL.SpellingStatusUpdater
{
    /// <summary>
    /// Started by the SpellFixer Paratext plugin as Paratext shuts down:
    ///   SpellingStatusUpdater.exe --wait-pid {Paratext's process id} --projects-dir "{e.g. C:\My Paratext 9 Projects}"
    /// Waits for Paratext to exit and then merges the pending spelling fixes into each project's SpellingStatus.xml
    /// </summary>
    internal static class Program
    {
        private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(30);

        [STAThread]
        private static int Main(string[] args)
        {
            // never crash (that would put up a Windows "stopped working" dialog after Paratext has closed)
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "SpellingStatusUpdater.log"),
                                       $"{DateTime.Now:s} ERROR: {ex}{Environment.NewLine}");
                }
                catch
                {
                    // nowhere else to report it
                }
                return 3;
            }
        }

        private static int Run(string[] args)
        {
            int? waitPid = null;
            string projectsDirectory = null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--wait-pid" && int.TryParse(args[i + 1], out int pid))
                    waitPid = pid;
                else if (args[i] == "--projects-dir")
                    projectsDirectory = args[i + 1];
            }

            if (String.IsNullOrEmpty(projectsDirectory) || !Directory.Exists(projectsDirectory))
                return 1;

            if (waitPid.HasValue && !WaitForExit(waitPid.Value))
                return 2;   // Paratext is still running; leave everything pending for next time

            SpellingStatusUpdateRunner.ProcessProjectsDirectory(projectsDirectory, DateTime.Now);
            return 0;
        }

        private static bool WaitForExit(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                    return process.WaitForExit((int)MaxWait.TotalMilliseconds);
            }
            catch (ArgumentException)
            {
                return true;    // it has already exited
            }
        }
    }
}
