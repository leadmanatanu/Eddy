using ConfigurationKeyGenerator.Models;
using ConfigurationKeyGenerator.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ConfigurationKeyGenerator.Views
{
    public partial class LicenseList : Page
    {
        // Full, unfiltered set of logs loaded from the service.
        // The grid always binds to a filtered view of this list.
        private List<ConfigurationKeyLog> allLogs = new List<ConfigurationKeyLog>();

        public LicenseList()
        {
            InitializeComponent();
            LoadConfigurationLogs();
        }

        private void LoadConfigurationLogs()
        {
            try
            {
                ConfigurationKeyLogService service = new ConfigurationKeyLogService();

                allLogs = service.GetAll()?.ToList() ?? new List<ConfigurationKeyLog>();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddLicense_Click(object sender, RoutedEventArgs e)
        {
            AddLicense window =
                new AddLicense();
            window.Owner =
                Window.GetWindow(this);
            bool? result =
                window.ShowDialog();
            if (result == true)
            {
                LoadConfigurationLogs();
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            Button button =
                sender as Button;
            if (button == null)
                return;
            ConfigurationKeyLog log =
                button.DataContext as ConfigurationKeyLog;
            if (log == null)
                return;
            SaveFileDialog dialog =
                new SaveFileDialog
                {
                    Title = "Save Configuration File",
                    FileName = Path.GetFileNameWithoutExtension(log.GeneratedFileName) + ".txt",
                    Filter = "Text File (*.txt)|*.txt"
                };
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllBytes(
                    dialog.FileName,
                    log.GeneratedFile);
                MessageBox.Show(
                    "Downloaded successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Hide the floating placeholder as soon as the user types
            txtSearchPlaceholder.Visibility = string.IsNullOrEmpty(txtSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyFilter();
        }

        /// <summary>
        /// Filters allLogs by the current search text (matching Product,
        /// Customer Name, or Machine ID), updates the grid, the record
        /// count label, and toggles the empty-state placeholder.
        /// </summary>
        private void ApplyFilter()
        {
            string search = txtSearch?.Text?.Trim() ?? string.Empty;

            IEnumerable<ConfigurationKeyLog> filtered = allLogs;

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = allLogs.Where(log =>
                    (log.ProductName != null && log.ProductName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (log.CustomerName != null && log.CustomerName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (log.MachineId != null && log.MachineId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            List<ConfigurationKeyLog> result = filtered.ToList();

            licenseGrid.ItemsSource = result;


            // Empty state only shows when there truly are no records at all
            // (not just because the search filtered everything out) —
            // but we still want feedback for a filtered-to-zero search too.
            if (allLogs.Count == 0)
            {
                txtEmptyTitle.Text = "No configuration licenses yet";
                txtEmptySubtitle.Text = "Generated licenses will appear here.";
                panelEmptyState.Visibility = Visibility.Visible;
                licenseGrid.Visibility = Visibility.Collapsed;
            }
            else if (result.Count == 0)
            {
                txtEmptyTitle.Text = "No matching licenses";
                txtEmptySubtitle.Text = "Try a different search term.";
                panelEmptyState.Visibility = Visibility.Visible;
                licenseGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                panelEmptyState.Visibility = Visibility.Collapsed;
                licenseGrid.Visibility = Visibility.Visible;
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
                return;

            ConfigurationKeyLog log =
                button.DataContext as ConfigurationKeyLog;

            if (log == null)
                return;

            AddLicense window = new AddLicense(log);

            window.Owner = Window.GetWindow(this);

            bool? result = window.ShowDialog();

            if (result == true)
            {
                LoadConfigurationLogs();
            }
        }
    }
}
