using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading;

using static Tmds.Linux.LibC;

using Process=System.Diagnostics.Process;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    public static class Program
    {
        // Configuration for genie.
        internal static GenieConfig Config { get; } = new GenieConfig();

        // Original path before we enforce secure path.
        public static string originalPath {get; set;}

        // Selection of original environment variables.
        public static string[] clonedVariables {get; set;}

        // User name of the real user running genie.
        public static string realUserName { get; set;}

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

            // Store the name of the real user.
            // TODO: replace this with something less hilariously insecure
            realUserName = Environment.GetEnvironmentVariable("LOGNAME");

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

            var cmdIsRunning = new Command ("--is-running");
            cmdIsRunning.AddAlias ("-r");
            cmdIsRunning.Description = "Check whether systemd is running in genie, or not.";
            cmdIsRunning.Handler = CommandHandler.Create<bool>((Func<bool, int>)IsRunningHandler);

            rootCommand.Add (cmdIsRunning);

            var cmdIsInside = new Command ("--is-in-bottle");
            cmdIsInside.AddAlias ("-b");
            cmdIsInside.Description = "Check whether currently executing within the genie bottle, or not.";
            cmdIsInside.Handler = CommandHandler.Create<bool>((Func<bool,int>)IsInsideHandler);

            rootCommand.Add (cmdIsInside);

            // Parse the arguments and invoke the handler.
            return rootCommand.InvokeAsync(args).Result;
        }

        #region Command handlers

        // Handle the case where genie is invoked without a command specified.
        public static int RootHandler (bool verbose)
        {
            Console.WriteLine("genie: one of the commands -i, -s, or -c must be supplied.");
            return 0;
        }

        // Initialize the bottle (if necessary) only
        public static int InitializeHandler (bool verbose)
        {
            // Get the bottle state.
            var state = GetBottleState(verbose);

            // If a bottle exists, we have succeeded already. Exit and report success.
            if (state.existedAtStart)
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

        // Initialize the bottle, if necessary, then start a shell in it.
        public static int ShellHandler (bool verbose)
        {
            // Get the bottle state.
            var state = GetBottleState (verbose);

            if (state.startedWithin)
            {
                Console.WriteLine ("genie: already inside the bottle; cannot start shell!");
                return EINVAL;
            }

            using (var r = new RootPrivilege())
            {
                if (!state.existedAtStart)
                    InitializeBottle(verbose);

                // At this point, we should be outside an existing bottle, one way or another.

                // It shouldn't matter whether we have setuid here, since we start the shell with
                // machinectl, which reassigns uid appropriately as login(1).
                StartShell(verbose);
            }

            return 0;
        }

        // Initialize the bottle, if necessary, then start a login prompt in it.
        public static int LoginHandler (bool verbose)
        {
            // Get the bottle state.
            var state = GetBottleState (verbose);

            if (state.startedWithin)
            {
                Console.WriteLine ("genie: already inside the bottle; cannot start login prompt!");
                return EINVAL;
            }

            using (var r = new RootPrivilege())
            {
                if (!state.existedAtStart)
                    InitializeBottle(verbose);

                // At this point, we should be outside an existing bottle, one way or another.

                // It shouldn't matter whether we have setuid here, since we start the shell with
                // a login prompt, which reassigns uid appropriately.
                StartLogin(verbose);
            }

            return 0;
        }

        // Initialize the bottle, if necessary, then run a command in it.
        public static int ExecHandler (bool verbose, IEnumerable<string> command)
        {
            // Get the bottle state.
            var state = GetBottleState (verbose);

            // If already inside, just execute it.
            if (state.startedWithin)
                return Helpers.RunAndWait (command.First(), command.Skip(1).ToArray());

            using (var r = new RootPrivilege())
            {

                if (!state.existedAtStart)
                    InitializeBottle(verbose);

                // At this point, we should be inside an existing bottle, one way or another.

                RunCommand (verbose, command.ToArray());
            }

            return 0;
        }

        // Shut down the bottle and clean up.
        public static int ShutdownHandler (bool verbose)
        {
            // Get the bottle state.
            var state = GetBottleState (verbose);

            if (!state.existedAtStart)
            {
                Console.WriteLine ("genie: no bottle exists.");
                return EINVAL;
            }

            if (state.startedWithin)
            {
                Console.WriteLine ("genie: cannot shut down bottle from inside bottle; exiting.");
                return EINVAL;
            }

            using (var r = new RootPrivilege())
            {
                if (verbose)
                    Console.WriteLine ("genie: running systemctl poweroff within bottle");

                var systemdPid = Helpers.GetSystemdPid();
                var sd = Process.GetProcessById (systemdPid);

                Helpers.Chain ("nsenter",
                    new string[] {"-t", systemdPid.ToString(), "-m", "-p", "systemctl", "poweroff"},
                    "running command failed; nsenter");

                Console.Write ("Waiting for systemd exit...");

                // Wait for systemd to exit.
                int timeout = Config.SystemdStartupTimeout;

                while (!sd.WaitForExit(1000))
                {
                    Console.Write (".");
                    timeout--;

                    if (timeout < 0)
                    {
                        Console.WriteLine("\n\nTimed out waiting for systemd to exit.\nThis may indicate a systemd configuration error.\nAttempting to continue.");
                        break;
                    }
                }

                Console.WriteLine();

                if (Config.UpdateHostname)
                {
                    Thread.Sleep (500);
                    Helpers.DropHostname (verbose);
                }
            }

            return 0;
        }

        // Check whether systemd has been started by genie, or not.
        public static int IsRunningHandler (bool verbose)
        {
            // Get the bottle state.
            var state = GetBottleState (verbose);

            if (state.existedAtStart)
            {
                Console.WriteLine ("running");
                return 0;
            }

            Console.WriteLine ("stopped");
            return 1;
        }

        // Check whether currently executing within the genie bottle, or not.
        public static int IsInsideHandler (bool verbose)
        {
            // Get the bottle state.
            var state = GetBottleState (verbose);

            if (state.startedWithin)
            {
                Console.WriteLine ("inside");
                return 0;
            }

            if (state.existedAtStart)
            {
                Console.WriteLine("outside");
                return 1;
            }

            Console.WriteLine("no-bottle");
            return 2;
        }

        #endregion Command handlers

        #region Implementation methods

        // Get the current status of the bottle.
        private static BottleStatus GetBottleState (bool verbose)
        {
            // Get systemd PID.
            var systemdPid = Helpers.GetSystemdPid();

            return new BottleStatus (systemdPid, verbose);
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
                Helpers.UpdateHostname (verbose);

            // Run systemd in a container.
            if (verbose)
                Console.WriteLine ("genie: starting systemd.");

            Helpers.Chain ("daemonize",
                new string[] {Config.PathToUnshare, "-fp", "--propagation", "shared", "--mount-proc", "systemd"},
                "initializing bottle failed; daemonize");

            // Wait for systemd to be up. (Polling, sigh.)
            Console.Write ("Waiting for systemd...");

            int systemdPid;

            do
            {
                Thread.Sleep (500);
                systemdPid = Helpers.GetSystemdPid();

                Console.Write (".");

            } while ( systemdPid == 0);

            // Now that systemd exists, write out its (external) PID.
            // We do not need to store the inside-bottle PID anywhere for obvious reasons.
            // Create the path file.
            File.WriteAllText("/run/genie.systemd.pid", systemdPid.ToString());

            // Wait for systemd to be in running state.
            int runningYet = 255;
            int timeout = Config.SystemdStartupTimeout;

            var ryArgs = new string[] {"-c", $"nsenter -t {systemdPid} -m -p systemctl is-system-running -q 2> /dev/null"};

            do
            {
                Thread.Sleep (1000);
                runningYet = Helpers.RunAndWait ("sh", ryArgs);

                Console.Write ("!");

                timeout--;
                if (timeout < 0)
                {
                    Console.WriteLine("\n\nTimed out waiting for systemd to enter running state.\nThis may indicate a systemd configuration error.\nAttempting to continue.\nFailed units will now be displayed (systemctl list-units --failed):");

                    Helpers.Chain ("nsenter",
                        new string[] {"-t", systemdPid.ToString(), "-m", "-p", "systemctl", "list-units", "--failed"},
                        "running command failed; nsenter for systemctl list-units --failed");

                    break;
                }

            } while ( runningYet != 0);

            if (Config.MountXSocket)
            {
                if (verbose)
                    Console.Write ("genie: bind mounting WSLg X11 socket");

                Helpers.Chain ("nsenter",
                    new string[] {"-t", systemdPid.ToString(), "-m", "-p", Config.GetPrefixedPath ("libexec/genie/bindxsocket.sh")},
                    "running bindxsocket.sh failed; nsenter");
            }

            Console.WriteLine();
        }

        // Do the work of running a command inside the bottle.
        private static void RunCommand (bool verbose, string[] commandLine)
        {
            if (verbose)
                Console.WriteLine ($"genie: running command '{string.Join(' ', commandLine)}'");

            var commandPrefix = new string[] {"shell", "-q", $"{realUserName}@.host", Config.GetPrefixedPath ("libexec/genie/runinwsl"),
                    Environment.CurrentDirectory };

            var command = commandPrefix.Concat(commandLine);

            Helpers.Chain ("machinectl",
                command.ToArray(),
                "running command failed; machinectl shell");
        }

        // Start a user session with a login prompt inside the bottle.
        private static void StartLogin (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: starting login");

            Helpers.Chain ("machinectl",
                new string[] {"login", ".host"},
                "starting login failed; machinectl login");
        }

        // Do the work of starting a shell inside the bottle.
        private static void StartShell (bool verbose)
        {
            if (verbose)
                Console.WriteLine ("genie: starting shell");

            Helpers.Chain ("machinectl",
                new string[] {"shell", "-q", $"{realUserName}@.host"},
                "starting shell failed; machinectl shell");
        }

        #endregion Implementation methods
    }
}
