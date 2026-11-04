using System.Windows;
using _8F.Models;

namespace _8F.Views
{
    public partial class LastChangeNoticeDialog : Window
    {
        public LastChangeNoticeDialog(ConfigChangeLogEntry? entry)
        {
            InitializeComponent();

            if (entry == null)
            {
                txtHeaderLine.Text = "Configuration Status:";
                txtAreaLine.Text = "No changes found.";
                txtSummary.Text = "No configuration changes recorded.";
            }
            else
            {
                string localTimeStr = entry.ChangedAt.ToLocalTime().ToString("dd-MMM-yyyy HH:mm");
                txtHeaderLine.Text = $"Configuration last changed by {entry.ChangedByRole} on {localTimeStr}:";
                txtAreaLine.Text = $"Area: {entry.Area}" + (!string.IsNullOrWhiteSpace(entry.Channel) ? $" ({entry.Channel})" : "");
                txtSummary.Text = entry.Summary;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
