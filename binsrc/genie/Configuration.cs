using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    /// <summary>
    /// Configuration values for Genie.
    /// </summary>
    internal class GenieConfig
    {
        #region Static configuration

        // Install location of genie and friends.
#if LOCAL
        internal const string Prefix = "/usr/local";
#else
        private const string Prefix = "/usr";
#endif

        /// <summary>
        /// Default environment variables to be added to every genie bottle.
        /// </summary>
        internal static readonly string[] DefaultVariables = { "INSIDE_GENIE=true" };

        #endregion Static configuration

        private IConfiguration Configuration { get; }

        internal GenieConfig()
        {
            Configuration = new ConfigurationBuilder().SetBasePath("/etc").AddIniFile("genie.ini", optional: false).Build();
        }

        /// <summary>
        /// Secure path to use when shelling out from Genie and to init systemd.
        /// </summary>
        internal string SecurePath => Configuration.GetValue("genie:secure-path", "/lib/systemd:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin");

        /// <summary>
        /// True if the original path should be merged onto the end of the secure path for systemd and/or genie bottle;
        /// false otherwise.
        /// </summary>
        internal bool ClonePath => Configuration.GetValue("genie:clone-path", false);

        /// <summary>
        /// Environment variables to copy into the systemd environment and/or genie bottle.
        /// </summary>
        internal IEnumerable<string> CloneEnv => Configuration.GetValue("genie:clone-env", "WSL_DISTRO_NAME,WSL_INTEROP,WSLENV")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        /// <summary>
        /// True to update the host name with the "-wsl" suffix, false otherwise.
        /// </summary>
        internal bool UpdateHostname => Configuration.GetValue("genie:update-hostname", true);

        /// <summary>
        /// Suffix with which to update the host name, if the above is true.
        /// </summary>
        internal string HostnameSuffix => Configuration.GetValue("genie:update-hostname-suffix", "-wsl");

        /// <summary>
        /// Path to the local binary for unshare(1).
        /// </summary>
        internal string PathToUnshare => Configuration.GetValue("genie:unshare", "/usr/bin/unshare");

        /// <summary>
        /// Seconds to wait for systemd to enter the running state on startup.
        /// </summary>
        internal int SystemdStartupTimeout => Configuration.GetValue("genie:systemd-timeout", 240);

        /// <summary>
        /// True to symlink a stub resolv.conf file for systemd-resolved, false otherwise.
        /// </summary>
        internal bool ResolvedStub => Configuration.GetValue("genie:resolved-stub", false);

        internal bool AppArmorNamespace => Configuration.GetValue("genie:apparmor-namespace", false);

        /// <summary>
        /// Get the installation-dependent path for a given file.
        /// </summary>
        internal static string GetPrefixedPath(string path) => Path.Combine(Prefix, path);

        /// <summary>
        /// Warn the user if the systemd target is not multi-user.target.
        /// </summary>
        internal bool TargetWarning => Configuration.GetValue("genie:target-warning", true);
    }
}
