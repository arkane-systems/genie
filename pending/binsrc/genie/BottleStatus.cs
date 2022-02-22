namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    internal enum Status
    {
        NoBottlePresent,
        BottleStarting,
        BottleStarted,
        BottleStartedNotReady,
        BottleShutdown,
        InsideBottleNotReady,
        InsideBottle
    }

    internal record BottleStatus
    {
        // Current status of the bottle.
        internal Status Status { get; init; }

        internal int SystemdPid { get; init;}

        internal BottleStatus (bool verbose)
        {
            SystemdPid = Helpers.GetSystemdPid();

            switch (SystemdPid)
            {
                case 0:
                    // No systemd is running. The only possibility is that there
                    // is no bottle running.
                    this.Status = Status.NoBottlePresent;
                    break;

                case 1:
                    // systemd is running as pid 1. This means we are inside the
                    // bottle. Check systemd status for more information.

                    if (Helpers.IsSystemdRunning(SystemdPid))
                        this.Status = Status.InsideBottle;
                    else
                        this.Status = Status.InsideBottleNotReady;

                    break;

                default:
                    // systemd is running with the given PID. This means that we
                    // are outside the bottle. Current status depends on the state
                    // of the genie flag files.

                    if (FlagFiles.StartupFile)
                    {
                        this.Status = Status.BottleStarting;
                        break;
                    }

                    if (FlagFiles.ShutdownFile)
                    {
                        this.Status = Status.BottleShutdown;
                        break;
                    }

                    if (FlagFiles.RunFile)
                    {
                        if (Helpers.IsSystemdRunning (SystemdPid))
                            this.Status = Status.BottleStarted;
                        else
                            this.Status = Status.BottleStartedNotReady;
                    }

                    break;
            }
        }

        internal bool StartedWithinBottle => (Status == Status.InsideBottle) || (Status == Status.InsideBottleNotReady);

        internal bool BottleExistsInContext => (Status == Status.BottleStarted) || (Status == Status.BottleStartedNotReady);

        internal bool BottleExists => StartedWithinBottle || BottleExistsInContext ;

        internal bool BottleWillExist => (Status == Status.BottleStarting);

        internal bool BottleStartingUp => (Status == Status.BottleStarting);

        internal bool BottleClosingDown => (Status == Status.BottleShutdown);

        internal bool BottleError => (Status == Status.BottleStartedNotReady) || (Status == Status.InsideBottleNotReady);
    }
}