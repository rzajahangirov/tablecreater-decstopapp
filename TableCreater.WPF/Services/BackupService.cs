using System.IO;

namespace TableCreater.WPF.Services;

/// <summary>
/// Professional implementation of automated and manual database backups and restores.
/// Protects against data corruption by keeping safety snapshots before every restore.
/// </summary>
public class BackupService : IBackupService
{
    private readonly string _dbFilePath;
    private readonly string _backupDirectory;

    public BackupService()
    {
        _dbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tablecreater.db");
        _backupDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");

        if (!Directory.Exists(_backupDirectory))
        {
            Directory.CreateDirectory(_backupDirectory);
        }

        // Migrate any existing backups from LocalApplicationData
        try
        {
            var oldBackups = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TableCreater", "Backups");

            if (Directory.Exists(oldBackups))
            {
                foreach (var file in Directory.GetFiles(oldBackups))
                {
                    var dest = Path.Combine(_backupDirectory, Path.GetFileName(file));
                    if (!File.Exists(dest))
                        File.Copy(file, dest, overwrite: true);
                }
            }
        }
        catch { }
    }

    public string BackupDirectory => _backupDirectory;

    public string CreateBackup(string? targetFilePath = null)
    {
        if (!File.Exists(_dbFilePath))
        {
            throw new FileNotFoundException("Məlumat bazası faylı tapılmadı.", _dbFilePath);
        }

        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            targetFilePath = Path.Combine(_backupDirectory, $"tablecreater_backup_{timestamp}.db");
        }

        // Ensure directory of target exists
        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Copy file with sharing enabled
        File.Copy(_dbFilePath, targetFilePath, overwrite: true);

        // Also back up security.json companion (check app dir first, then AppData)
        var securityFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "security.json");
        if (!File.Exists(securityFile))
        {
            securityFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TableCreater", "security.json");
        }

        if (File.Exists(securityFile))
        {
            string secBackup = Path.ChangeExtension(targetFilePath, ".security.json");
            File.Copy(securityFile, secBackup, overwrite: true);
        }

        return targetFilePath;
    }

    public bool RestoreBackup(string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Seçilmiş nüsxə faylı tapılmadı.", sourceFilePath);
        }

        // 1. Create a pre-restore safety copy of current db
        if (File.Exists(_dbFilePath))
        {
            string preRestoreBak = _dbFilePath + $".pre_restore_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            File.Copy(_dbFilePath, preRestoreBak, overwrite: true);
        }

        // 2. Overwrite db with the selected backup
        File.Copy(sourceFilePath, _dbFilePath, overwrite: true);

        // 3. Restore companion security.json if it exists alongside the backup
        string companionSecurity = Path.ChangeExtension(sourceFilePath, ".security.json");
        if (File.Exists(companionSecurity))
        {
            var targetSecurity = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "security.json");
            File.Copy(companionSecurity, targetSecurity, overwrite: true);
        }

        return true;
    }

    public List<FileInfo> GetExistingBackups()
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return new List<FileInfo>();
        }

        var dirInfo = new DirectoryInfo(_backupDirectory);
        return dirInfo.GetFiles("*.db")
            .OrderByDescending(f => f.CreationTime)
            .ToList();
    }
}
