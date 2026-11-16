using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using _8F.Auth;
using _8F.Repositories.Interfaces;
using _8F.Services;

namespace _8F.ViewModels
{
    public class ProfilePickerViewModel : BaseViewModel
    {
        private readonly IConfigProfileRepository _repository;
        private readonly IConfigChangeRepository _configChangeRepository;
        private readonly Func<int?>? _getCurrentOpenProfileId;
        private readonly Action? _onOpenProfileDeleted;
        private readonly Func<string, string, bool> _confirmDelete;
        private readonly Action<string, string, MessageBoxImage> _showMessage;
        private readonly Func<ConfigProfileSummary, Task>? _onPrimaryActionAsync;
        private readonly Action? _onCloseWindow;
        private readonly Func<bool> _isAdminCheck;

        private ConfigProfileSummary? _selectedProfile;
        private string _headerTitle = "Export Saved Configuration Profile";
        private string _statusText = "Loading saved configuration profiles from database...";
        private bool _isSelectionMode;
        private bool _allowDelete;
        private bool _isBusy;

        public ProfilePickerViewModel(
            IConfigProfileRepository repository,
            IConfigChangeRepository configChangeRepository,
            Func<int?>? getCurrentOpenProfileId = null,
            Action? onOpenProfileDeleted = null,
            Func<string, string, bool>? confirmDelete = null,
            Action<string, string, MessageBoxImage>? showMessage = null,
            Func<ConfigProfileSummary, Task>? onPrimaryActionAsync = null,
            Action? onCloseWindow = null,
            Func<bool>? isAdminCheck = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _configChangeRepository = configChangeRepository ?? throw new ArgumentNullException(nameof(configChangeRepository));
            _getCurrentOpenProfileId = getCurrentOpenProfileId;
            _onOpenProfileDeleted = onOpenProfileDeleted;
            _confirmDelete = confirmDelete ?? ((msg, title) =>
                MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
            _showMessage = showMessage ?? ((msg, title, icon) => MessageBox.Show(msg, title, MessageBoxButton.OK, icon));
            _onPrimaryActionAsync = onPrimaryActionAsync;
            _onCloseWindow = onCloseWindow;
            _isAdminCheck = isAdminCheck ?? (() =>
                AppServices.SessionContext.CurrentRole == Role.Admin &&
                AppServices.AuthService.Can(Permission.FileOperations));

            PrimaryCommand = new RelayCommand(() => _ = ExecutePrimaryAsync(), CanExecutePrimary);
            DeleteCommand = new RelayCommand(() => _ = ExecuteDeleteAsync(), CanExecuteDelete);
            CancelCommand = new RelayCommand(() => { ResetSelectionResult(); _onCloseWindow?.Invoke(); });
        }

        public ObservableCollection<ConfigProfileSummary> Profiles { get; } = new();

        public ConfigProfileSummary? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public string HeaderTitle
        {
            get => _headerTitle;
            set => SetProperty(ref _headerTitle, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            set
            {
                if (SetProperty(ref _isSelectionMode, value))
                {
                    OnPropertyChanged(nameof(PrimaryButtonText));
                }
            }
        }

        public bool AllowDelete
        {
            get => _allowDelete;
            set
            {
                if (SetProperty(ref _allowDelete, value))
                {
                    OnPropertyChanged(nameof(DeleteButtonVisibility));
                    RaiseCommandStates();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public bool IsAdmin => _isAdminCheck();

        public string PrimaryButtonText => IsSelectionMode ? "Select Profile" : "Export Profile";

        public Visibility DeleteButtonVisibility => AllowDelete ? Visibility.Visible : Visibility.Collapsed;

        public int SelectedProfileId { get; private set; }
        public string SelectedProfileName { get; private set; } = string.Empty;

        public RelayCommand PrimaryCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }

        public bool CanExecutePrimary() => !IsBusy && SelectedProfile != null;

        public bool CanExecuteDelete() => !IsBusy && AllowDelete && IsAdmin && SelectedProfile != null;

        public async Task LoadProfilesAsync(bool selectFirstOnLoad = false)
        {
            IsBusy = true;
            try
            {
                StatusText = "Loading saved configuration profiles from database...";
                var profiles = await _repository.ListConfigProfilesAsync();

                Profiles.Clear();
                if (profiles != null)
                {
                    foreach (var profile in profiles)
                    {
                        Profiles.Add(profile);
                    }
                }

                if (Profiles.Count == 0)
                {
                    SelectedProfile = null;
                    StatusText = "No saved profiles found.";
                }
                else
                {
                    SelectedProfile = selectFirstOnLoad ? Profiles[0] : null;
                    StatusText = $"Found {Profiles.Count} saved profile(s). Select a profile and click {(IsSelectionMode ? "Select Profile" : "Export")}.";
                }
            }
            catch (Exception ex)
            {
                Profiles.Clear();
                SelectedProfile = null;
                StatusText = $"Error connecting to database: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                RaiseCommandStates();
            }
        }

        public async Task ExecutePrimaryAsync()
        {
            if (IsBusy) return;
            if (SelectedProfile is not ConfigProfileSummary selected)
            {
                _showMessage("Please select a profile from the list.", "Selection Required", MessageBoxImage.Warning);
                return;
            }

            if (IsSelectionMode)
            {
                SelectedProfileId = selected.Id;
                SelectedProfileName = selected.Name;
                _onCloseWindow?.Invoke();
                return;
            }

            if (_onPrimaryActionAsync != null)
            {
                IsBusy = true;
                try
                {
                    await _onPrimaryActionAsync(selected);
                }
                finally
                {
                    IsBusy = false;
                    RaiseCommandStates();
                }
            }
        }

        public async Task ExecuteDeleteAsync()
        {
            if (IsBusy) return;

            if (!AllowDelete)
            {
                _showMessage("Deleting profiles is not available in this view.", "Action Not Allowed", MessageBoxImage.Warning);
                return;
            }

            if (!IsAdmin)
            {
                _showMessage("Admin access is required to delete configuration profiles.", "Access Denied", MessageBoxImage.Warning);
                return;
            }

            if (SelectedProfile is not ConfigProfileSummary target)
            {
                return;
            }

            bool isCurrentlyOpen = _getCurrentOpenProfileId?.Invoke() == target.Id;
            string confirmMsg = $"Delete profile '{target.Name}'? This cannot be undone.";
            if (isCurrentlyOpen)
            {
                confirmMsg += "\n\nThis profile is currently open. Deleting it will remove the saved copy from the database without altering the live running configuration.";
            }

            if (!_confirmDelete(confirmMsg, "Confirm Delete"))
            {
                return;
            }

            IsBusy = true;
            StatusText = $"Deleting profile '{target.Name}'...";
            try
            {
                bool deleted = await _repository.DeleteProfileAsync(target.Id);
                if (deleted)
                {
                    if (isCurrentlyOpen)
                    {
                        _onOpenProfileDeleted?.Invoke();
                    }

                    string detailsJson = JsonSerializer.Serialize(new
                    {
                        ProfileId = target.Id,
                        ProfileName = target.Name,
                        DeletedAtUtc = DateTime.UtcNow
                    });
                    await _configChangeRepository.RecordChangeAsync(
                        "Delete profile",
                        null,
                        $"Deleted profile '{target.Name}' (ID: {target.Id}) at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                        detailsJson);
                }
                else
                {
                    if (isCurrentlyOpen)
                    {
                        _onOpenProfileDeleted?.Invoke();
                    }

                    _showMessage(
                        $"Profile '{target.Name}' (ID: {target.Id}) was already deleted or no longer exists.",
                        "Profile Not Found",
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _showMessage(
                    $"Failed to delete profile '{target.Name}': {ex.Message}",
                    "Delete Error",
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                await LoadProfilesAsync(selectFirstOnLoad: false);
            }
        }

        public void ResetSelectionResult()
        {
            SelectedProfileId = 0;
            SelectedProfileName = string.Empty;
        }

        private void RaiseCommandStates()
        {
            PrimaryCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
        }
    }
}
