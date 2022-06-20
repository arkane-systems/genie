using System;
using System.Diagnostics;
using System.Linq;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie.RunInWsl
{
    public static class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine("runwsl: error in usage; should only be called by genie");
            Console.WriteLine("runwsl <ewd> <command line>");
        }

        /// <summary>
        /// Entry point.
        /// </summary>
        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return 127;
            }

            // Split args into executable working directory and command.
            var ewd = args[0];
            var command = args.Skip(1);

            // Change to correct working directory.
            Environment.CurrentDirectory = ewd;

            // Run specified command.
            var enumerable = command.ToList();
            try
            {
                var p = Process.Start(enumerable.First(), enumerable.Skip(1));
                p.WaitForExit();

                return p.ExitCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"runinwsl: error executing command '{string.Join(' ', enumerable)}':\r\n{ex.Message}");

                return 127;
            }
        }
    }
}


