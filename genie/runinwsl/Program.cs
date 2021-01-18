using System;
using System.Diagnostics;
using System.Linq;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie.RunInWsl
{
    public static class Program
    {
        private static void PrintUsage ()
        {
            Console.WriteLine ("runwsl: error in usage; should only be called by genie");
            Console.WriteLine ("runwsl <ewd> <command line>");
        }

        // Entrypoint.
        public static int Main (string[] args)
        {
            if (args.Length < 2)
            {
                Program.PrintUsage ();
                return 127;
            }

            // Split args into executable working directory and command.
            string ewd = args[0];
            var command = args.Skip(1);

            // Import the environment from the saved WSL environment.

            /*

            # Import the environment from the saved WSL environment to the shell.
            if [ -e /run/genie.env ]
            then
                export $(cat /run/genie.env | xargs)
            fi

            */

            // Import and merge the path from the saved WSL environment.

            /*

            if [ -e /run/genie.path ]
            then
                PATH=$PATH:$(cat /run/genie.path)
                export PATH="$(echo $PATH | awk -v RS=: '!($0 in a) {a[$0]; printf("%s%s", length(a) > 1 ? ":" : "", $0)}')"
            fi

            */

            // Change to correct working directory.
            Environment.CurrentDirectory = ewd;

            // Run specified command.
            try
            {
                var p = Process.Start(command.First(), command.Skip(1));
                p.WaitForExit();

                return p.ExitCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine ($"runinwsl: error executing command '{string.Join(' ', command)}':\r\n{ex.Message}");

                return 127;
            }
        }
    }
}



