using System.IO;
using System.Windows;
using System.Windows.Input;

namespace _8F
{
    public partial class SaveProfileDialog : Window
    {
        public string ProfileName { get; private set; } = string.Empty;

        public SaveProfileDialog(string defaultName = "", string title = "Save Configuration Profile to Database")
        {
            InitializeComponent();
            txtTitle.Text = title;
            txtProfileName.Text = defaultName ?? string.Empty;
            Loaded += (s, e) =>
            {
                txtProfileName.Focus();
                txtProfileName.SelectAll();
            };
        }

        public static bool ValidateProfileName(string? rawName, out string trimmedName, out string errorMessage)
        {
            trimmedName = (rawName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                errorMessage = "Please enter a valid profile name.";
                return false;
            }

            if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                errorMessage = "Profile name contains invalid characters (such as \\ / : * ? \" < > |). Please choose another name.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateProfileName(txtProfileName.Text, out string name, out string error))
            {
                MessageBox.Show(this, error, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtProfileName.Focus();
                txtProfileName.SelectAll();
                return;
            }

            ProfileName = name;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void txtProfileName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnSave_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                btnCancel_Click(sender, e);
            }
        }
    }
}
