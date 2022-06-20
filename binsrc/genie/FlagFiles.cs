using System.IO;

namespace ArkaneSystems.WindowsSubsystemForLinux.Genie
{
    internal static class FlagFiles
    {
        private const string StartupFileName = "/run/genie.startup";

        private const string ShutdownFileName = "/run/genie.shutdown";

        private const string RunFileName = "/run/genie.up";

        public static bool StartupFile
        {
            get => TestFile(StartupFileName);
            set
            {
                if (value)
                    CreateFile(StartupFileName);
                else
                    DeleteFile(StartupFileName);
            }
        }

        public static bool ShutdownFile
        {
            get => TestFile(ShutdownFileName);
            set
            {
                if (value)
                    CreateFile(ShutdownFileName);
                else
                    DeleteFile(ShutdownFileName);
            }
        }

        public static bool RunFile
        {
            get => TestFile(RunFileName);
            set
            {
                if (value)
                    CreateFile(RunFileName);
                else
                    DeleteFile(RunFileName);
            }
        }

        #region Helper methods

        private static void CreateFile(string path) => File.Create(path).Close();

        private static void DeleteFile(string path) => File.Delete(path);

        private static bool TestFile(string path) => File.Exists(path);

        #endregion
    }
}