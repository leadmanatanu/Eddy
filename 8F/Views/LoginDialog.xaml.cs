using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _8F.Auth;

namespace _8F.Views
{
    public partial class LoginDialog : Window
    {
        private readonly IAuthService _authService;
        private readonly int _maxAttempts;
        private int _failedAttempts = 0;

        public Role AuthenticatedRole { get; private set; } = Role.None;

        public LoginDialog(IAuthService authService)
        {
            InitializeComponent();
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            string? maxAttemptsStr = ConfigurationManager.AppSettings["MaxLoginAttempts"];
            _maxAttempts = int.TryParse(maxAttemptsStr, out int val) && val > 0 ? val : 3;

            Loaded += (s, e) => txtPassword.Focus();
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

        private async void btnOK_Click(object sender, RoutedEventArgs e)
        {
            await AttemptLoginAsync();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await AttemptLoginAsync();
            }
        }

        private async Task AttemptLoginAsync()
        {
            string enteredPassword = txtPassword.Password;
            if (string.IsNullOrEmpty(enteredPassword))
            {
                return;
            }

            btnOK.IsEnabled = false;
            btnCancel.IsEnabled = false;

            try
            {
                Role role = await _authService.AuthenticateAsync(enteredPassword);
                if (role != Role.None)
                {
                    AuthenticatedRole = role;
                    _authService.Session.SetSession(role);
                    DialogResult = true;
                    Close();
                    return;
                }

                // Failed attempt: 500 ms delay to blunt brute-force attempts
                await Task.Delay(500);
                _failedAttempts++;

                if (_failedAttempts >= _maxAttempts)
                {
                    await _authService.LogAuditAsync("LoginFailed", null, $"Max consecutive failed attempts ({_maxAttempts}) reached. Application exiting.");
                    MessageBox.Show("Too many failed login attempts. The application will now exit.", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    Close();
                    Application.Current.Shutdown();
                    return;
                }

                MessageBox.Show($"Invalid password. ({_maxAttempts - _failedAttempts} attempt(s) remaining)", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.SelectAll();
                txtPassword.Focus();
            }
            finally
            {
                btnOK.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }
    }
}
