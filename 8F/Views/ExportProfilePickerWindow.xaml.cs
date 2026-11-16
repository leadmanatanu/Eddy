using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using _8F.Auth;
using _8F.Models;
using _8F.Services;
using _8F.ViewModels;

namespace _8F
{
    public partial class ExportProfilePickerWindow : Window
    {
        private readonly IConfigProfileRepository _repository;

        public ProfilePickerViewModel ViewModel { get; }

        public bool IsSelectionMode
        {
            get => ViewModel.IsSelectionMode;
            set => ViewModel.IsSelectionMode = value;
        }

        public bool AllowDelete
        {
            get => ViewModel.AllowDelete;
            set => ViewModel.AllowDelete = value;
        }

        public string HeaderTitle
        {
            get => ViewModel.HeaderTitle;
            set => ViewModel.HeaderTitle = value;
        }

        public int SelectedProfileId => ViewModel.SelectedProfileId;
        public string SelectedProfileName => ViewModel.SelectedProfileName;

        public Func<int?>? GetCurrentOpenProfileId { get; set; }
        public Action? OnOpenProfileDeleted { get; set; }

        public ExportProfilePickerWindow()
        {
            _repository = new InspectionLogRepository();
            ViewModel = new ProfilePickerViewModel(
                _repository,
                AppServices.ConfigChangeRepository,
                getCurrentOpenProfileId: () => GetCurrentOpenProfileId?.Invoke(),
                onOpenProfileDeleted: () => OnOpenProfileDeleted?.Invoke(),
                confirmDelete: (msg, title) =>
                    MessageBox.Show(this, msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes,
                showMessage: (msg, title, icon) =>
                    MessageBox.Show(this, msg, title, MessageBoxButton.OK, icon),
                onPrimaryActionAsync: PerformExportAsync,
                onCloseWindow: Close);

            InitializeComponent();
            DataContext = ViewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadProfilesAsync(selectFirstOnLoad: false);
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

        private void dgProfiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgProfiles.SelectedItem is ConfigProfileSummary && ViewModel.PrimaryCommand.CanExecute(null))
            {
                ViewModel.PrimaryCommand.Execute(null);
            }
        }

        private async Task PerformExportAsync(ConfigProfileSummary selectedProfile)
        {
            try
            {
                string safeProfileName = string.Concat(selectedProfile.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
                string timestampStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string defaultFileName = $"{safeProfileName}_{timestampStr}.json";

                Microsoft.Win32.SaveFileDialog saveDlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Configuration File",
                    FileName = defaultFileName,
                    DefaultExt = ".json",
                    Filter = "JSON Documents (*.json)|*.json|CSV Documents (*.csv)|*.csv|All Files (*.*)|*.*"
                };

                bool? dialogResult = saveDlg.ShowDialog(this);
                if (dialogResult != true)
                {
                    return;
                }

                string targetFilePath = saveDlg.FileName;
                ViewModel.StatusText = $"Fetching profile '{selectedProfile.Name}' from database...";

                var (channels, systemSettings) = await _repository.GetFullConfigProfileAsync(selectedProfile.Id);
                if (channels == null || channels.Count == 0)
                {
                    MessageBox.Show(this, "The selected configuration profile contains no channel data.", "Empty Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string fileContent;
                string fileExt = Path.GetExtension(targetFilePath).ToLowerInvariant();

                if (fileExt == ".json")
                {
                    fileContent = ExportToJson(channels, systemSettings, selectedProfile.Name);
                }
                else
                {
                    fileContent = ExportToCsv(channels);
                }

                File.WriteAllText(targetFilePath, fileContent, Encoding.UTF8);

                string successMsg = $"Configuration profile '{selectedProfile.Name}' successfully exported to:\n{targetFilePath}";
                ViewModel.StatusText = "Export completed successfully.";
                MessageBox.Show(this, successMsg, "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                Close();
            }
            catch (Exception ex)
            {
                ViewModel.StatusText = $"Export error: {ex.Message}";
                MessageBox.Show(this, $"Failed to export configuration profile: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ExportToCsv(List<ChannelData> channels)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Channel,Tx Strength,Frequency Number,Frequency Name,Frequency (Hz),Gain (dB),Phase (deg),Enabled,Strength,Post Gain,Center X (ex),Center Y (ey),Width,Height,Rotation Angle (deg),Overlay Center X (ex_O),Overlay Center Y (ey_O),Overlay Width,Overlay Height,Overlay Rotation Angle");

            foreach (var ch in channels)
            {
                if (ch.graphDatas == null) continue;
                foreach (var graph in ch.graphDatas)
                {
                    if (graph.ellipses != null && graph.ellipses.Count > 0)
                    {
                        foreach (var ell in graph.ellipses)
                        {
                            sb.AppendLine(FormattableString.Invariant($"{ch.Id},{ch.TxStrength},{graph.Id},\"{graph.Name}\",{graph.freq},{graph.gain},{graph.phase},{graph.isEnable},{graph.strength},{graph.postGain},{ell.ex},{ell.ey},{ell.width},{ell.height},{ell.angel},{graph.ex_O},{graph.ey_O},{graph.width_O},{graph.height_O},{graph.angel_O}"));
                        }
                    }
                    else
                    {
                        sb.AppendLine(FormattableString.Invariant($"{ch.Id},{ch.TxStrength},{graph.Id},\"{graph.Name}\",{graph.freq},{graph.gain},{graph.phase},{graph.isEnable},{graph.strength},{graph.postGain},{graph.ex},{graph.ey},{graph.width},{graph.height},{graph.angel},{graph.ex_O},{graph.ey_O},{graph.width_O},{graph.height_O},{graph.angel_O}"));
                    }
                }
            }

            return sb.ToString();
        }

        private static string ExportToJson(List<ChannelData> channels, SystemSettings? systemSettings, string profileName)
        {
            var container = new ProfileConfigFile
            {
                Version = 2,
                ProfileName = profileName,
                SystemConfig = systemSettings,
                Channels = channels
            };

            return JsonConvert.SerializeObject(container, Newtonsoft.Json.Formatting.Indented);
        }
    }
}
