using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels;

/// <summary>
/// ViewModel for Settings & Security management.
/// Supports PIN modification with envelope re-encryption, database backup, and restore.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISecurityService _securityService;
    private readonly IBackupService _backupService;

    public SettingsViewModel(
        ISecurityService securityService,
        IBackupService backupService)
    {
        _securityService = securityService;
        _backupService = backupService;

        DbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tablecreater.db");
    }

    // =========================================================================
    // PIN CHANGE FIELDS
    // =========================================================================

    [ObservableProperty]
    private string _oldPin = string.Empty;

    [ObservableProperty]
    private string _newPin = string.Empty;

    [ObservableProperty]
    private string _confirmNewPin = string.Empty;

    [ObservableProperty]
    private string? _pinErrorMessage;

    [ObservableProperty]
    private string? _pinSuccessMessage;

    // =========================================================================
    // BACKUP & RESTORE FIELDS
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<FileInfo> _existingBackups = new();

    [ObservableProperty]
    private string? _backupSuccessMessage;

    [ObservableProperty]
    private string? _backupErrorMessage;

    [ObservableProperty]
    private string _dbFilePath = string.Empty;

    public string BackupDirectoryPath => _backupService.BackupDirectory;

    public string EncryptionStatus => !string.IsNullOrEmpty(_securityService.ActiveDek)
        ? "🔒 Aktiv (AES-256-GCM + SQLCipher)"
        : "⚠️ Təhlükəsizlik Zərfi Yoxlanılır";

    // =========================================================================
    // COMMANDS
    // =========================================================================

    [RelayCommand]
    public void LoadSettings()
    {
        RefreshBackupList();
    }

    public void RefreshBackupList()
    {
        try
        {
            var backups = _backupService.GetExistingBackups();
            ExistingBackups = new ObservableCollection<FileInfo>(backups);
        }
        catch
        {
            // Ignore directory read issues
        }
    }

    [RelayCommand]
    public void ChangePin()
    {
        PinErrorMessage = null;
        PinSuccessMessage = null;

        if (string.IsNullOrWhiteSpace(OldPin))
        {
            PinErrorMessage = "Zəhmət olmasa cari təhlükəsizlik kodunu daxil edin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPin) || NewPin.Trim().Length < 4)
        {
            PinErrorMessage = "Yeni kod ən azı 4 simvoldan ibarət olmalıdır.";
            return;
        }

        if (NewPin.Trim() != ConfirmNewPin.Trim())
        {
            PinErrorMessage = "Yeni kod və təkrarı bir-birinə uyğun deyil!";
            return;
        }

        bool success = _securityService.ChangePin(OldPin.Trim(), NewPin.Trim());
        if (success)
        {
            PinSuccessMessage = "Təhlükəsizlik kodu uğurla yeniləndi! Bütün məlumatlar yeni kodla qorunur.";
            OldPin = string.Empty;
            NewPin = string.Empty;
            ConfirmNewPin = string.Empty;
        }
        else
        {
            PinErrorMessage = "Cari kod yanlışdır! Kod yenilənmədi.";
        }
    }

    [RelayCommand]
    public void CreateAutoBackup()
    {
        try
        {
            BackupErrorMessage = null;
            BackupSuccessMessage = null;

            string savedPath = _backupService.CreateBackup();
            RefreshBackupList();
            BackupSuccessMessage = $"Yedək nüsxəsi uğurla yaradıldı:\n{Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            BackupErrorMessage = $"Yedəkləmə xətası: {ex.Message}";
        }
    }

    public string CreateCustomBackup(string targetPath)
    {
        string result = _backupService.CreateBackup(targetPath);
        RefreshBackupList();
        return result;
    }

    public bool RestoreBackup(string sourcePath)
    {
        bool result = _backupService.RestoreBackup(sourcePath);
        RefreshBackupList();
        return result;
    }

    [RelayCommand]
    public void OpenBackupFolder()
    {
        try
        {
            if (!Directory.Exists(_backupService.BackupDirectory))
            {
                Directory.CreateDirectory(_backupService.BackupDirectory);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = _backupService.BackupDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            BackupErrorMessage = $"Qovluğu açmaq mümkün olmadı: {ex.Message}";
        }
    }
}
