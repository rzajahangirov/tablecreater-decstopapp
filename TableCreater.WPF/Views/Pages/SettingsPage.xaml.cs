using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TableCreater.WPF.ViewModels;

namespace TableCreater.WPF.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    private void PinInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        ViewModel.OldPin = TxtOldPin.Password;
        ViewModel.NewPin = TxtNewPin.Password;
        ViewModel.ConfirmNewPin = TxtConfirmPin.Password;
    }

    private void BtnChangePin_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        ViewModel.OldPin = TxtOldPin.Password;
        ViewModel.NewPin = TxtNewPin.Password;
        ViewModel.ConfirmNewPin = TxtConfirmPin.Password;

        ViewModel.ChangePinCommand.Execute(null);

        if (string.IsNullOrEmpty(ViewModel.PinErrorMessage))
        {
            TxtOldPin.Password = string.Empty;
            TxtNewPin.Password = string.Empty;
            TxtConfirmPin.Password = string.Empty;
        }
    }

    private void BtnBackupAs_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Məlumat Bazasının Nüsxəsini Saxla",
            Filter = "SQLite Baza Faylı (*.db)|*.db|Bütün Fayllar (*.*)|*.*",
            FileName = $"tablecreater_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                ViewModel.CreateCustomBackup(sfd.FileName);
                MessageBox.Show(
                    $"Yedək nüsxəsi uğurla saxlanıldı:\n{sfd.FileName}",
                    "Yedəkləmə Uğurlu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Yedəkləmə zamanı xəta baş verdi:\n{ex.Message}",
                    "Xəta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var ofd = new OpenFileDialog
        {
            Title = "Bərpa Ediləcək Baza Faylını Seçin",
            Filter = "SQLite Baza Faylı (*.db)|*.db|Bütün Fayllar (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            var confirmResult = MessageBox.Show(
                "DİQQƏT: Məlumat bazasını bərpa etdikdə cari bütün məlumatlar seçilən nüsxə ilə əvəzlənəcək.\n\nƏməliyyatdan öncə mövcud bazanın təhlükəsizlik surəti avtomatik saxlanılacaq.\n\nDavam etmək istəyirsiniz?",
                "Bazanın Bərpasını Təsdiqlə",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult == MessageBoxResult.Yes)
            {
                try
                {
                    ViewModel.RestoreBackup(ofd.FileName);
                    MessageBox.Show(
                        "Məlumat bazası uğurla bərpa edildi!\n\nDəyişikliklərin tam qüvvəyə minməsi üçün tətbiq bağlanacaq. Zəhmət olmasa tətbiqi yenidən başladın.",
                        "Bərpa Uğurla Tamamlandı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Clean shutdown so SQLite unlocks
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Bərpa zamanı xəta baş verdi:\n{ex.Message}",
                        "Xəta",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }
}
