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
using Nett;
using Tmds.Linux;
using static Tmds.Linux.LibC;

using Process=System.Diagnostics.Process;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    public static class Program
    {
#if LOCAL
        public const string Prefix = "/usr/local";
#else
        public const string Prefix = "/usr";
#endif

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

            if (!IsWsl())
            {
                Console.WriteLine ("genie: not executing under WSL - how did we get here?");
                return EBADF;
            }

            if (IsWsl1())
            {
                Console.WriteLine ("genie: systemd is not supported under WSL 1.");
                return EPERM;
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
            rootCommand.Handler = CommandHandler.Create<bool>((Func<bool, int>)RootHandler);

            var cmdInitialize = new Command ("--initialize");
            cmdInitialize.AddAlias ("-i");
            cmdInitialize.Description = "Initialize the bottle (if necessary) only.";
            cmdInitialize.Handler = CommandHandler.Create<bool>((Func<bool, int>)InitializeHandler);

            rootCommand.Add (cmdInitialize);

            var cmdShell = new Command ("--shell");
            cmdShell.AddAlias ("-s");
            cmdShell.Description = "Initialize the bottle (if necessary), and run a shell in it.";
            cmdShell.Handler = CommandHandler.Create<bool>((Func<bool, int>)ShellHandler);

            rootCommand.Add (cmdShell);

            var argCmdLine = new Argument<string> ();
            argCmdLine.Description = "The command to execute within the bottle.";
            argCmdLine.Arity = ArgumentArity.OneOrMore;

            var cmdExec = new Command ("--command");
            cmdExec.AddAlias ("-c");
            cmdExec.Argument = argCmdLine;
            cmdExec.Description = "Initialize the bottle (if necessary), and run the specified command in it.";
            cmdExec.Handler = CommandHandler.Create<bool, List<string>>((Func<bool, List<string>, int>)ExecHandler);

            rootCommand.Add (cmdExec);

            // Parse the arguments and invoke the handler.
            return rootCommand.InvokeAsync(args).Result;
        }

        // Check if we are being run under WSL.
        private static bool IsWsl()
        {
            var osrelease = File.ReadAllText("/proc/sys/kernel/osrelease");

            return osrelease.Contains ("microsoft", StringComparison.OrdinalIgnoreCase);
        }

        // Check if we are being run under WSL 1.
        private static bool IsWsl1()
        {
            // We check for WSL 1 by examining the type of the root filesystem. If the
            // root filesystem is lxfs, then we're running under WSL 1. If not,
            // and having already established that we're running under WSL, we
            // assume 2.

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
                    return (deets[2] == "lxfs") ;
                }
            }

            Console.WriteLine ("genie: cannot find root filesystem mount; terminating.");
            Environment.Exit (EPERM);

            // should never get here
            return true;
        }

        private static string GetPrefixedPath (string path) => Path.Combine (Prefix, path);

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

        private static string GenerateEnvironmentString(string env)
        {
            return $"echo {env}=${env} >> /run/genie.env\n";
        }

        private static bool EnvironmentExist(string env)
        {
            return Environment.GetEnvironmentVariable(env) != null;
        }

        private static void insertToPATH(string str)
        {
            File.AppendText("/run/genie.env").Write($"PATH=\"${{PATH:+${{PATH}}:}}{str}\"");
        }
        
        private static void ReadConfig() //TODO: reduce a lot of complexity
        {
            const string filename = "/etc/genie.conf";
            try
            {
                var toml = Toml.ReadFile(filename);
                var passEnvironment = toml.Get<bool>("PassEnvironment");
                var tablePath = toml.Get<TomlTable>("PATH");
                var passAllPath = tablePath.Get<bool>("PassAllPATH");
                var passSelectedPath = tablePath.Get<bool>("PassSelectedPATH");
                var addToPath = tablePath.Get <List<string>>("AddToPATH");
                var environmentToPass = toml.Get<TomlTable>("ENVIRONMENT").Get <List<string>>("EnvironmentToPass");
                if ( passAllPath && passSelectedPath)
                {
                    passAllPath = false;
                    Console.WriteLine("WARNING: Both \"PassAllPATH\" and \"PassSelectedPATH\" enabled, using PassSelectedPATH only");
                }

                var dumpEnvFileLocation = GetPrefixedPath("/lib/genie/dumpwslenv.sh");
                var dumpEnvFile = File.CreateText(dumpEnvFileLocation);
                if (!passEnvironment) return;
                if (passAllPath)
                {
                    dumpEnvFile.Write(GenerateEnvironmentString("PATH"));
                }

                if (passSelectedPath)
                {
                    switch (addToPath.Count)
                    {
                        case 0:
                            Console.WriteLine("WARNING: The list AddToPATH in settings is empty but PassSelectedPATH is true");
                            break;
                        case 1:
                            insertToPATH(addToPath[0]);
                            break;
                        default:
                        {
                            var toAdd = addToPath.Aggregate("", (current, value) => current + (value + ':'));

                            // This should remove the trailing :, without the hassle to track the last one
                            toAdd = toAdd.Substring(0, toAdd.Length -1);
                            insertToPATH(toAdd);
                            break;
                        }
                    }
                }

                foreach (var value in environmentToPass)
                {
                    if (EnvironmentExist(value))
                    {
                        dumpEnvFile.Write(GenerateEnvironmentString(value));
                    }
                    else
                    {
                        Console.WriteLine($"Environment Value {value} don't exist, skipping");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        // Do the work of initializing the bottle.
        private static void InitializeBottle (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: initializing bottle.");

            // Dump the envars
            if (verbose)
                Console.WriteLine ("genie: dumping WSL environment variables.");
            
            ReadConfig();
            Chain (GetPrefixedPath ("/lib/genie/dumpwslenv.sh"), "",
                   "initializing bottle failed; dumping WSL envars");

            // Generate new hostname.
            if (verbose)
                Console.WriteLine ("genie: generating new hostname.");

            Chain ("/bin/sh", "-c \"/bin/echo `hostname`-wsl > /run/hostname-wsl\"",
                   "initializing bottle failed; making new hostname");

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
                Console.WriteLine ("genie: removing old hostname.");

            Chain ("/bin/sh", String.Concat ("-c \"", GetPrefixedPath ("bin/hostess"), " del `hostname`\""),
                       "initializing bottle failed; removing old hostname");

            // Set the new hostname.
            if (verbose)
                Console.WriteLine ("genie: setting new hostname.");

            Chain ("/bin/mount", "--bind /run/hostname-wsl /etc/hostname",
                   "initializing bottle failed; bind mounting hostname");

            // Hosts file: check for new host name; if not there, update it.
            Chain ("/bin/sh", String.Concat ("-c \"", GetPrefixedPath ("bin/hostess"), " add `hostname`-wsl 127.0.0.1\""),
                       "initializing bottle failed; adding new hostname");

            // Run systemd in a container.
            if (verbose)
                Console.WriteLine ("genie: starting systemd.");

            Chain ("/usr/sbin/daemonize", "/usr/bin/unshare -fp --propagation shared --mount-proc /lib/systemd/systemd",
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
                   String.Concat ($"-t {systemdPid} --wd=\"{Environment.CurrentDirectory}\" -m -p /sbin/runuser -u {realUserName} -- ",
                                  GetPrefixedPath ("/lib/genie/runinwsl.sh"),
                                  $" {commandLine.Trim()}"),
                   "running command failed; nsenter");
        }

        // Do the work of starting a shell inside the bottle.
        private static void StartShell (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: starting shell");

            // Read environment variables
            var envars = new StringBuilder (256);
            var envnames = new StringBuilder (128);

            if (File.Exists ("/run/genie.env"))
            {
        foreach (string s in File.ReadAllLines ("/run/genie.env"))
                {
                    var v = s.Split (new char[] {'='});

                    envars.Append ($"{s} ");
                    envnames.Append ($"{v[0]},");

                    if (verbose)
                        Console.WriteLine ($"envar: {v[0]}={v[1]}");
                }
                if (envars.Length > 0) envars.Length--;
                if (envnames.Length > 0) envnames.Length--;
            }
            else
              Console.WriteLine ("genie: environment file missing; continuing anyway");

            Chain ("/usr/bin/nsenter",
                   $"-t {systemdPid} -m -p /usr/bin/env {envars.ToString()} /sbin/runuser -l {realUserName} -w {envnames.ToString()}",
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
            // runuser, which expects root and reassigns uid appropriately.
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
