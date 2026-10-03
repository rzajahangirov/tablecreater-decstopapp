using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TableCreater.WPF.ViewModels;

namespace TableCreater.WPF.Views.Pages;

/// <summary>
/// Code-behind for TransactionDetailPage.
/// Handles navigation and document opening.
/// </summary>
public partial class TransactionDetailPage : UserControl
{
    public TransactionDetailPage()
    {
        InitializeComponent();
    }

    private TransactionDetailViewModel? ViewModel => DataContext as TransactionDetailViewModel;

    /// <summary>
    /// Navigate back to the Customer Details page.
    /// </summary>
    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToCustomerDetails(ViewModel.CustomerId);
        }
    }

    /// <summary>
    /// Open attached document file using the system default app.
    /// </summary>
    private void BtnOpenDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string documentPath &&
            !string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = documentPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sənədi açmaq mümkün olmadı: {ex.Message}",
                    "Xəta", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
