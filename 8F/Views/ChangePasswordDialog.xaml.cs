using System;
using System.Windows;
using System.Windows.Input;
using _8F.Auth;

namespace _8F.Views
{
    public partial class ChangePasswordDialog : Window
    {
        private readonly IAuthService _authService;
        private readonly Role _currentRole;

        public ChangePasswordDialog(IAuthService authService)
        {
            InitializeComponent();
            Loaded += (s, e) => TargetRole_Changed(this, new RoutedEventArgs());
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _currentRole = _authService.Session.CurrentRole;

            if (_currentRole == Role.Admin)
            {
                pnlTargetSelection.Visibility = Visibility.Visible;
            }
            else
            {
                pnlTargetSelection.Visibility = Visibility.Collapsed;
            }

            Loaded += (s, e) =>
            {
                if (pnlCurrentPassword.Visibility == Visibility.Visible)
                {
                    txtCurrentPassword.Focus();
                }
                else
                {
                    txtNewPassword.Focus();
                }
            };
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

        private void TargetRole_Changed(object sender, RoutedEventArgs e)
        {
            // Fires during InitializeComponent before all controls exist
            if (!IsInitialized || pnlCurrentPassword == null ||
                txtCurrentPassword == null || txtNewPassword == null ||
                rbOperatorPassword == null)
                return;

            bool resetOperator = rbOperatorPassword.IsChecked == true;
            pnlCurrentPassword.Visibility = resetOperator ? Visibility.Collapsed : Visibility.Visible;

            if (resetOperator) txtNewPassword.Focus();
            else txtCurrentPassword.Focus();
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            string newPw = txtNewPassword.Password;
            string confirmPw = txtConfirmPassword.Password;

            if (string.IsNullOrWhiteSpace(newPw) || newPw.Length < 6)
            {
                MessageBox.Show("New password must be at least 6 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNewPassword.Focus();
                return;
            }

            if (newPw != confirmPw)
            {
                MessageBox.Show("New password and confirmation do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtConfirmPassword.SelectAll();
                txtConfirmPassword.Focus();
                return;
            }

            btnSave.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                if (_currentRole == Role.Admin && rbOperatorPassword.IsChecked == true)
                {
                    // Admin resetting Operator password
                    var (success, errorMsg) = await _authService.ResetOperatorPasswordAsync(newPw);
                    if (success)
                    {
                        MessageBox.Show("Operator password has been reset successfully.", "Password Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                        return;
                    }

                    MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    // Changing own password (Admin or Operator)
                    string currentPw = txtCurrentPassword.Password;
                    if (string.IsNullOrEmpty(currentPw))
                    {
                        MessageBox.Show("Please enter your current password.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        txtCurrentPassword.Focus();
                        return;
                    }

                    var (success, errorMsg) = await _authService.ChangeOwnPasswordAsync(_currentRole, currentPw, newPw);
                    if (success)
                    {
                        MessageBox.Show("Your password has been changed successfully.", "Password Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                        return;
                    }

                    MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to change password: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSave.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
