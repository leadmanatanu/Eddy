using System;
using System.Windows;
using System.Windows.Input;
using _8F.Auth;

namespace _8F.Views
{
    public partial class FirstRunSetupDialog : Window
    {
        private readonly IAuthService _authService;

        public FirstRunSetupDialog(IAuthService authService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            Loaded += (s, e) => txtAdminPassword.Focus();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string adminPw = txtAdminPassword.Password;
            string confirmAdmin = txtConfirmAdminPassword.Password;
            string opPw = txtOperatorPassword.Password;
            string confirmOp = txtConfirmOperatorPassword.Password;

            if (string.IsNullOrWhiteSpace(adminPw) || adminPw.Length < 6)
            {
                MessageBox.Show("Admin password must be at least 6 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtAdminPassword.Focus();
                return;
            }

            if (adminPw != confirmAdmin)
            {
                MessageBox.Show("Admin passwords do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtConfirmAdminPassword.SelectAll();
                txtConfirmAdminPassword.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(opPw) || opPw.Length < 6)
            {
                MessageBox.Show("Operator password must be at least 6 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtOperatorPassword.Focus();
                return;
            }

            if (opPw != confirmOp)
            {
                MessageBox.Show("Operator passwords do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtConfirmOperatorPassword.SelectAll();
                txtConfirmOperatorPassword.Focus();
                return;
            }

            if (adminPw == opPw)
            {
                MessageBox.Show("Admin and Operator passwords must be different.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtOperatorPassword.SelectAll();
                txtOperatorPassword.Focus();
                return;
            }

            btnSave.IsEnabled = false;
            btnExit.IsEnabled = false;

            try
            {
                await _authService.SetInitialPasswordsAsync(adminPw, opPw);
                MessageBox.Show("Initial credentials saved successfully! You may now log in.", "Setup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save credentials: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                btnSave.IsEnabled = true;
                btnExit.IsEnabled = true;
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
