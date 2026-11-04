using System.Windows;
using _8F.Models;

namespace _8F.Views
{
    public partial class ConfigChangeDetailsDialog : Window
    {
        public ConfigChangeDetailsDialog(ConfigChangeLogEntry entry)
        {
            InitializeComponent();

            string localTimeStr = entry.ChangedAt.ToLocalTime().ToString("dd-MMM-yyyy HH:mm:ss");
            string utcTimeStr = entry.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

            txtMetaLine1.Text = $"Timestamp: {localTimeStr} ({utcTimeStr})  |  Changed By: {entry.ChangedByRole}";
            txtMetaLine2.Text = $"Area: {entry.Area}" + (!string.IsNullOrWhiteSpace(entry.Channel) ? $"  |  Channel: {entry.Channel}" : "");
            txtSummary.Text = entry.Summary;
            txtDetails.Text = !string.IsNullOrWhiteSpace(entry.Details) ? entry.Details : "No additional raw field details recorded.";
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
