using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Linux;

using Tmds.Linux;
using static Tmds.Linux.LibC;

using Process=System.Diagnostics.Process;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    public static class Program
    {
        #region System status

        // User ID of the real user running genie.
        public static uid_t realUserId { get; set; }

        // User name of the real user running genie.
        public static string realUserName { get; set;}

        // Group ID of the real user running genie.
        public static gid_t realGroupId { get; set; }

        // PID of the earliest running root systemd, or 0 if there is no running root systemd
        public static int systemdPid { get; set;}

        // Did the bottle exist when genie was started?
        public static bool bottleExistedAtStart { get; set;}

        // Was genie started within the bottle?
        public static bool startedWithinBottle {get; set;}

        #endregion System status

        // Entrypoint.
        public static int Main (string[] args)
        {
            // *** PRELAUNCH CHECKS
            // Check that we are, in fact, running on Linux/WSL.
            if (!RuntimeInformation.IsOSPlatform (OSPlatform.Linux))
            {
                Console.WriteLine ("genie: not executing on the Linux platform - how did we get here?");
                return EBADF;
            }

            if (geteuid() != 0)
            {
                Console.WriteLine ("genie: must execute as root - has the setuid bit gone astray?");
                return EPERM;
            }

            // *** PARSE COMMAND-LINE
            // Create options.
            Option optVerbose = new Option ("--verbose",
                                            "Display verbose progress messages",
                                            new Argument<bool>(defaultValue: false));
            optVerbose.AddAlias ("-v");

            // Add them to the root command.
            var rootCommand = new RootCommand();
            rootCommand.Description = "Handles transitions to the \"bottle\" namespace for systemd under WSL.";
            rootCommand.AddOption (optVerbose);
            rootCommand.Handler = CommandHandler.Create<bool>(RootHandler);

            var cmdInitialize = new Command ("--initialize");
            cmdInitialize.AddAlias ("-i");
            cmdInitialize.Description = "Initialize the bottle (if necessary) only.";
            cmdInitialize.Handler = CommandHandler.Create<bool>(InitializeHandler);

            rootCommand.Add (cmdInitialize);

            var cmdShell = new Command ("--shell");
            cmdShell.AddAlias ("-s");
            cmdShell.Description = "Initialize the bottle (if necessary), and run a shell in it.";
            cmdShell.Handler = CommandHandler.Create<bool>(ShellHandler);

            rootCommand.Add (cmdShell);

            var argCmdLine = new Argument<string> ();
            argCmdLine.Description = "The command to execute within the bottle.";
            argCmdLine.Arity = ArgumentArity.OneOrMore;

            var cmdExec = new Command ("--command");
            cmdExec.AddAlias ("-c");
            cmdExec.Argument = argCmdLine;
            cmdExec.Description = "Initialize the bottle (if necessary), and run the specified command in it.";
            cmdExec.Handler = CommandHandler.Create<bool, List<string>>(ExecHandler);

            rootCommand.Add (cmdExec);

            // Parse the arguments and invoke the handler.
            return rootCommand.InvokeAsync(args).Result;
        }

        // Get the pid of the earliest running root systemd, or 0 if none is running.
        private static int GetSystemdPid ()
        {
            var processInfo = ProcessManager.GetProcessInfos (_ => _.ProcessName == "systemd")
                .Where (_ => _.Ruid == 0)
                .OrderBy (_ => _.StartTime)
                .FirstOrDefault();

            return processInfo != null ? processInfo.ProcessId : 0;
        }

        private static int RunAndWait (string command, string args)
        {
            try
            {
                var p = Process.Start (command, args);
                p.WaitForExit();

                return p.ExitCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"genie: error executing command '{command} {args}':\r\n{ex.Message}");
                Environment.Exit (127);

                // never reached
                return 127;
            }
        }

        private static void Chain (string command, string args, string onError = "command execution failed;")
        {
            int r = RunAndWait (command, args);

            if (r != 0)
            {
                Console.WriteLine ($"genie: {onError} returned {r}.");
                Environment.Exit (r);
            }
        }

        // Do the work of initializing the bottle.
        private static void InitializeBottle (bool verbose)
        {
            int r;

            if (verbose)
                Console.WriteLine ("genie: initializing bottle.");

	    // Dump the envars
            Chain ("/lib/genie/dumpwslenv.sh", "",
                   "initializing bottle failed; dumping WSL envars");

            // Generate new hostname.
            Chain ("/bin/sh", "-c \"/bin/echo `hostname`-wsl > /etc/hostname-wsl\"",
                   "initializing bottle failed; making new hostname");

            unsafe
            {
                var bytes = Encoding.UTF8.GetBytes("/etc/hostname-wsl");
                fixed (byte* buffer = bytes)
                {
                    chmod (buffer, Convert.ToUInt16 ("644", 8));
                }
            }

            // Hosts file: check for old host name; if there, remove it.
            r = RunAndWait ("/bin/sh", "-c \"/usr/bin/hostess has `hostname` > /dev/null\"");

            if (r == 0)
            {
                Chain ("/bin/sh", "-c \"/usr/bin/hostess del `hostname`\"",
                       "initializing bottle failed; removing old hostname");
            }

            // Set the new hostname.
            Chain ("/bin/mount", "--bind /etc/hostname-wsl /etc/hostname",
                   "initializing bottle failed; bind mounting hostname");

            // Hosts file: check for new host name; if not there, update it.
            r = RunAndWait ("/bin/sh", "-c \"/usr/bin/hostess has `hostname`-wsl > /dev/null\"");

            if (r == 1)
            {
                Chain ("/bin/sh", "-c \"/usr/bin/hostess add `hostname`-wsl 127.0.0.1\"",
                       "initializing bottle failed; adding new hostname");
            }

            // Run systemd in a container.
            Chain ("/usr/sbin/daemonize", "/usr/bin/unshare -fp --mount-proc /lib/systemd/systemd",
                   "initializing bottle failed; daemonize");

            // Wait for systemd to be up. (Polling, sigh.)
            do
            {
                Thread.Sleep (500);
                systemdPid = GetSystemdPid();
            } while (systemdPid == 0);
        }

        // Previous UID while rootified.
        private static uid_t previousUid = 0;
        private static gid_t previousGid = 0;

        // Become root.
        private static void Rootify ()
        {
            if (previousUid != 0)
                throw new InvalidOperationException("Cannot rootify root.");

            previousUid = getuid();
            previousGid = getgid();
            setreuid(0, 0);
            setregid(0, 0);

        // Console.WriteLine ($"uid={getuid()} gid={getgid()} euid={geteuid()} egid={getegid()}");
        }

        // Do the work of running a command inside the bottle.
        private static void RunCommand (bool verbose, string commandLine)
        {
            if (verbose)
                Console.WriteLine ($"genie: running command '{commandLine}'");

            Chain ("/usr/bin/nsenter",
                   $"-t {systemdPid} --wd=\"{Environment.CurrentDirectory}\" -m -p /sbin/runuser -u {realUserName} -- {commandLine.Trim()}",
                   "running command failed; nsenter");
        }

        // Do the work of starting a shell inside the bottle.
        private static void StartShell (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: starting shell");

            Chain ("/usr/bin/nsenter",
                   $"-t {systemdPid} -m -p /sbin/runuser -l {realUserName}",
                   "starting shell failed; nsenter");
        }

        // Revert from root.
        private static void Unrootify ()
        {
            // if (previousUid == 0)
            //    throw new InvalidOperationException("Cannot unrootify unroot.");

            setreuid(previousUid, previousUid);
            setregid(previousGid, previousGid);
            previousUid = 0;
            previousGid = 0;
        }

        // Update the status of the system for use by the command handlers.
        private static void UpdateStatus (bool verbose)
        {
            // Store the UID and name of the real user.
            realUserId = getuid();
            realUserName = Environment.GetEnvironmentVariable("LOGNAME");

            realGroupId = getgid();

            // Get systemd PID.
            systemdPid = GetSystemdPid();

            // Set startup state flags.
            if (systemdPid == 0)
            {
                bottleExistedAtStart = false;
                startedWithinBottle = false;

                if (verbose)
                    Console.WriteLine ("genie: no bottle present.");
            }
            else if (systemdPid == 1)
            {
                bottleExistedAtStart = true;
                startedWithinBottle = true;

                if (verbose)
                    Console.WriteLine ("genie: inside bottle.");
            }
            else
            {
                bottleExistedAtStart = true;
                startedWithinBottle = false;

                if (verbose)
                    Console.WriteLine ($"genie: outside bottle {systemdPid}.");
            }
        }

        // Handle the case where genie is invoked without a command specified.
        public static int RootHandler (bool verbose)
        {
            Console.WriteLine("genie: one of the commands -i, -s, or -c must be supplied.");
            return 0;
        }

        // Initialize the bottle (if necessary) only
        public static int InitializeHandler (bool verbose)
        {
            // Update the system status.
            UpdateStatus(verbose);

            // If a bottle exists, we have succeeded already. Exit and report success.
            if (bottleExistedAtStart)
            {
                if (verbose)
                    Console.WriteLine ("genie: bottle already exists (no need to initialize).");

                return 0;
            }

            // Become root - daemonize expects real uid root as well as effective uid root.
            Rootify();

            // Init the bottle.
            InitializeBottle(verbose);

            // Give up root.
            Unrootify();

            return 0;
        }

        // Initialize the bottle, if necessary, then start a shell in it.
        public static int ShellHandler (bool verbose)
        {
            // Update the system status.
            UpdateStatus(verbose);

            if (startedWithinBottle)
            {
                Console.WriteLine ("genie: already inside the bottle; cannot start shell!");
                return EINVAL;
            }

            Rootify();

            if (!bottleExistedAtStart)
                InitializeBottle(verbose);

            // At this point, we should be outside an existing bottle, one way or another.

            // It shouldn't matter whether we have setuid here, since we start the shell with
            // login, which expects root and reassigns uid appropriately.
            StartShell(verbose);

            Unrootify();

            return 0;
        }

        // Initialize the bottle, if necessary, then run a command in it.
        public static int ExecHandler (bool verbose, List<string> command)
        {
            // Update the system status.
            UpdateStatus(verbose);

            // Recombine command argument.
            StringBuilder cmdLine = new StringBuilder (2048);
            foreach (var s in command.Skip (1))
            {
                cmdLine.Append (s);
                cmdLine.Append (' ');
            }

            if (cmdLine.Length > 0)
                cmdLine.Remove (cmdLine.Length - 1, 1);

            // If already inside, just execute it.
            if (startedWithinBottle)
            {
                var p = Process.Start (command.First(), cmdLine.ToString());
                p.WaitForExit();
                return p.ExitCode;
            }

            Rootify();

            if (!bottleExistedAtStart)
                InitializeBottle(verbose);

            // At this point, we should be inside an existing bottle, one way or another.

            RunCommand (verbose, $"{command.First()} {cmdLine.ToString()}");

            return 0;
        }
    }
}
