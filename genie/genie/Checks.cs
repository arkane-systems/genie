using System;
using System.IO;
using System.Runtime.InteropServices;

using static Tmds.Linux.LibC;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    internal static class Checks
    {
        // Are we running on the Linux platform?
        internal static bool IsLinux => RuntimeInformation.IsOSPlatform (OSPlatform.Linux);

        // Are we being run under WSL 1?
        internal static bool IsWsl1
        {
            get
            {
                // We check for WSL 1 by examining the type of the root filesystem. If the
                // root filesystem is lxfs or wslfs, then we're running under WSL 1.

                var mounts = File.ReadAllLines("/proc/self/mounts");

                foreach (var mnt in mounts)
                {
                    var deets = mnt.Split(' ');
                    if (deets.Length < 6)
                    {
                        Console.WriteLine ("genie: mounts format error; terminating.");
                        Environment.Exit (EBADF);
                    }

                    if (deets[1] == "/")
                    {
                        // Root filesystem.
                        return ((deets[2] == "lxfs") || (deets[2] == "wslfs")) ;
                    }
                }

                Console.WriteLine ("genie: cannot find root filesystem mount; terminating.");
                Environment.Exit (EPERM);

                // should never get here
                return true;
            }
        }
        
        // Are we being run under WSL 2?
        internal static bool IsWsl2
        {
            get
            {
                if (Directory.Exists("/run/WSL"))
                   return true;
                else
                {
                    var osrelease = File.ReadAllText("/proc/sys/kernel/osrelease");

                    return osrelease.Contains ("microsoft", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        // Are we running as EUID root, i.e. setuid root?
        internal static bool IsSetuidRoot => geteuid() == 0;
    }
}