using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Tmds.Linux;
using static Tmds.Linux.LibC;

using Process=System.Diagnostics.Process;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    public static class Program
    {
        // Configuration for genie.
        internal static GenieConfig Config { get; } = new GenieConfig();

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

        // Original path before we enforce secure path.
        public static string originalPath {get; set;}

        // Selection of original environment variables.
        public static string[] clonedVariables {get; set;}

        #endregion System status

        // Entrypoint.
        public static int Main (string[] args)
        {
            // *** PRELAUNCH CHECKS
            // Check that we are, in fact, running on Linux/WSL.
            if (!Checks.IsLinux)
            {
                Console.WriteLine ("genie: not executing on the Linux platform - how did we get here?");
                return EBADF;
            }

            if (Checks.IsWsl1)
            {
                Console.WriteLine ("genie: systemd is not supported under WSL 1.");
                return EPERM;
            }

            if (!Checks.IsWsl2)
            {
                Console.WriteLine ("genie: not executing under WSL 2 - how did we get here?");
                return EBADF;
            }

            if (!Checks.IsSetuidRoot)
            {
                Console.WriteLine ("genie: must execute as root - has the setuid bit gone astray?");
                return EPERM;
            }

            // Set up secure path, saving original if specified.

            if (Config.ClonePath)
                originalPath = Environment.GetEnvironmentVariable("PATH");
            else
                // TODO: Should reference system drive by letter
                originalPath = @"/mnt/c/Windows/System32";

            Environment.SetEnvironmentVariable ("PATH", Config.SecurePath);

            // Stash original environment (specified variables only).
            clonedVariables = GenieConfig.DefaultVariables
                .Union (from DictionaryEntry de in Environment.GetEnvironmentVariables()
                        where Config.CloneEnv.Contains (de.Key)
                        select $"{de.Key}={de.Value}")
                .ToArray();

            // *** PARSE COMMAND-LINE
            // Create options.
            Option optVerbose = new Option ("--verbose",
                                            "Display verbose progress messages");
            optVerbose.AddAlias ("-v");
            optVerbose.Argument = new Argument<bool>();
            optVerbose.Argument.SetDefaultValue(false);

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

            var cmdLogin = new Command ("--login");
            cmdLogin.AddAlias ("-l");
            cmdLogin.Description = "Initialize the bottle (if necessary), and open a logon prompt in it.";
            cmdLogin.Handler = CommandHandler.Create<bool>((Func<bool, int>)LoginHandler);

            rootCommand.Add (cmdLogin);

            var argCmdLine = new Argument<IEnumerable<string>> ("command");
            argCmdLine.Description = "The command to execute within the bottle.";
            argCmdLine.Arity = ArgumentArity.OneOrMore;

            var cmdExec = new Command ("--command");
            cmdExec.AddAlias ("-c");
            cmdExec.AddArgument(argCmdLine);
            cmdExec.Description = "Initialize the bottle (if necessary), and run the specified command in it.";
            cmdExec.Handler = CommandHandler.Create<bool, IEnumerable<string>>((Func<bool, IEnumerable<string>, int>)ExecHandler);

            rootCommand.Add (cmdExec);

            var cmdShutdown = new Command ("--shutdown");
            cmdShutdown.AddAlias ("-u");
            cmdShutdown.Description = "Shut down systemd and exit the bottle.";
            cmdShutdown.Handler = CommandHandler.Create<bool>((Func<bool, int>)ShutdownHandler);

            rootCommand.Add (cmdShutdown);

            // Parse the arguments and invoke the handler.
            return rootCommand.InvokeAsync(args).Result;
        }

        // Do the work of initializing the bottle.
        private static void InitializeBottle (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: initializing bottle.");

            // Create the path file.
            File.WriteAllText("/run/genie.path", originalPath);

            // Create the env file.
            File.WriteAllLines("/run/genie.env", clonedVariables);

            // Now that the WSL hostname can be set via .wslconfig, we're going to make changing
            // it automatically in genie an option, enable/disable in genie.ini. Defaults to on
            // for backwards compatibility.
            if (Config.UpdateHostname)
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

                // Chain ("mount", "--bind /run/hostname-wsl /etc/hostname",
                //        "initializing bottle failed; bind mounting hostname");
                Helpers.Chain ("mount",
                    new string[] {"--bind", "/run/hostname-wsl", "/etc/hostname"},
                    "initializing bottle failed; bind mounting hostname");
            }

            // Run systemd in a container.
            if (verbose)
                Console.WriteLine ("genie: starting systemd.");

            // Chain ("daemonize", $"{Config.PathToUnshare} -fp --propagation shared --mount-proc systemd",
            //        "initializing bottle failed; daemonize");
            Helpers.Chain ("daemonize",
                new string[] {Config.PathToUnshare, "-fp", "--propagation", "shared", "--mount-proc", "systemd"},
                "initializing bottle failed; daemonize");

            // Wait for systemd to be up. (Polling, sigh.)
            Console.Write ("Waiting for systemd...");

            do
            {
                Thread.Sleep (500);
                systemdPid = Helpers.GetSystemdPid();

                Console.Write (".");

            } while ( systemdPid == 0);

            // Wait for systemd to be in running state.\
            int runningYet = 255;
            int timeout = Config.SystemdStartupTimeout;

            var ryArgs = new string[] {"-c", $"nsenter -t {systemdPid} -m -p systemctl is-system-running -q 2> /dev/null"};

            do
            {
                Thread.Sleep (1000);
                // runningYet = RunAndWait ("sh", $"-c \"nsenter -t {systemdPid} -m -p systemctl is-system-running -q 2> /dev/null\"");
                runningYet = Helpers.RunAndWait ("sh", ryArgs);

                Console.Write ("!");

                timeout--;
                if (timeout < 0)
                {
                    Console.WriteLine("\n\nTimed out waiting for systemd to enter running state.\nThis may indicate a systemd configuration error.\nAttempting to continue.");
                    break;
                }

            } while ( runningYet != 0);

            Console.WriteLine();
        }

        // Do the work of running a command inside the bottle.
        private static void RunCommand (bool verbose, string[] commandLine)
        {
            if (verbose)
                Console.WriteLine ($"genie: running command '{string.Join(' ', commandLine)}'");

            // Chain ("machinectl",
            //        String.Concat ($"shell -q {realUserName}@.host ",
            //                       Config.GetPrefixedPath ("libexec/genie/runinwsl.sh"),
            //                       $" \"{Environment.CurrentDirectory}\" {commandLine.Trim()}"),
            //        "running command failed; machinectl shell");
            var commandPrefix = new string[] {"shell", "-q", $"{realUserName}@.host", Config.GetPrefixedPath ("libexec/genie/runinwsl"),
                    Environment.CurrentDirectory };

            var command = commandPrefix.Concat(commandLine);

            Helpers.Chain ("machinectl",
                command.ToArray(),
                "running command failed; machinectl shell");
        }

        // Do the work of starting a shell inside the bottle.
        private static void StartShell (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: starting shell");

            // Chain ("machinectl",
            //        $"shell -q {realUserName}@.host",
            //        "starting shell failed; machinectl shell");
            Helpers.Chain ("machinectl",
                new string[] {"shell", "-q", $"{realUserName}@.host"},
                "starting shell failed; machinectl shell");
        }

        // Start a user session with a login prompt inside the bottle.
        private static void StartLogin (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: starting login");

            // Chain ("machinectl",
            //        $"login .host",
            //        "starting login failed; machinectl login");
            Helpers.Chain ("machinectl",
                new string[] {"login", ".host"},
                "starting login failed; machinectl login");
        }

        // Update the status of the system for use by the command handlers.
        private static void UpdateStatus (bool verbose)
        {
            // Store the UID and name of the real user.
            realUserId = getuid();
            realUserName = Environment.GetEnvironmentVariable("LOGNAME");

            realGroupId = getgid();

            // Get systemd PID.
            systemdPid = Helpers.GetSystemdPid();

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
                    Console.WriteLine ($"genie: outside bottle, systemd pid: {systemdPid}.");
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

            // Daemonize expects real uid root as well as effective uid root.
            using (var r = new RootPrivilege())
            {            
                // Init the bottle.
                InitializeBottle(verbose);
            }

            return 0;
        }

        public static int ShutdownHandler (bool verbose)
        {
            // Update the system status.
            UpdateStatus(verbose);

            if (!bottleExistedAtStart)
            {
                Console.WriteLine ("genie: no bottle exists.");
                return EINVAL;
            }

            if (startedWithinBottle)
            {
                Console.WriteLine ("genie: cannot shut down bottle from inside bottle; exiting.");
                return EINVAL;
            }

            using (var r = new RootPrivilege())
            {
                if (verbose)
                    Console.WriteLine ("genie: running systemctl poweroff within bottle");

                var sd = Process.GetProcessById (systemdPid);

                // Call systemctl to trigger shutdown.
                // Chain ("nsenter",
                //        String.Concat ($"-t {systemdPid} -m -p systemctl poweroff"),
                //        "running command failed; nsenter");
                Helpers.Chain ("nsenter",
                    new string[] {"-t", systemdPid.ToString(), "-m", "-p", "systemctl", "poweroff"},
                    "running command failed; nsenter");

                if (verbose)
                    Console.WriteLine ("genie: waiting for systemd to exit");

                // Wait for systemd to exit (maximum 16 s).
                sd.WaitForExit(16000);

                if (Config.UpdateHostname)
                {
                    // Drop the in-bottle hostname.
                    if (verbose)
                        Console.WriteLine ("genie: dropping in-bottle hostname");

                    Thread.Sleep (500);

                    Helpers.Chain ("umount", new string[] {"/etc/hostname"});
                    File.Delete ("/run/hostname-wsl");

                    Helpers.Chain ("hostname", new string[] {"-F", "/etc/hostname"} );
                }
            }

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

            using (var r = new RootPrivilege())
            {
                if (!bottleExistedAtStart)
                    InitializeBottle(verbose);

                // At this point, we should be outside an existing bottle, one way or another.

                // It shouldn't matter whether we have setuid here, since we start the shell with
                // runuser, which expects root and reassigns uid appropriately.
                StartShell(verbose);
            }

            return 0;
        }

        // Initialize the bottle, if necessary, then start a login prompt in it.
        public static int LoginHandler (bool verbose)
        {
            // Update the system status.
            UpdateStatus(verbose);

            if (startedWithinBottle)
            {
                Console.WriteLine ("genie: already inside the bottle; cannot start login prompt!");
                return EINVAL;
            }

            using (var r = new RootPrivilege())
            {
                if (!bottleExistedAtStart)
                    InitializeBottle(verbose);

                // At this point, we should be outside an existing bottle, one way or another.

                // It shouldn't matter whether we have setuid here, since we start the shell with
                // runuser, which expects root and reassigns uid appropriately.
                StartLogin(verbose);
            }

            return 0;
        }

        // Initialize the bottle, if necessary, then run a command in it.
        public static int ExecHandler (bool verbose, IEnumerable<string> command)
        {
            // Update the system status.
            UpdateStatus(verbose);

            // Recombine command argument.
            // StringBuilder cmdLine = new StringBuilder (2048);
            // foreach (var s in command.Skip (1))
            // {
            //     cmdLine.Append (s);
            //     cmdLine.Append (' ');
            // }

            // if (cmdLine.Length > 0)
            //     cmdLine.Remove (cmdLine.Length - 1, 1);

            // If already inside, just execute it.
            if (startedWithinBottle)
            {
                var p = Process.Start (command.First(), command.Skip(1));
                p.WaitForExit();
                return p.ExitCode;
            }

            using (var r = new RootPrivilege())
            {

                if (!bottleExistedAtStart)
                    InitializeBottle(verbose);

                // At this point, we should be inside an existing bottle, one way or another.

                RunCommand (verbose, command.ToArray());
            }

            return 0;
        }
    }
}
