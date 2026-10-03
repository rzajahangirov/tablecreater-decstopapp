using System.Windows;
using System.Windows.Input;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.Views.Windows;

public partial class LoginWindow : Window
{
    private readonly ISecurityService _securityService;
    private bool _isPasswordVisible = false;

    public LoginWindow(ISecurityService securityService)
    {
        _securityService = securityService;
        InitializeComponent();

        Loaded += (s, e) => TxtPassword.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        AttemptLogin();
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AttemptLogin();
        }
    }

    private void AttemptLogin()
    {
        string pin = _isPasswordVisible ? TxtVisiblePassword.Text : TxtPassword.Password;

        if (string.IsNullOrWhiteSpace(pin))
        {
            ShowError("Zəhmət olmasa təhlükəsizlik kodunu daxil edin.");
            return;
        }

        bool success = _securityService.VerifyAndUnlock(pin.Trim());
        if (success)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError("Daxil edilən kod yanlışdır!");
            if (_isPasswordVisible)
            {
                TxtVisiblePassword.SelectAll();
                TxtVisiblePassword.Focus();
            }
            else
            {
                TxtPassword.SelectAll();
                TxtPassword.Focus();
            }
        }
    }

    private void ShowError(string message)
    {
        TxtError.Text = message;
        TxtError.Visibility = Visibility.Visible;
    }

    private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        if (_isPasswordVisible)
        {
            TxtVisiblePassword.Text = TxtPassword.Password;
            TxtPassword.Visibility = Visibility.Collapsed;
            TxtVisiblePassword.Visibility = Visibility.Visible;
            IconTogglePassword.Text = "\uED1A"; // Eye closed or slash
            TxtVisiblePassword.Focus();
            TxtVisiblePassword.CaretIndex = TxtVisiblePassword.Text.Length;
        }
        else
        {
            TxtPassword.Password = TxtVisiblePassword.Text;
            TxtVisiblePassword.Visibility = Visibility.Collapsed;
            TxtPassword.Visibility = Visibility.Visible;
            IconTogglePassword.Text = "\uE7B3"; // Eye open
            TxtPassword.Focus();
        }
    }
}
