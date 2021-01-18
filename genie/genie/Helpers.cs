using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Linux;

using Process=System.Diagnostics.Process;

using static Tmds.Linux.LibC;

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

        // Run a subprocess and wait for it to exit, returning its return code.
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

        // Run a subprocess and wait for it to exit, erroring out if it does not return success.
        internal static void Chain (string command, string[] args, string onError = "command execution failed;")
        {
            int r = RunAndWait (command, args);

            if (r != 0)
            {
                Console.WriteLine ($"genie: {onError} returned {r}.");
                Environment.Exit (r);
            }
        }

        // Add the "-wsl" suffix to the system hostname, and update the hosts file accordingly.
        internal static void UpdateHostname (bool verbose)
        {
            // Generate new hostname.
            if (verbose)
                Console.WriteLine ("genie: generating new hostname.");

            string externalHost;

            unsafe
            {
                int success;

                byte [] bytes = new byte[64] ;
                fixed (byte * buffer = bytes)
                {
                    success = gethostname (buffer, 64);
                }

                if (success != 0)
                {
                    Console.WriteLine ($"genie: error retrieving hostname: {success}.");
                    Environment.Exit (success);
                }

                externalHost = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            }

            if (verbose)
                Console.WriteLine ($"genie: external hostname is {externalHost}");

            // Make new hostname.
            string internalHost = $"{externalHost.Substring(0, (externalHost.Length <= 60 ? externalHost.Length : 60))}-wsl";

            File.WriteAllLines ("/run/hostname-wsl", new string[] {
                internalHost
            });

            unsafe
            {
                var bytes = Encoding.UTF8.GetBytes("/run/hostname-wsl");
                fixed (byte* buffer = bytes)
                {
                    chmod (buffer, Convert.ToUInt16 ("644", 8));
                }
            }

            // Hosts file: check for old host name; if there, remove it.
            if (verbose)
                Console.WriteLine ("genie: updating hosts file.");

            try
            {
                var hosts = File.ReadAllLines ("/etc/hosts");
                var newHosts = new List<string> (hosts.Length);

                newHosts.Add ($"127.0.0.1 localhost {internalHost}");

                foreach (string s in hosts)
                {
                    if (!(
                        (s.Contains (externalHost) || s.Contains (internalHost))
                         && (s.Contains("127.0.0.1"))
                        ))
                    {
                        newHosts.Add (s);
                    }
                }

                File.WriteAllLines ("/etc/hosts", newHosts.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"genie: error updating host file: {ex.Message}");
                Environment.Exit (130);
            }

            // Set the new hostname.
            if (verbose)
                Console.WriteLine ("genie: setting new hostname.");

            Helpers.Chain ("mount",
                new string[] {"--bind", "/run/hostname-wsl", "/etc/hostname"},
                "initializing bottle failed; bind mounting hostname");
        }
    }
}
