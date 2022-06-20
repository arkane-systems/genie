using System;
using Tmds.Linux;
using static Tmds.Linux.LibC;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    /// <summary>
    /// While this class exists, the program is running as root.
    /// </summary>
    internal class RootPrivilege : IDisposable
    {
        /// <summary>
        /// Previous UID while running as root.
        /// </summary>
        private uid_t _previousUid;

        /// <summary>
        /// Previous GID while running as root.
        /// </summary>
        private gid_t _previousGid;

        /// <summary>
        /// Become root.
        /// </summary>
        internal RootPrivilege()
        {
            _previousUid = getuid();
            _previousGid = getgid();

            setreuid(0, 0);
            setregid(0, 0);

            IsRoot = true;
        }

        /// <summary>
        /// On disposal, cease to be root.
        /// </summary>
        public void Dispose()
        {
            setreuid(_previousUid, _previousUid);
            setregid(_previousGid, _previousGid);

            _previousUid = 0;
            _previousGid = 0;

            IsRoot = false;
        }

        ~RootPrivilege()
        {
            if (IsRoot)
                Dispose();
        }

        private bool IsRoot { get; set; }
    }
}
