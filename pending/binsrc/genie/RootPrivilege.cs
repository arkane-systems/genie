using System;

using Tmds.Linux;
using static Tmds.Linux.LibC;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    // While this class exists, the program is running as root.
    internal class RootPrivilege : IDisposable
    {
        // Previous UID while running as root.
        private uid_t previousUid = 0;

        // Previous GID while running as root.
        private gid_t previousGid = 0;

        // Become root.
        internal RootPrivilege()
        {
            this.previousUid = getuid();
            this.previousGid = getgid();

            setreuid(0, 0);
            setregid(0, 0);

            this.IsRoot = true;
        }

        // On disposal, cease to be root.
        public void Dispose()
        {
            setreuid(this.previousUid, this.previousUid);
            setregid(this.previousGid, this.previousGid);

            this.previousUid = 0;
            this.previousGid = 0;

            this.IsRoot = false;
        }

        ~RootPrivilege()
        {
            if (this.IsRoot)
                this.Dispose();
        }

        internal bool IsRoot { get; private set; } = false;
    }
}
