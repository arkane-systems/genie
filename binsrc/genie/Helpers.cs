using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        internal static string WslDistroName => System.Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") ;

        // Get the pid of the earliest running root systemd, or 0 if none is running.
        internal static int GetSystemdPid ()
        {
            var processInfo = ProcessManager.GetProcessInfos (_ => _.ProcessName == "systemd")
                .Where (_ => _.Ruid == 0)
                .OrderBy (_ => _.StartTime)
                .FirstOrDefault();

            return processInfo != null ? processInfo.ProcessId : 0;
        }

        #region Subprocesses

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

        // Run a subprocess and wait for it to exit, returning its output.
        internal static string RunAndWaitForOutput (string command, string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo (command, string.Join(" ", args));
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;

                var p = Process.Start(psi);

                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();

                return output;
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"genie: error executing command '{command} {string.Join(' ', args)}':\r\n{ex.Message}");
                Environment.Exit (127);

                // never reached
                return "never-reached";
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

        #endregion Subprocesses

        #region Systemd state

        internal static bool IsSystemdRunning (int systemdPid)
        {
            string[] args;

            if (systemdPid == 1)
            {
                args = new string[] {"-c", $"systemctl is-system-running -q 2> /dev/null"};
            }
            else
            {
                args = new string[] {"-c", $"nsenter -t {systemdPid} -m -p systemctl is-system-running -q 2> /dev/null"};
            }

            var retval = Helpers.RunAndWait ("sh", args);

            return (retval == 0);
        }

        #endregion Systemd state

        #region Hostname management

        // Add the "-wsl" suffix to the system hostname, and update the hosts file accordingly.
        internal static void UpdateHostname (string HostnameSuffix, bool verbose)
        {
            // Generate new hostname.
            if (verbose)
                Console.WriteLine ("genie: generating new hostname.");

            string externalHost = HostHelpers.Hostname;

            if (verbose)
                Console.WriteLine ($"genie: external hostname is {externalHost}");

            // Make new hostname.
            string internalHost = $"{externalHost.Substring(0, (externalHost.Length <= 60 ? externalHost.Length : 60))}{HostnameSuffix}";

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

            if (!MountHelpers.BindMount ("/run/hostname-wsl", "/etc/hostname"))
            {
                Console.WriteLine ("genie: initializing bottle failed; bind mounting hostname");
                Environment.Exit(EPERM);
            }
        }

        // Drop the in-bottle hostname.
        internal static void DropHostname (bool verbose)
        {
            // Drop the in-bottle hostname.
            if (verbose)
                Console.WriteLine ("genie: dropping in-bottle hostname");

            if (!MountHelpers.UnMount ("/etc/hostname"))
            {
                Console.WriteLine ("genie: shutdown failed; unmounting hostname");
                Environment.Exit(EPERM);
            }

            File.Delete ("/run/hostname-wsl");

            var hostname = File.ReadAllLines ("/etc/hostname")[0].TrimEnd();

            HostHelpers.Hostname = hostname;
        }

        #endregion Hostname management

        #region Resolved symlink

        internal static void CreateResolvSymlink (bool verbose)
        {
            // We cannot check if the target (/run/systemd/resolve/stub-resolv.conf) exists,
            // since it will not be created until after systemd-resolved starts up. So we're
            // going to have to live with that uncertainty.

            // Check if source (/etc/resolv.conf) exists.
            if (File.Exists ("/etc/resolv.conf"))
            {
                // If so, move it to the backup file (/etc/resolv.conf.wsl).
                if (verbose)
                    Console.WriteLine ("genie: backing up old resolv.conf");

                File.Move ("/etc/resolv.conf", "/etc/resolv.conf.wsl", true);
            }

            // Create symbolic link from /etc/resolv.conf to /run/systemd/resolve/stub-resolv.conf.
            if (verbose)
                Console.WriteLine ("genie: creating resolv symlink");

            try
            {
                FsHelpers.CreateSymbolicLink ("/etc/resolv.conf", "/run/systemd/resolve/stub-resolv.conf");
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"genie: error creating resolv symlink: {ex.Message}");
            }
        }

        internal static void RemoveResolvSymlink (bool verbose)
        {
            // Check if /etc/resolv.conf exists, and if so, if it is a symlink to the target
            // (/run/systemd/resolve/stub-resolv.conf).
            if (!File.Exists ("/etc/resolv.conf"))
            {
                Console.WriteLine ("genie: resolv symlink does not exist.");
                return;
            }

            if (!(File.GetAttributes ("/etc/resolv.conf").HasFlag (FileAttributes.ReparsePoint)))
            {
                Console.WriteLine ("genie: resolv symlink is not a symlink.");
                return;
            }

            // If so, delete the symlink.
            File.Delete ("/etc/resolv.conf");

            // Check if /etc/resolv.conf.wsl exists.
            if (File.Exists ("/etc/resolv.conf.wsl"))
                // If so, move it to /etc/resolv.conf.
                File.Move ("/etc/resolv.conf.wsl", "/etc/resolv.conf", true);
            else
                // If not, warn the user.
                Console.WriteLine ("genie: could not find /etc/resolv.conf backup. Please manually restore /etc/resolv.conf.");
        }

        #endregion Resolved symlink

        #region User identities

        internal static string GetLoginName ()
        {
            return Environment.GetEnvironmentVariable("LOGNAME");

            // Try this again later?
            // Error code 6? What is code 6?

            // unsafe
            // {
            //     byte[] bytes = new byte[64];

            //     fixed (byte* buffer = bytes)
            //     {
            //         int gls = getlogin_r(buffer, 64);

            //         if (gls == 0)
            //             return Encoding.UTF8.GetString (bytes);
            //         else
            //         {
            //             Console.WriteLine ($"genie: error retrieving login name gls={gls}, using fallback");
            //             return Environment.GetEnvironmentVariable ("LOGNAME");
            //             // throw new InvalidOperationException ("genie: Could not retrieve real user name.");
            //         }
            //     }
            // }
        }

        #endregion User identities
    }
}
