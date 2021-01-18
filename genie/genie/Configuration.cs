using System;
using System.IO;

using Microsoft.Extensions.Configuration;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    // COnfiguration values for Genie.
    internal class GenieConfig
    {
        #region Static configuration

        // Install location of genie and friends.
#if LOCAL
        internal const string Prefix = "/usr/local";
#else
        internal const string Prefix = "/usr";
#endif

        // Default environment variables to be added to every genie bottle.
        internal static string[] DefaultVariables = { "INSIDE_GENIE=true" } ;

        #endregion Static configuration

        private IConfiguration Configuration { get; init; }

        internal GenieConfig()
        {
            this.Configuration = new ConfigurationBuilder()
                .SetBasePath ("/etc")
                .AddIniFile ("genie.ini", optional: false)
                .Build();
        }

        // Secure path to use when shelling out from Genie and to init systemd.
        internal string SecurePath => this.Configuration.GetValue<string> ("genie:secure-path", "/lib/systemd:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin");

        // True if the original path should be merged onto the end of the secure path for systemd and/or genie bottle;
        // false otherwise.
        internal bool ClonePath => this.Configuration.GetValue<bool> ("genie:clone-path", false);

        // Environment variables to copy into the systemd environment and/or genie bottle.
        internal string[] CloneEnv => (this.Configuration.GetValue<string> ("genie:clone-env", "WSL_DISTRO_NAME,WSL_INTEROP,WSLENV"))
            .Split (',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // True to update the host name with the "-wsl" suffix, false otherwise.
        internal bool UpdateHostname => this.Configuration.GetValue<bool> ("genie:update-hostname", true);

        // Path to the local binary for unshare(1).
        internal string PathToUnshare => this.Configuration.GetValue<string> ("genie:unshare", "/usr/bin/unshare");

        // Seconds to wait for systemd to enter the running state on startup.
        internal int SystemdStartupTimeout => this.Configuration.GetValue<int> ("genie:systemd-timeout", 180);
        
        // Get the installation-dependent path for a given file.
        internal string GetPrefixedPath (string path) => Path.Combine (GenieConfig.Prefix, path);
    }
}