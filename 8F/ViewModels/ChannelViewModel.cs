using System.Windows.Media;

namespace _8F.ViewModels
{
    public class ChannelViewModel : BaseViewModel
    {
        private int _channelId = 1;
        public int ChannelId
        {
            get => _channelId;
            set => SetProperty(ref _channelId, value);
        }

        private string _channelName = "Channel-1";
        public string ChannelName
        {
            get => _channelName;
            set => SetProperty(ref _channelName, value);
        }

        private bool _isActive = false;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                {
                    OnPropertyChanged(nameof(HeaderBrush));
                }
            }
        }

        private bool _isMaximized = false;
        public bool IsMaximized
        {
            get => _isMaximized;
            set
            {
                if (SetProperty(ref _isMaximized, value))
                {
                    OnPropertyChanged(nameof(MaximizeIcon));
                    OnPropertyChanged(nameof(MaximizeToolTip));
                }
            }
        }

        public string MaximizeIcon => IsMaximized ? "\uE73F" : "\uE740"; // Restore : Maximize
        public string MaximizeToolTip => IsMaximized ? "Restore" : "Maximize";

        public Brush HeaderBrush => IsActive
            ? new SolidColorBrush(Color.FromRgb(46, 125, 50)) // Active Green (#2E7D32)
            : new SolidColorBrush(Color.FromRgb(66, 66, 66)); // Inactive Grey (#424242)

        private int _totalCount = 0;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                    OnPropertyChanged(nameof(TotalCountText));
            }
        }

        private int _okCount = 0;
        public int OkCount
        {
            get => _okCount;
            set
            {
                if (SetProperty(ref _okCount, value))
                    OnPropertyChanged(nameof(OkCountText));
            }
        }

        private int _notOkCount = 0;
        public int NotOkCount
        {
            get => _notOkCount;
            set
            {
                if (SetProperty(ref _notOkCount, value))
                    OnPropertyChanged(nameof(NotOkCountText));
            }
        }

        public string TotalCountText => $"Total - {TotalCount}";
        public string OkCountText => $"OK - {OkCount}";
        public string NotOkCountText => $"Not OK - {NotOkCount}";

        private Brush _overallResultBrush = new SolidColorBrush(Color.FromRgb(48, 83, 113));
        public Brush OverallResultBrush
        {
            get => _overallResultBrush;
            set => SetProperty(ref _overallResultBrush, value);
        }

        private bool _isLogging = false;
        public bool IsLogging
        {
            get => _isLogging;
            set
            {
                if (SetProperty(ref _isLogging, value))
                    OnPropertyChanged(nameof(LogButtonText));
            }
        }

        public string LogButtonText => IsLogging ? "Stop Log" : "Start Log";

        private bool _showHiddenChannelChip = false;
        public bool ShowHiddenChannelChip
        {
            get => _showHiddenChannelChip;
            set => SetProperty(ref _showHiddenChannelChip, value);
        }

        private string _hiddenChannelText = "";
        public string HiddenChannelText
        {
            get => _hiddenChannelText;
            set => SetProperty(ref _hiddenChannelText, value);
        }

        private Brush _hiddenChannelBrush = new SolidColorBrush(Color.FromRgb(66, 66, 66));
        public Brush HiddenChannelBrush
        {
            get => _hiddenChannelBrush;
            set => SetProperty(ref _hiddenChannelBrush, value);
        }
    }
}
