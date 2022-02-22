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
        #region Helper methods

        #region AppArmor

        private static void ConfigureAppArmorNamespace (ref string[] startupChain, bool verbose)
        {
            // Check whether AppArmor is available in the kernel.
            if (Directory.Exists("/sys/module/apparmor"))
            {
                // If the AppArmor filesystem is not mounted, mount it.
                if (!Directory.Exists("/sys/kernel/security/apparmor"))
                {
                    // Mount AppArmor filesystem.
                    if (!MountHelpers.Mount ("securityfs", "/sys/kernel/security", "securityfs"))
                    {
                        Console.WriteLine ("genie: could not mount AppArmor filesystem; attempting to continue without AppArmor namespace");
                        return;
                    }
                }

                // Create AppArmor namespace for genie bottle.
                string nsName = $"genie-{Helpers.WslDistroName}";

                if (verbose)
                   Console.WriteLine ($"genie: creating AppArmor namespace {nsName}");

                if (!Directory.Exists("/sys/kernel/security/apparmor/policy/namespaces"))
                {
                    Console.WriteLine ("genie: could not find AppArmor filesystem; attempting to continue without AppArmor namespace");
                    return;
                }

                Directory.CreateDirectory ($"/sys/kernel/security/apparmor/policy/namespaces/{nsName}");

                // Update startup chain with aa-exec command.
                startupChain = startupChain.Concat(new string[] {"aa-exec", "-n", $"{nsName}", "-p", "unconfined", "--"}).ToArray();
            }
            else
            {
                Console.WriteLine ("genie: AppArmor not available in kernel; attempting to continue without AppArmor namespace");
            }
        }

        // Now we have exited the bottle, clean up the AppArmor namespace.
        private static void UnconfigureAppArmorNamespace (bool verbose)
        {
            string nsName = $"genie-{Helpers.WslDistroName}";

            if (verbose)
                Console.WriteLine ($"genie: deleting AppArmor namespace {nsName}");

            if (Directory.Exists($"/sys/kernel/security/apparmor/policy/namespaces/{nsName}"))
            {
                try
                {
                    Directory.Delete($"/sys/kernel/security/apparmor/policy/namespaces/{nsName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine ($"genie: could not delete AppArmor namespace; {ex.Message}");
                }
            }
            else
                Console.WriteLine ("genie: no AppArmor namespace to delete"); 
        }

        #endregion AppArmor

        #region Binfmts

        // Unmount the binfmts fs.
        private static void UnmountBinfmts (bool verbose)
        {
            if (Directory.Exists("/proc/sys/fs/binfmt_misc"))
            {
                if (verbose)
                    Console.WriteLine ("genie: unmounting binfmt_misc filesystem before proceeding");

                if (!MountHelpers.UnMount ("/proc/sys/fs/binfmt_misc"))
                {
                    Console.WriteLine ("genie: failed to unmount binfmt_misc filesystem; attempting to continue");
                }
            }
            else
            {
                if (verbose)
                    Console.WriteLine ("genie: no binfmt_misc filesystem present");
            }
        }

        // Having unmounted the binfmts fs before starting systemd, we remount it now as
        // a courtesy. But remember, genie is not guaranteed to be idempotent, so don't
        // rely on this, for the love of Thompson and Ritchie!
        private static void RemountBinfmts (bool verbose)
        {
            if (!Directory.Exists("/proc/sys/fs/binfmt_misc"))
            {
                if (verbose)
                    Console.WriteLine ("genie: remounting binfmt_misc filesystem as a courtesy");

                if (!MountHelpers.Mount("binfmt_misc", "/proc/sys/fs/binfmt_misc", FsType.BinaryFormats))
                {
                    Console.WriteLine ("genie: failed to remount binfmt_misc filesystem; attempting to continue");
                }
            }
        }

        #endregion Binfmts

        private static RootCommand GetCommandLineParser()
        {
            // Create options.
            Option optVerbose = new Option<bool> ("--verbose",
                                                  getDefaultValue: () => false,
                                                  description: "Display verbose progress messages");
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

            var cmdCleanup = new Command ("--cleanup");
            cmdCleanup.Description = "Clean up leftover genie files (only when not in use).";
            cmdCleanup.Handler = CommandHandler.Create<bool>((Func<bool, int>)CleanupHandler);

            rootCommand.Add (cmdCleanup);

            return rootCommand;
        }

        // Set up secure path, saving original if specified.
        private static void SetUpSecurePath()
        {
            string originalPath;

            if (Config.ClonePath)
                originalPath = Environment.GetEnvironmentVariable("PATH");
            else
                // TODO: Should reference system drive by letter
                originalPath = @"/mnt/c/Windows/System32";

            Environment.SetEnvironmentVariable ("PATH", Config.SecurePath);

            // Create the path file.
            File.WriteAllText("/run/genie.path", originalPath);
        }

        // Stash original environment (specified variables only).
        private static void StashEnvironment()
        {
            string[] clonedVariables;

            clonedVariables = GenieConfig.DefaultVariables
                .Union (from DictionaryEntry de in Environment.GetEnvironmentVariables()
                        where Config.CloneEnv.Contains (de.Key)
                        select $"{de.Key}={de.Value}")
                .ToArray();

            // Create the env file.
            File.WriteAllLines("/run/genie.env", clonedVariables);
        }

        #endregion Helper methods

        #region Bottle startup and shutdown

        // Do the work of initializing the bottle.
        public static void StartBottle (bool verbose)
        {
            FlagFiles.StartupFile = true;

            if (verbose)
                Console.WriteLine ("genie: starting bottle");

            SetUpSecurePath();
            StashEnvironment();

            // Check and warn if not multi-user.target
            if (Config.TargetWarning)
            {
                Helpers.RunAndWait ("sh", new string[] { Config.GetPrefixedPath ("lib/genie/check-default-target.sh") });
            } 

            // Now that the WSL hostname can be set via .wslconfig, we're going to make changing
            // it automatically in genie an option, enable/disable in genie.ini. Defaults to on
            // for backwards compatibility and because turning off when using bridged networking is
            // a Bad Idea.
            if (Config.UpdateHostname)
                Helpers.UpdateHostname (Config.HostnameSuffix, verbose);

            // If configured to, create the resolv.conf symlink.
            if (Config.ResolvedStub)
                Helpers.CreateResolvSymlink (verbose);

            // Unmount the binfmts fs before starting systemd, so systemd can mount it
            // again with all the trimmings.
            UnmountBinfmts (verbose);

            // Define systemd startup chain - command string to pass to daemonize
            string [] startupChain = new string[] {Config.PathToUnshare, "-fp", "--propagation", "shared", "--mount-proc", "--"};

            // If requested, configure AppArmor namespace.
            if (Config.AppArmorNamespace)
                ConfigureAppArmorNamespace (ref startupChain, verbose);

            // Update startup chain with systemd command.
            startupChain = startupChain.Append("systemd").ToArray();

            // Run systemd in a container.
            if (verbose)
            {
                Console.WriteLine ("genie: starting systemd with command line:");
                Console.WriteLine (string.Join(" ", startupChain));
            }

            Helpers.Chain ("daemonize",
                startupChain,
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
                    // What state are we in?
                    var state = Helpers.RunAndWaitForOutput ("sh", new string[] {"-c", $"\"nsenter -t {systemdPid} -m -p systemctl is-system-running 2> /dev/null\""});

                    if (state.StartsWith("starting"))
                    {
                        Console.WriteLine("\n\nSystemd is still starting. This may indicate a unit is being slow to start.\nAttempting to continue.\n");
                    }
                    else
                    {
                        Console.WriteLine("\n\nTimed out waiting for systemd to enter running state.\nThis may indicate a systemd configuration error.\nAttempting to continue.\nFailed units will now be displayed (systemctl list-units --failed):");

                        Helpers.ChainInside (systemdPid,
                            new string[] {"systemctl", "list-units", "--failed"},
                            "running command failed; nsenter for systemctl list-units --failed");

                        Console.WriteLine("Information on known-problematic units may be found at\nhttps://github.com/arkane-systems/genie/wiki/Systemd-units-known-to-be-problematic-under-WSL\n");
                    }

                    break;
                }

            } while ( runningYet != 0);

            FlagFiles.RunFile = true;
            FlagFiles.StartupFile = false;

            Console.WriteLine();
        }

        // Do the work of shutting down the bottle.
        public static void StopBottle (bool verbose)
        {
            FlagFiles.ShutdownFile = true;
            FlagFiles.RunFile = false;      

            if (verbose)
                Console.WriteLine ("genie: running systemctl poweroff within bottle");

            var systemdPid = Helpers.GetSystemdPid();
            var sd = Process.GetProcessById (systemdPid);

            Helpers.ChainInside (systemdPid,
                new string[] {"systemctl", "poweroff"},
                "running command failed; nsenter systemctl poweroff");

            // Wait for systemd to exit.
            Console.Write ("Waiting for systemd exit...");

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

            // We reverse the processes we performed to pre-startup the bottle as the post-shutdown, in reverse order.
            if (Config.AppArmorNamespace)
                UnconfigureAppArmorNamespace (verbose);

            RemountBinfmts (verbose);

            if (Config.ResolvedStub)
                Helpers.RemoveResolvSymlink (verbose);

            if (Config.UpdateHostname)
            {
                Thread.Sleep (500);
                Helpers.DropHostname (verbose);
            }

            FlagFiles.ShutdownFile = false;
        }

        #endregion Bottle startup and shutdown

        #region Run inside bottle

        // Start a user session with the default shell inside the bottle.
        private static void StartShell (bool verbose, int systemdPid)
        {
            if (verbose)
                Console.WriteLine ("genie: starting shell");

            Helpers.ChainInside (systemdPid,
                new string[] {"machinectl", "shell", "-q", $"{realUserName}@.host"},
                "starting shell failed; machinectl shell");
        }

        // Start a user session with a login prompt inside the bottle.
        private static void StartLogin (bool verbose, int systemdPid)
        {
            if (verbose)
                Console.WriteLine ("genie: starting login");

            Helpers.ChainInside (systemdPid,
                new string[] {"machinectl", "login", ".host"},
                "starting login failed; machinectl login");
        }

        // Run a command in a user session inside the bottle.
        private static void RunCommand (bool verbose, int systemdPid, string[] commandLine)
        {
            if (verbose)
                Console.WriteLine ($"genie: running command '{string.Join(' ', commandLine)}'");

            var commandPrefix = new string[] {"machinectl", "shell", "-q", $"{realUserName}@.host", Config.GetPrefixedPath ("lib/genie/runinwsl"),
                    Environment.CurrentDirectory };

            var command = commandPrefix.Concat(commandLine);

            Helpers.ChainInside (systemdPid,
                command.ToArray(),
                "running command failed; machinectl shell");
        }

        #endregion

        #region Command handlers

        // Handle the case where genie is invoked without a command specified.
        public static int RootHandler (bool verbose)
        {
            Console.WriteLine("genie: one of the commands -i, -s, -l, -c, -r or -b must be supplied.");
            return 0;
        }

        // // Initialize the bottle (if necessary) only
        public static int InitializeHandler (bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus (verbose);

            // If a bottle exists, we have succeeded already. Exit and report success.
            if (state.Status != Status.NoBottlePresent)
            {
                if (verbose)
                    Console.WriteLine ("genie: bottle already exists (no need to initialize).");

                return 0;
            }

            // Daemonize expects real uid root as well as effective uid root.
            using (var r = new RootPrivilege())
            {
                // Init the bottle.
                StartBottle(verbose);
            }

            return 0;
        }

        // Start a shell inside the bottle, initializing it if necessary.
        public static int ShellHandler (bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus (verbose);

            // If inside bottle, cannot start shell.
            if (state.StartedWithinBottle)
            {
                Console.WriteLine ("genie: already inside the bottle; cannot start shell!");
                return EINVAL;
            }

            // If shutting down, display error and exit.
            if (state.BottleClosingDown)
            {
                Console.WriteLine ("genie: bottle is stopping, cannot start shell!");
                return ECANCELED;
            }

            // If bottle does not exist, initialize it.
            if (!state.BottleExistsInContext && !state.BottleStartingUp)
            {
                // Daemonize expects real uid root as well as effective uid root.
                using (var r = new RootPrivilege())
                {
                    // Init the bottle.
                    StartBottle(verbose);
                }
            }

            // If bottle is starting up, wait for it.
            if (state.BottleStartingUp)
            {
                Console.Write ("genie: bottle is starting in another session, please wait");

                do
                {
                    Thread.Sleep (1000);
                    Console.Write (".");

                    state = new BottleStatus (verbose);
                } while (!state.BottleExistsInContext);

                Console.WriteLine();
            }

            if (state.BottleError)
            {
                Console.WriteLine ("* WARNING: Bottled systemd is not in running state; errors may occur");
            }

            using (var r = new RootPrivilege())
            {
                // At this point, we should be outside an existing bottle, one way or another.

                // It shouldn't matter whether we have setuid here, since we start the shell with
                // machinectl, which reassigns uid appropriately as login(1).
                StartShell (verbose, Helpers.GetSystemdPid());
            }

            return 0;
        }

        // Start a login prompt inside the bottle, initializing it if necessary.
        public static int LoginHandler (bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus (verbose);

            // If inside bottle, cannot start shell.
            if (state.StartedWithinBottle)
            {
                Console.WriteLine ("genie: already inside the bottle; cannot start login prompt!");
                return EINVAL;
            }

            // If shutting down, display error and exit.
            if (state.BottleClosingDown)
            {
                Console.WriteLine ("genie: bottle is stopping, cannot start login prompt!");
                return ECANCELED;
            }

            // If bottle does not exist, initialize it.
            if (!state.BottleExistsInContext && !state.BottleStartingUp)
            {
                // Daemonize expects real uid root as well as effective uid root.
                using (var r = new RootPrivilege())
                {
                    // Init the bottle.
                    StartBottle(verbose);
                }
            }

            // If bottle is starting up, wait for it.
            if (state.BottleStartingUp)
            {
                Console.Write ("genie: bottle is starting in another session, please wait");

                do
                {
                    Thread.Sleep (1000);
                    Console.Write (".");

                    state = new BottleStatus (verbose);
                } while (!state.BottleExistsInContext);

                Console.WriteLine();
            }

            if (state.BottleError)
            {
                Console.WriteLine ("* WARNING: Bottled systemd is not in running state; errors may occur");
            }

            using (var r = new RootPrivilege())
            {
                // At this point, we should be outside an existing bottle, one way or another.

                // It shouldn't matter whether we have setuid here, since we start the shell with
                // a login prompt, which reassigns uid appropriately as login(1).
                StartLogin (verbose, Helpers.GetSystemdPid());
            }

            return 0;
        }

        // Run a command inside the bottle, initializing it if necessary.
        public static int ExecHandler (bool verbose, IEnumerable<string> command)
        {
            // Get the bottle state.
            var state = new BottleStatus (verbose);

            // If inside bottle, just execute the command.
            if (state.StartedWithinBottle)
                return Helpers.RunAndWait (command.First(), command.Skip(1).ToArray());

            // If shutting down, display error and exit.
            if (state.BottleClosingDown)
            {
                Console.WriteLine ("genie: bottle is stopping, cannot run command!");
                return ECANCELED;
            }

            // If bottle does not exist, initialize it.
            if (!state.BottleExistsInContext && !state.BottleStartingUp)
            {
                // Daemonize expects real uid root as well as effective uid root.
                using (var r = new RootPrivilege())
                {
                    // Init the bottle.
                    StartBottle(verbose);
                }
            }

            // If bottle is starting up, wait for it.
            if (state.BottleStartingUp)
            {
                Console.Write ("genie: bottle is starting in another session, please wait");

                do
                {
                    Thread.Sleep (1000);
                    Console.Write (".");

                    state = new BottleStatus (verbose);
                } while (!state.BottleExistsInContext);

                Console.WriteLine();
            }

            if (state.BottleError)
            {
                Console.WriteLine ("* WARNING: Bottled systemd is not in running state; errors may occur");
            }

            using (var r = new RootPrivilege())
            {
                // At this point, we should be inside an existing bottle, one way or another.

                RunCommand (verbose, Helpers.GetSystemdPid(), command.ToArray());
            }

            return 0;
        }

        // Shut down the bottle and clean up.
        public static int ShutdownHandler (bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus (verbose);

            if (state.Status == Status.NoBottlePresent)
            {
                Console.WriteLine ("genie: no bottle exists.");
                return EINVAL;
            }

            if (state.StartedWithinBottle)
            {
                Console.WriteLine ("genie: cannot shut down bottle from inside bottle; exiting.");
                return EINVAL;
            }

            if (state.BottleWillExist || state.BottleClosingDown)
            {
                Console.WriteLine ("genie: bottle in transitional state; please wait until this is complete.");
            }

            using (var r = new RootPrivilege())
            {
                StopBottle (verbose);
            }

            return 0;
        }

        // Check whether systemd has been started by genie, or not.
        public static int IsRunningHandler (bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus (verbose);

            if (state.BottleExists && !state.BottleError)
            {
                Console.WriteLine ("running");
                return 0;
            }
            else if (state.BottleWillExist)
            {
                Console.WriteLine ("starting");
                return 2;
            }
            else if (state.BottleClosingDown)
            {
                Console.WriteLine ("stopping");
                return 3;
            }
            else if (state.BottleError)
            {
                Console.WriteLine ("running (systemd errors)");
                return 4;
            }

            Console.WriteLine ("stopped");
            return 1;
        }

        // Check whether currently executing within the genie bottle, or not.
        public static int IsInsideHandler (bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus (verbose);

            if (state.StartedWithinBottle)
            {
                Console.WriteLine ("inside");
                return 0;
            }
            else if (!(state.Status == Status.NoBottlePresent))
            {
                Console.WriteLine("outside");
                return 1;
            }

            Console.WriteLine("no-bottle");
            return 2;
        }

        // Cleanup leftover genie files
        public static int CleanupHandler (bool verbose)
        {
            string[] files = {
                "/run/genie.startup",
                "/run/genie.shutdown",
                "/run/genie.up",
                "/run/genie.env",
                "/run/genie.path",
                "/run/genie.systemd.pid",
                "/run/hostname-wsl"
            };

            // Get the bottle state.
            var state = new BottleStatus (verbose);

            if (state.Status != Status.NoBottlePresent)
            {
                Console.WriteLine ("genie: cannot clean up while bottle exists.");
                return EINVAL;
            }

            foreach (string s in files)
            {
                if (File.Exists (s))
                {
                    if (verbose)
                        Console.WriteLine ($"genie: deleting leftover file {s}");

                    File.Delete (s);
                }
            }

            return 0;
        }

        #endregion Command handlers

        // Configuration for genie.
        internal static GenieConfig Config { get; } = new GenieConfig();

        // User name of the real user running genie.
        public static string realUserName { get; set;}

        // Entrypoint.
        public static int Main (string[] args)
        {
            // *** PRELAUNCH CHECKS
            // Check that we are, in fact, running on Linux/WSL.
            if (!PlatformChecks.IsLinux)
            {
                Console.WriteLine ("genie: not executing on the Linux platform - how did we get here?");
                return EBADF;
            }

            if (PlatformChecks.IsWsl1)
            {
                Console.WriteLine ("genie: systemd is not supported under WSL 1.");
                return EPERM;
            }

            if (!PlatformChecks.IsWsl2)
            {
                Console.WriteLine ("genie: not executing under WSL 2 - how did we get here?");
                return EBADF;
            }

            if (!UidChecks.IsEffectivelyRoot)
            {
                Console.WriteLine ("genie: must execute as root - has the setuid bit gone astray?");
                return EPERM;
            }

            // Store the name of the real user.
            realUserName = Helpers.GetLoginName();

            // Parse the command-line arguments and invoke the proper command.
            var result = GetCommandLineParser().InvokeAsync(args).Result;

            return result;
        }
    }
}
