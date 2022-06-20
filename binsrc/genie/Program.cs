using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading;
using static Tmds.Linux.LibC;
using Process = System.Diagnostics.Process;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    public static class Program
    {
        #region Helper methods

        #region AppArmor

        private static void ConfigureAppArmorNamespace(ref string[] startupChain, bool verbose)
        {
            // Check whether AppArmor is available in the kernel.
            if (Directory.Exists("/sys/module/apparmor"))
            {
                // If the AppArmor filesystem is not mounted, mount it.
                if (!Directory.Exists("/sys/kernel/security/apparmor"))
                {
                    // Mount AppArmor filesystem.
                    if (!MountHelpers.Mount("securityfs", "/sys/kernel/security", "securityfs"))
                    {
                        Console.WriteLine("genie: could not mount AppArmor filesystem; attempting to continue without AppArmor namespace");
                        return;
                    }
                }

                // Create AppArmor namespace for genie bottle.
                var nsName = $"genie-{Helpers.WslDistroName}";

                if (verbose)
                   Console.WriteLine($"genie: creating AppArmor namespace {nsName}");

                if (!Directory.Exists("/sys/kernel/security/apparmor/policy/namespaces"))
                {
                    Console.WriteLine("genie: could not find AppArmor filesystem; attempting to continue without AppArmor namespace");
                    return;
                }

                Directory.CreateDirectory($"/sys/kernel/security/apparmor/policy/namespaces/{nsName}");

                // Update startup chain with aa-exec command.
                startupChain = startupChain.Concat(new[] {"aa-exec", "-n", $"{nsName}", "-p", "unconfined", "--"}).ToArray();
            }
            else
                Console.WriteLine("genie: AppArmor not available in kernel; attempting to continue without AppArmor namespace");
        }

        /// <summary>
        /// Now we have exited the bottle, clean up the AppArmor namespace.
        /// </summary>
        private static void UnconfigureAppArmorNamespace(bool verbose)
        {
            var nsName = $"genie-{Helpers.WslDistroName}";

            if (verbose)
                Console.WriteLine($"genie: deleting AppArmor namespace {nsName}");

            if (Directory.Exists($"/sys/kernel/security/apparmor/policy/namespaces/{nsName}"))
            {
                try
                {
                    Directory.Delete($"/sys/kernel/security/apparmor/policy/namespaces/{nsName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"genie: could not delete AppArmor namespace; {ex.Message}");
                }
            }
            else
                Console.WriteLine("genie: no AppArmor namespace to delete");
        }

        #endregion AppArmor

        #region Binfmts

        /// <summary>
        /// Unmount the binfmts fs.
        /// </summary>
        private static void UnmountBinfmts(bool verbose)
        {
            if (Directory.Exists("/proc/sys/fs/binfmt_misc"))
            {
                if (verbose)
                    Console.WriteLine("genie: unmounting binfmt_misc filesystem before proceeding");

                if (!MountHelpers.UnMount("/proc/sys/fs/binfmt_misc"))
                    Console.WriteLine("genie: failed to unmount binfmt_misc filesystem; attempting to continue");
            }
            else
            {
                if (verbose)
                    Console.WriteLine("genie: no binfmt_misc filesystem present");
            }
        }

        /// <summary>
        /// Having unmounted the binfmts fs before starting systemd, we remount it now as
        /// a courtesy. But remember, genie is not guaranteed to be idempotent, so don't
        /// rely on this, for the love of Thompson and Ritchie!
        /// </summary>
        private static void RemountBinfmts(bool verbose)
        {
            if (!Directory.Exists("/proc/sys/fs/binfmt_misc"))
            {
                if (verbose)
                    Console.WriteLine("genie: remounting binfmt_misc filesystem as a courtesy");

                if (!MountHelpers.Mount("binfmt_misc", "/proc/sys/fs/binfmt_misc", FsType.BinaryFormats))
                    Console.WriteLine("genie: failed to remount binfmt_misc filesystem; attempting to continue");
            }
        }

        #endregion Binfmts

        private static RootCommand GetCommandLineParser()
        {
            // Create options.
            Option optVerbose = new Option<bool>("--verbose", getDefaultValue:() => false, description: "Display verbose progress messages");
            optVerbose.AddAlias("-v");

            // Add them to the root command.
            var rootCommand = new RootCommand
            {
                Description = "Handles transitions to the \"bottle\" namespace for systemd under WSL."
            };

            rootCommand.AddOption(optVerbose);
            rootCommand.Handler = CommandHandler.Create((Func<bool, int>)RootHandler);

            var cmdInitialize = new Command("--initialize");
            cmdInitialize.AddAlias("-i");
            cmdInitialize.Description = "Initialize the bottle(if necessary) only.";
            cmdInitialize.Handler = CommandHandler.Create((Func<bool, int>)InitializeHandler);

            rootCommand.Add(cmdInitialize);

            var cmdShell = new Command("--shell");
            cmdShell.AddAlias("-s");
            cmdShell.Description = "Initialize the bottle(if necessary), and run a shell in it.";
            cmdShell.Handler = CommandHandler.Create((Func<bool, int>)ShellHandler);

            rootCommand.Add(cmdShell);

            var cmdLogin = new Command("--login");
            cmdLogin.AddAlias("-l");
            cmdLogin.Description = "Initialize the bottle(if necessary), and open a logon prompt in it.";
            cmdLogin.Handler = CommandHandler.Create((Func<bool, int>)LoginHandler);

            rootCommand.Add(cmdLogin);

            var argCmdLine = new Argument<IEnumerable<string>>("command")
            {
                Description = "The command to execute within the bottle.",
                Arity = ArgumentArity.OneOrMore
            };

            var cmdExec = new Command("--command");
            cmdExec.AddAlias("-c");
            cmdExec.AddArgument(argCmdLine);
            cmdExec.Description = "Initialize the bottle(if necessary), and run the specified command in it.";
            cmdExec.Handler = CommandHandler.Create((Func<bool, IEnumerable<string>, int>)ExecHandler);

            rootCommand.Add(cmdExec);

            var cmdShutdown = new Command("--shutdown");
            cmdShutdown.AddAlias("-u");
            cmdShutdown.Description = "Shut down systemd and exit the bottle.";
            cmdShutdown.Handler = CommandHandler.Create((Func<bool, int>)ShutdownHandler);

            rootCommand.Add(cmdShutdown);

            var cmdIsRunning = new Command("--is-running");
            cmdIsRunning.AddAlias("-r");
            cmdIsRunning.Description = "Check whether systemd is running in genie, or not.";
            cmdIsRunning.Handler = CommandHandler.Create((Func<bool, int>)IsRunningHandler);

            rootCommand.Add(cmdIsRunning);

            var cmdIsInside = new Command("--is-in-bottle");
            cmdIsInside.AddAlias("-b");
            cmdIsInside.Description = "Check whether currently executing within the genie bottle, or not.";
            cmdIsInside.Handler = CommandHandler.Create((Func<bool,int>)IsInsideHandler);

            rootCommand.Add(cmdIsInside);

            var cmdCleanup = new Command("--cleanup")
            {
                Description = "Clean up leftover genie files(only when not in use).",
                Handler = CommandHandler.Create((Func<bool, int>)CleanupHandler)
            };

            rootCommand.Add(cmdCleanup);

            return rootCommand;
        }

        /// <summary>
        /// Set up secure path, saving original if specified.
        /// </summary>
        private static void SetUpSecurePath()
        {
            var originalPath = Config.ClonePath ? Environment.GetEnvironmentVariable("PATH") : @"/mnt/c/Windows/System32";

            Environment.SetEnvironmentVariable("PATH", Config.SecurePath);

            // Create the path file.
            File.WriteAllText("/run/genie.path", originalPath);
        }

        /// <summary>
        /// Stash original environment(specified variables only).
        /// </summary>
        private static void StashEnvironment()
        {
            var clonedVariables = GenieConfig.DefaultVariables
                .Union(Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                .Where(de => Config.CloneEnv.Contains(de.Key))
                .Select(de => $"{de.Key}={de.Value}")).ToArray();

            // Create the env file.
            File.WriteAllLines("/run/genie.env", clonedVariables);
        }

        #endregion Helper methods

        #region Bottle startup and shutdown

        /// <summary>
        /// Do the work of initializing the bottle.
        /// </summary>
        private static void StartBottle(bool verbose)
        {
            FlagFiles.StartupFile = true;

            if (verbose)
                Console.WriteLine("genie: starting bottle");

            SetUpSecurePath();
            StashEnvironment();

            // Check and warn if not multi-user.target.
            if (Config.TargetWarning)
                Helpers.RunAndWait("sh", new[] { GenieConfig.GetPrefixedPath("lib/genie/check-default-target.sh") });

            // Now that the WSL hostname can be set via .wslconfig, we're going to make changing
            // it automatically in genie an option, enable/disable in genie.ini. Defaults to on
            // for backwards compatibility and because turning off when using bridged networking is
            // a Bad Idea.
            if (Config.UpdateHostname)
                Helpers.UpdateHostname(Config.HostnameSuffix, verbose);

            // If configured to, create the resolv.conf symlink.
            if (Config.ResolvedStub)
                Helpers.CreateResolvSymlink(verbose);

            // Unmount the binfmts fs before starting systemd, so systemd can mount it
            // again with all the trimmings.
            UnmountBinfmts(verbose);

            // Define systemd startup chain - command string to pass to daemonize.
            var startupChain = new[] {Config.PathToUnshare, "-fp", "--propagation", "shared", "--mount-proc", "--"};

            // If requested, configure AppArmor namespace.
            if (Config.AppArmorNamespace)
                ConfigureAppArmorNamespace(ref startupChain, verbose);

            // Update startup chain with systemd command.
            startupChain = startupChain.Append("systemd").ToArray();

            // Run systemd in a container.
            if (verbose)
            {
                Console.WriteLine("genie: starting systemd with command line:");
                Console.WriteLine(string.Join(" ", startupChain));
            }

            Helpers.Chain("daemonize", startupChain, "initializing bottle failed; daemonize");

            // Wait for systemd to be up.(Polling, sigh.)
            Console.Write("Waiting for systemd...");

            int systemdPid;

            do
            {
                Thread.Sleep(500);
                systemdPid = Helpers.GetSystemdPid();

                Console.Write(".");
            } while (systemdPid == 0);

            // Now that systemd exists, write out its(external) PID.
            // We do not need to store the inside-bottle PID anywhere for obvious reasons.
            // Create the path file.
            File.WriteAllText("/run/genie.systemd.pid", systemdPid.ToString());

            // Wait for systemd to be in running state.
            int runningYet;
            var timeout = Config.SystemdStartupTimeout;
            var ryArgs = new[] {"-c", $"nsenter -t {systemdPid} -m -p systemctl is-system-running -q 2> /dev/null"};

            do
            {
                Thread.Sleep(1000);
                runningYet = Helpers.RunAndWait("sh", ryArgs);

                Console.Write("!");

                timeout--;

                if (timeout < 0)
                {
                    // What state are we in?
                    var state = Helpers.RunAndWaitForOutput("sh", new[] {"-c", $"\"nsenter -t {systemdPid} -m -p systemctl is-system-running 2> /dev/null\""});

                    if (state.StartsWith("starting"))
                        Console.WriteLine("\n\nSystemd is still starting. This may indicate a unit is being slow to start.\nAttempting to continue.\n");
                    else
                    {
                        Console.WriteLine("\n\nTimed out waiting for systemd to enter running state.\nThis may indicate a systemd configuration error.\nAttempting to continue.\nFailed units will now be displayed(systemctl list-units --failed):");

                        Helpers.ChainInside(systemdPid, new[] {"systemctl", "list-units", "--failed"}, "running command failed; nsenter for systemctl list-units --failed");

                        Console.WriteLine("Information on known-problematic units may be found at\nhttps://github.com/arkane-systems/genie/wiki/Systemd-units-known-to-be-problematic-under-WSL\n");
                    }

                    break;
                }

            } while (runningYet != 0);

            FlagFiles.RunFile = true;
            FlagFiles.StartupFile = false;

            Console.WriteLine();
        }

        /// <summary>
        /// Do the work of shutting down the bottle.
        /// </summary>
        private static void StopBottle(bool verbose)
        {
            FlagFiles.ShutdownFile = true;
            FlagFiles.RunFile = false;

            if (verbose)
                Console.WriteLine("genie: running systemctl poweroff within bottle");

            var systemdPid = Helpers.GetSystemdPid();
            var sd = Process.GetProcessById(systemdPid);

            Helpers.ChainInside(systemdPid, new[] {"systemctl", "poweroff"}, "running command failed; nsenter systemctl poweroff");

            // Wait for systemd to exit.
            Console.Write("Waiting for systemd exit...");

            var timeout = Config.SystemdStartupTimeout;

            while (!sd.WaitForExit(1000))
            {
                Console.Write(".");
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
                UnconfigureAppArmorNamespace(verbose);

            RemountBinfmts(verbose);

            if (Config.ResolvedStub)
                Helpers.RemoveResolvSymlink(verbose);

            if (Config.UpdateHostname)
            {
                Thread.Sleep(500);
                Helpers.DropHostname(verbose);
            }

            FlagFiles.ShutdownFile = false;
        }

        #endregion Bottle startup and shutdown

        #region Run inside bottle

        /// <summary>
        /// Start a user session with the default shell inside the bottle.
        /// </summary>
        private static void StartShell(bool verbose, int systemdPid)
        {
            if (verbose)
                Console.WriteLine("genie: starting shell");

            Helpers.ChainInside(systemdPid, new[] {"machinectl", "shell", "-q", $"{RealUserName}@.host"}, "starting shell failed; machinectl shell");
        }

        /// <summary>
        /// Start a user session with a login prompt inside the bottle.
        /// </summary>
        private static void StartLogin(bool verbose, int systemdPid)
        {
            if (verbose)
                Console.WriteLine("genie: starting login");

            Helpers.ChainInside(systemdPid, new[] {"machinectl", "login", ".host"}, "starting login failed; machinectl login");
        }

        /// <summary>
        /// Run a command in a user session inside the bottle.
        /// </summary>
        private static void RunCommand(bool verbose, int systemdPid, string[] commandLine)
        {
            if (verbose)
                Console.WriteLine($"genie: running command '{string.Join(' ', commandLine)}'");

            var commandPrefix = new[] {"machinectl", "shell", "-q", $"{RealUserName}@.host", GenieConfig.GetPrefixedPath("lib/genie/runinwsl"), Environment.CurrentDirectory };
            var command = commandPrefix.Concat(commandLine);

            Helpers.ChainInside(systemdPid, command.ToArray(), "running command failed; machinectl shell");
        }

        #endregion

        #region Command handlers

        /// <summary>
        /// Handle the case where genie is invoked without a command specified.
        /// </summary>
        private static int RootHandler(bool verbose)
        {
            Console.WriteLine("genie: one of the commands -i, -s, -l, -c, -r or -b must be supplied.");

            return 0;
        }

        /// <summary>
        /// Initialize the bottle(if necessary) only.
        /// </summary>
        private static int InitializeHandler(bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus();

            // If a bottle exists, we have succeeded already. Exit and report success.
            if (state.Status != Status.NoBottlePresent)
            {
                if (verbose)
                    Console.WriteLine("genie: bottle already exists(no need to initialize).");

                return 0;
            }

            // Daemonize expects real uid root as well as effective uid root.
            using var r = new RootPrivilege();
            // Init the bottle.
            StartBottle(verbose);

            return 0;
        }

        /// <summary>
        /// Start a shell inside the bottle, initializing it if necessary.
        /// </summary>
        private static int ShellHandler(bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus();

            // If inside bottle, cannot start shell.
            if (state.StartedWithinBottle)
            {
                Console.WriteLine("genie: already inside the bottle; cannot start shell!");
                return EINVAL;
            }

            // If shutting down, display error and exit.
            if (state.BottleClosingDown)
            {
                Console.WriteLine("genie: bottle is stopping, cannot start shell!");
                return ECANCELED;
            }

            // If bottle does not exist, initialize it.
            if (!state.BottleExistsInContext && !state.BottleStartingUp)
            {
                // Daemonize expects real uid root as well as effective uid root.
                // Init the bottle.
                StartBottle(verbose);
            }

            // If bottle is starting up, wait for it.
            if (state.BottleStartingUp)
            {
                Console.Write("genie: bottle is starting in another session, please wait");

                do
                {
                    Thread.Sleep(1000);
                    Console.Write(".");

                    state = new BottleStatus();
                } while (!state.BottleExistsInContext);

                Console.WriteLine();
            }

            if (state.BottleError)
                Console.WriteLine("* WARNING: Bottled systemd is not in running state; errors may occur");

            // At this point, we should be outside an existing bottle, one way or another.
            // It shouldn't matter whether we have setuid here, since we start the shell with
            // machinectl, which reassigns uid appropriately as login(1).
            StartShell(verbose, Helpers.GetSystemdPid());

            return 0;
        }

        /// <summary>
        /// Start a login prompt inside the bottle, initializing it if necessary.
        /// </summary>
        private static int LoginHandler(bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus();

            // If inside bottle, cannot start shell.
            if (state.StartedWithinBottle)
            {
                Console.WriteLine("genie: already inside the bottle; cannot start login prompt!");
                return EINVAL;
            }

            // If shutting down, display error and exit.
            if (state.BottleClosingDown)
            {
                Console.WriteLine("genie: bottle is stopping, cannot start login prompt!");
                return ECANCELED;
            }

            // If bottle does not exist, initialize it.
            if (!state.BottleExistsInContext && !state.BottleStartingUp)
            {
                // Daemonize expects real uid root as well as effective uid root.
                // Init the bottle.
                StartBottle(verbose);
            }

            // If bottle is starting up, wait for it.
            if (state.BottleStartingUp)
            {
                Console.Write("genie: bottle is starting in another session, please wait");

                do
                {
                    Thread.Sleep(1000);
                    Console.Write(".");

                    state = new BottleStatus();
                } while (!state.BottleExistsInContext);

                Console.WriteLine();
            }

            if (state.BottleError)
                Console.WriteLine("* WARNING: Bottled systemd is not in running state; errors may occur");

            // At this point, we should be outside an existing bottle, one way or another.
            // It shouldn't matter whether we have setuid here, since we start the shell with
            // a login prompt, which reassigns uid appropriately as login(1).
            StartLogin(verbose, Helpers.GetSystemdPid());

            return 0;
        }

        /// <summary>
        /// Run a command inside the bottle, initializing it if necessary.
        /// </summary>
        private static int ExecHandler(bool verbose, IEnumerable<string> command)
        {
            // Get the bottle state.
            var state = new BottleStatus();

            // If inside bottle, just execute the command.
            if (state.StartedWithinBottle)
            {
                var enumerable = command.ToList();
                return Helpers.RunAndWait(enumerable.First(), enumerable.Skip(1).ToArray());
            }

            // If shutting down, display error and exit.
            if (state.BottleClosingDown)
            {
                Console.WriteLine("genie: bottle is stopping, cannot run command!");
                return ECANCELED;
            }

            // If bottle does not exist, initialize it.
            if (!state.BottleExistsInContext && !state.BottleStartingUp)
            {
                // Daemonize expects real uid root as well as effective uid root.
                // Init the bottle.
                StartBottle(verbose);
            }

            // If bottle is starting up, wait for it.
            if (state.BottleStartingUp)
            {
                Console.Write("genie: bottle is starting in another session, please wait");

                do
                {
                    Thread.Sleep(1000);
                    Console.Write(".");

                    state = new BottleStatus();
                } while (!state.BottleExistsInContext);

                Console.WriteLine();
            }

            if (state.BottleError)
                Console.WriteLine("* WARNING: Bottled systemd is not in running state; errors may occur");

            // At this point, we should be inside an existing bottle, one way or another.
            RunCommand(verbose, Helpers.GetSystemdPid(), command.ToArray());

            return 0;
        }

        /// <summary>
        /// Shut down the bottle and clean up.
        /// </summary>
        private static int ShutdownHandler(bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus();

            if (state.Status == Status.NoBottlePresent)
            {
                Console.WriteLine("genie: no bottle exists.");
                return EINVAL;
            }

            if (state.StartedWithinBottle)
            {
                Console.WriteLine("genie: cannot shut down bottle from inside bottle; exiting.");
                return EINVAL;
            }

            if (state.BottleWillExist || state.BottleClosingDown)
            {
                Console.WriteLine("genie: bottle in transitional state; please wait until this is complete.");
            }

            using var r = new RootPrivilege();
            StopBottle(verbose);

            return 0;
        }

        /// <summary>
        /// Check whether systemd has been started by genie, or not.
        /// </summary>
        private static int IsRunningHandler(bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus();

            if (state.BottleExists && !state.BottleError)
            {
                Console.WriteLine("running");
                return 0;
            }

            if (state.BottleWillExist)
            {
                Console.WriteLine("starting");
                return 2;
            }

            if (state.BottleClosingDown)
            {
                Console.WriteLine("stopping");
                return 3;
            }

            if (state.BottleError)
            {
                Console.WriteLine("running(systemd errors)");
                return 4;
            }

            Console.WriteLine("stopped");

            return 1;
        }

        /// <summary>
        /// Check whether currently executing within the genie bottle, or not.
        /// </summary>
        private static int IsInsideHandler(bool verbose)
        {
            // Get the bottle state.
            var state = new BottleStatus();

            if (state.StartedWithinBottle)
            {
                Console.WriteLine("inside");
                return 0;
            }

            if (state.Status != Status.NoBottlePresent)
            {
                Console.WriteLine("outside");
                return 1;
            }

            Console.WriteLine("no-bottle");
            return 2;
        }

        /// <summary>
        /// Cleanup leftover genie files.
        /// </summary>
        private static int CleanupHandler(bool verbose)
        {
            string[] files =
            {
                "/run/genie.startup",
                "/run/genie.shutdown",
                "/run/genie.up",
                "/run/genie.env",
                "/run/genie.path",
                "/run/genie.systemd.pid",
                "/run/hostname-wsl"
            };

            // Get the bottle state.
            var state = new BottleStatus();

            if (state.Status != Status.NoBottlePresent)
            {
                Console.WriteLine("genie: cannot clean up while bottle exists.");
                return EINVAL;
            }

            foreach(var s in files)
            {
                if (File.Exists(s))
                {
                    if (verbose)
                        Console.WriteLine($"genie: deleting leftover file {s}");

                    File.Delete(s);
                }
            }

            return 0;
        }

        #endregion Command handlers

        /// <summary>
        /// Configuration for genie.
        /// </summary>
        private static GenieConfig Config { get; } = new();

        /// <summary>
        /// User name of the real user running genie.
        /// </summary>
        private static string RealUserName { get; set;}

        /// <summary>
        /// Entry point.
        /// </summary>
        public static int Main(string[] args)
        {
            // *** PRELAUNCH CHECKS
            // Check that we are, in fact, running on Linux/WSL.
            if (!PlatformChecks.IsLinux)
            {
                Console.WriteLine("genie: not executing on the Linux platform - how did we get here?");
                return EBADF;
            }

            if (PlatformChecks.IsWsl1)
            {
                Console.WriteLine("genie: systemd is not supported under WSL 1.");
                return EPERM;
            }

            if (!PlatformChecks.IsWsl2)
            {
                Console.WriteLine("genie: not executing under WSL 2 - how did we get here?");
                return EBADF;
            }

            if (!UidChecks.IsEffectivelyRoot)
            {
                Console.WriteLine("genie: must execute as root - has the setuid bit gone astray?");
                return EPERM;
            }

            // Store the name of the real user.
            RealUserName = Helpers.GetLoginName();

            // Parse the command-line arguments and invoke the proper command.
            var result = GetCommandLineParser().InvokeAsync(args).Result;

            return result;
        }
    }
}
