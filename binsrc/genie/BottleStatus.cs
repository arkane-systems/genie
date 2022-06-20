namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    internal record BottleStatus
    {
        /// <summary>
        /// Current status of the bottle.
        /// </summary>
        internal Status Status { get; }

        private int SystemdPid { get; }

        internal BottleStatus()
        {
            SystemdPid = Helpers.GetSystemdPid();
            switch (SystemdPid)
            {
                case 0:
                    // No systemd is running. The only possibility is that there
                    // is no bottle running.
                    Status = Status.NoBottlePresent;
                    break;
                case 1:
                    // systemd is running as pid 1. This means we are inside the
                    // bottle. Check systemd status for more information.
                    Status = Helpers.IsSystemdRunning(SystemdPid) ? Status.InsideBottle : Status.InsideBottleNotReady;
                    break;
                default:
                    // systemd is running with the given PID. This means that we
                    // are outside the bottle. Current status depends on the state
                    // of the genie flag files.

                    if (FlagFiles.StartupFile)
                    {
                        Status = Status.BottleStarting;
                        break;
                    }

                    if (FlagFiles.ShutdownFile)
                    {
                        Status = Status.BottleShutdown;
                        break;
                    }

                    if (FlagFiles.RunFile)
                        Status = Helpers.IsSystemdRunning(SystemdPid) ? Status.BottleStarted : Status.BottleStartedNotReady;

                    break;
            }
        }

        internal bool StartedWithinBottle => Status is Status.InsideBottle or Status.InsideBottleNotReady;

        internal bool BottleExistsInContext => Status is Status.BottleStarted or Status.BottleStartedNotReady;

        internal bool BottleExists => StartedWithinBottle || BottleExistsInContext ;

        internal bool BottleWillExist => Status == Status.BottleStarting;

        internal bool BottleStartingUp => Status == Status.BottleStarting;

        internal bool BottleClosingDown => Status == Status.BottleShutdown;

        internal bool BottleError => Status is Status.BottleStartedNotReady or Status.InsideBottleNotReady;
    }
}
