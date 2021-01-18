using System;
using System.Linq;

using Linux;

using Process=System.Diagnostics.Process;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    internal static class Helpers
    {
        // Get the pid of the earliest running root systemd, or 0 if none is running.
        internal static int GetSystemdPid ()
        {
            var processInfo = ProcessManager.GetProcessInfos (_ => _.ProcessName == "systemd")
                .Where (_ => _.Ruid == 0)
                .OrderBy (_ => _.StartTime)
                .FirstOrDefault();

            return processInfo != null ? processInfo.ProcessId : 0;
        }

        internal static int RunAndWait (string command, string[] args)
        {
            try
            {
                var p = Process.Start(command, args);
                p.WaitForExit();

                return p.ExitCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"genie: error executing command '{command} {string.Join(' ', args)}':\r\n{ex.Message}");
                Environment.Exit (127);

                // never reached
                return 127;
            }
        }

        internal static void Chain (string command, string[] args, string onError = "command execution failed;")
        {
            int r = RunAndWait (command, args);

            if (r != 0)
            {
                Console.WriteLine ($"genie: {onError} returned {r}.");
                Environment.Exit (r);
            }
        }
    }
}
