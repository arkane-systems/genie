using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    public class Locations
    {
        // Location of the deviations file.
        public const string DeviationsFile = "lib/genie/deviated-preverts.conf";

        private JObject deviants;

        public Locations ()
        {
            // Get location of deviations file.
            string deviantCorner = Program.GetPrefixedPath (Locations.DeviationsFile);

            try
            {
                // Read in deviations file.
                string deviations = File.ReadAllText (deviantCorner);

                // Parse deviations file.
                this.deviants = JObject.Parse(deviations);
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"genie: could not load deviations file: {ex.Message}");
                Environment.Exit (150);
            }
        }

        public static readonly Dictionary<string, string> DefaultLocations = new Dictionary<string, string> () {
            {"daemonize", "/usr/bin/daemonize"},
            {"env", "/usr/bin/env"},
            {"mount", "/bin/mount"},
            {"nsenter", "/usr/bin/nsenter"},
            {"unshare", "/usr/bin/unshare"},
            {"runuser", "/sbin/runuser"},
            {"systemd", "/bin/systemd"}
        };

        public string GetLocation (string key)
        {
            if (!Locations.DefaultLocations.Keys.Contains(key))
                throw new InvalidOperationException ("Key? I am no key!");

            // Get the file location, if any.
            string fileLoc;
            
            try
            {
                fileLoc = (string)this.deviants[key];
            }
            catch
            {
                fileLoc = null;
            }

            // If it exists, return it. If not, or if none...
            if ((!string.IsNullOrEmpty (fileLoc)) && (File.Exists (fileLoc)))
                return fileLoc;

            // Get the corresponding raw location.
            // If it exists, return it. Otherwise...
            if (File.Exists (Locations.DefaultLocations[key]))
                return Locations.DefaultLocations[key];

            // Exit.
            Console.WriteLine ($"genie: unable to locate '{key}'. Please update deviations file.");
            Environment.Exit (151);

            // can't get here
            throw new InvalidOperationException("One does not simply walk into this code path.");
        }
    }
}