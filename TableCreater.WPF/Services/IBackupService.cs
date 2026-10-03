using System.IO;

namespace TableCreater.WPF.Services;

/// <summary>
/// Service interface for automated and manual database backups and restores.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Gets the default directory where automated backups are stored.
    /// </summary>
    string BackupDirectory { get; }

    /// <summary>
    /// Creates a backup of the current database file to the specified path or timestamped file in BackupDirectory.
    /// Returns the full path of the created backup file.
    /// </summary>
    string CreateBackup(string? targetFilePath = null);

    /// <summary>
    /// Restores database from the specified backup file.
    /// Creates a pre-restore backup first to guarantee zero accidental data loss.
    /// </summary>
    bool RestoreBackup(string sourceFilePath);

    /// <summary>
    /// Returns a list of all existing backup files in the default backup folder, sorted by date descending.
    /// </summary>
    List<FileInfo> GetExistingBackups();
}
