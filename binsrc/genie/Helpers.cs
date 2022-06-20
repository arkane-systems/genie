using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Linux;
using static System.Console;
using static Tmds.Linux.LibC;
using Process = System.Diagnostics.Process;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    internal static class Helpers
    {
        internal static string WslDistroName => Environment.GetEnvironmentVariable("WSL_DISTRO_NAME");

        /// <summary>
        /// Get the pid of the earliest running root systemd, or 0 if none is running.
        /// </summary>
        internal static int GetSystemdPid() => ProcessManager.GetProcessInfos(_ => _.ProcessName == "systemd")
            .Where(_ => _.Ruid == 0)
            .MinBy(_ => _.StartTime)?.ProcessId ?? 0;

        #region Subprocesses

        /// <summary>
        /// Run a sub process and wait for it to exit, returning its return code.
        /// </summary>
        internal static int RunAndWait(string command, string[] args)
        {
            try
            {
                var p = Process.Start(command, args);
                p.WaitForExit();

                return p.ExitCode;
            }
            catch (Exception ex)
            {
                WriteLine($"genie: error executing command '{command} {string.Join(' ', args)}':\r\n{ex.Message}");
                Environment.Exit(127);

                // Never reached.
                return 127;
            }
        }

        /// <summary>
        /// Run a sub process and wait for it to exit, returning its output.
        /// </summary>
        internal static string RunAndWaitForOutput(string command, string[] args)
        {
            try
            {
                var psi = new ProcessStartInfo(command, string.Join(" ", args))
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };

                var p = Process.Start(psi);
                var output = p?.StandardOutput.ReadToEnd();

                p?.WaitForExit();

                return output;
            }
            catch (Exception ex)
            {
                WriteLine($"genie: error executing command '{command} {string.Join(' ', args)}':\r\n{ex.Message}");
                Environment.Exit(127);

                // Never reached.
                return "never-reached";
            }
        }

        /// <summary>
        /// Run a sub process and wait for it to exit, erroring out if it does not return success.
        /// </summary>
        internal static void Chain(string command, string[] args, string onError = "command execution failed;")
        {
            var r = RunAndWait(command, args);
            if (r != 0)
            {
                WriteLine($"genie: {onError} returned {r}.");
                Environment.Exit(r);
            }
        }

        /// <summary>
        /// Run a sub process inside the bottle, and wait for it to exit, erroring out if it does not return success.
        /// </summary>
        internal static void ChainInside(int nsPid, IEnumerable<string> commandArgs, string onError = "command execution failed;")
        {
            var nsenterArgs = new[] { "-t", nsPid.ToString(), "-m", "-p" };
            var realArgs = nsenterArgs.Concat(commandArgs).ToArray();

            using var r = new RootPrivilege();
            Chain("nsenter", realArgs, onError);
        }

        #endregion Subprocesses

        #region Systemd state

        internal static bool IsSystemdRunning(int systemdPid)
        {
            var args = systemdPid == 1
                ? new[] {"-c", $"systemctl is-system-running -q 2> /dev/null"}
                : new[] {"-c", $"nsenter -t {systemdPid} -m -p systemctl is-system-running -q 2> /dev/null"};

            using var r = new RootPrivilege();
            var retVal = RunAndWait("sh", args);

            //Console.WriteLine($"debug is-system-running = {retVal}");

            return retVal == 0;
        }

        #endregion Systemd state

        #region Hostname management

        /// <summary>
        /// Add the "-wsl" suffix to the system hostname, and update the hosts file accordingly.
        /// </summary>
        internal static void UpdateHostname(string hostnameSuffix, bool verbose)
        {
            // Generate new hostname.
            if (verbose)
                WriteLine("genie: generating new hostname.");

            var externalHost = HostHelpers.Hostname;

            if (verbose)
                WriteLine($"genie: external hostname is {externalHost}");

            // Make new hostname.
            var internalHost = $"{externalHost[..(externalHost.Length <= 60 ? externalHost.Length : 60)]}{hostnameSuffix}";

            File.WriteAllLines("/run/hostname-wsl", new[] { internalHost });

            unsafe
            {
                var bytes = Encoding.UTF8.GetBytes("/run/hostname-wsl");
                fixed(byte* buffer = bytes)
                {
                    _ = chmod(buffer, Convert.ToUInt16("644", 8));
                }
            }

            // Hosts file: check for old host name; if there, remove it.
            if (verbose)
                WriteLine("genie: updating hosts file.");

            try
            {
                var hosts = File.ReadAllLines("/etc/hosts");
                var newHosts = new List<string>(hosts.Length) { $"127.0.0.1 localhost {internalHost}" };
                newHosts.AddRange(hosts.Where(s => !((s.Contains(externalHost) || s.Contains(internalHost)) && s.Contains("127.0.0.1"))));

                File.WriteAllLines("/etc/hosts", newHosts.ToArray());
            }
            catch (Exception ex)
            {
                WriteLine($"genie: error updating host file: {ex.Message}");
                Environment.Exit(130);
            }

            // Set the new hostname.
            if (verbose)
                WriteLine("genie: setting new hostname.");

            if (!MountHelpers.BindMount("/run/hostname-wsl", "/etc/hostname"))
            {
                WriteLine("genie: initializing bottle failed; bind mounting hostname");
                Environment.Exit(EPERM);
            }
        }

        /// <summary>
        /// Drop the in-bottle hostname.
        /// </summary>
        internal static void DropHostname(bool verbose)
        {
            // Drop the in-bottle hostname.
            if (verbose)
                WriteLine("genie: dropping in-bottle hostname");

            if (!MountHelpers.UnMount("/etc/hostname"))
            {
                WriteLine("genie: shutdown failed; unmounting hostname");
                Environment.Exit(EPERM);
            }

            File.Delete("/run/hostname-wsl");

            var hostname = File.ReadAllLines("/etc/hostname")[0].TrimEnd();

            HostHelpers.Hostname = hostname;
        }

        #endregion Hostname management

        #region Resolved symlink

        internal static void CreateResolvSymlink(bool verbose)
        {
            // We cannot check if the target(/run/systemd/resolve/stub-resolv.conf) exists,
            // since it will not be created until after systemd-resolved starts up. So we're
            // going to have to live with that uncertainty.

            // Check if source(/etc/resolv.conf) exists.
            if (File.Exists("/etc/resolv.conf"))
            {
                // If so, move it to the backup file(/etc/resolv.conf.wsl).
                if (verbose)
                    WriteLine("genie: backing up old resolv.conf");

                File.Move("/etc/resolv.conf", "/etc/resolv.conf.wsl", true);
            }

            // Create symbolic link from /etc/resolv.conf to /run/systemd/resolve/stub-resolv.conf.
            if (verbose)
                WriteLine("genie: creating resolv symlink");

            try
            {
                FsHelpers.CreateSymbolicLink("/etc/resolv.conf", "/run/systemd/resolve/stub-resolv.conf");
            }
            catch (Exception ex)
            {
                WriteLine($"genie: error creating resolv symlink: {ex.Message}");
            }
        }

        internal static void RemoveResolvSymlink(bool verbose)
        {
            // Check if /etc/resolv.conf exists, and if so, if it is a symlink to the target.
            // (/run/systemd/resolve/stub-resolv.conf).
            if (!File.Exists("/etc/resolv.conf"))
            {
                WriteLine("genie: resolv symlink does not exist.");
                return;
            }

            if (!(File.GetAttributes("/etc/resolv.conf").HasFlag(FileAttributes.ReparsePoint)))
            {
                WriteLine("genie: resolv symlink is not a symlink.");
                return;
            }

            // If so, delete the symlink.
            File.Delete("/etc/resolv.conf");

            // Check if /etc/resolv.conf.wsl exists.
            if (File.Exists("/etc/resolv.conf.wsl"))
            {
                // If so, move it to /etc/resolv.conf.
                File.Move("/etc/resolv.conf.wsl", "/etc/resolv.conf", true);
            }
            else
            {
                // If not, warn the user.
                WriteLine(
                    "genie: could not find /etc/resolv.conf backup. Please manually restore /etc/resolv.conf.");
            }
        }

        #endregion Resolved symlink

        #region User identities

        internal static string GetLoginName() => Environment.GetEnvironmentVariable("LOGNAME");

        #endregion User identities
    }
}
