using System;
using System.IO;
using System.Xml.Linq;
using System.Threading;

namespace AutoBackup
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                RunBackup();
                Console.WriteLine("Operation completed successfully. Press any key to exit or wait 1 minute...");
                PauseBeforeExit();
            }
            catch (Exception ex)
            {
                File.WriteAllText("error.log", ex.ToString());
                Console.WriteLine($"Unexpected error: {ex.Message}. See error.log for details.");
                Console.WriteLine("Press any key to exit or wait 1 minute...");
                PauseBeforeExit();
            }
        }

        static void RunBackup()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(exeDir, "config.xml");
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Config file not found: {configPath}");
                throw new FileNotFoundException("Config file missing", configPath);
            }

            var xml = XDocument.Load(configPath).Root;
            if (xml == null)
                throw new Exception($"Error in config file, cannot load: {configPath}");

            var workingDir = xml.Element("SourceDirectory")?.Value
                             ?? throw new Exception("Missing SourceDirectory in config");
            var backupDir = xml.Element("BackupDirectory")?.Value
                            ?? throw new Exception("Missing BackupDirectory in config");
            var workingDays = int.Parse(xml.Element("WorkingRetentionDays")?.Value
                                   ?? throw new Exception("Missing WorkingRetentionDays in config"));
            var backupDays = int.Parse(xml.Element("BackupRetentionDays")?.Value
                                  ?? throw new Exception("Missing BackupRetentionDays in config"));

            ProcessDirectory(backupDir, backupDays, "Backup");
            ProcessDirectory(workingDir, workingDays, "Working");
            MoveFiles(workingDir, backupDir);
        }

        static void ProcessDirectory(string path, int retentionDays, string label)
        {
            Console.WriteLine($"Processing {label} directory: {path}");
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory not found: {path}");
                return;
            }

            var threshold = DateTime.Now.AddDays(-retentionDays);
            foreach (var file in Directory.GetFiles(path))
            {
                var lastWrite = File.GetLastWriteTime(file);
                if (lastWrite < threshold)
                {
                    Console.WriteLine($"Deleting {label} file older than {retentionDays} days: {file}");
                    try { File.Delete(file); }
                    catch (Exception ex) { Console.WriteLine($"Failed to delete {file}: {ex.Message}"); }
                }
            }
        }

        static void MoveFiles(string sourceDir, string targetDir)
        {
            Console.WriteLine($"Moving files from Working to Backup: {sourceDir} -> {targetDir}");
            if (!Directory.Exists(sourceDir) || !Directory.Exists(targetDir))
                throw new DirectoryNotFoundException("Source or target directory does not exist.");

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(targetDir, fileName);

                if (File.Exists(destPath))
                {
                    Console.WriteLine($"File {fileName} already exists on backup, skipping...");
                    continue;
                }

                Console.WriteLine($"Moving file: {file} -> {destPath}");
                try { File.Move(file, destPath); }
                catch (Exception ex) { Console.WriteLine($"Failed to move {file}: {ex.Message}"); }
            }
        }

        static void PauseBeforeExit()
        {
            const int totalSeconds = 60;
            for (int i = 0; i < totalSeconds; i++)
            {
                if (Console.KeyAvailable) { Console.ReadKey(intercept: true); break; }
                Thread.Sleep(1000);
            }
        }
    }
}
