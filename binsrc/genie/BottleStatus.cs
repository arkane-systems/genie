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
    internal record BottleStatus
    {
        internal BottleStatus (int systemdPid, bool verbose)
        {
            // Set startup state flags.
            if (systemdPid == 0)
            {
                this.existedAtStart = false;
                this.startedWithin = false;

                if (verbose)
                    Console.WriteLine ("genie: no bottle present.");
            }
            else if (systemdPid == 1)
            {
                this.existedAtStart = true;
                this.startedWithin = true;

                if (verbose)
                    Console.WriteLine ("genie: inside bottle.");
            }
            else
            {
                this.existedAtStart = true;
                this.startedWithin = false;

                if (verbose)
                    Console.WriteLine ($"genie: outside bottle, systemd pid: {systemdPid}.");
            }
        }

        // Did the bottle exist when genie was started?
        internal bool existedAtStart { get; init;}

        // Was genie started within the bottle?
        internal bool startedWithin {get; init;}
    }
}