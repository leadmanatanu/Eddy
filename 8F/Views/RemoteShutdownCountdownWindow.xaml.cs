using System;
using System.Windows;
using System.Windows.Threading;

namespace _8F.Views
{
    public partial class RemoteShutdownCountdownWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private int _secondsRemaining;

        public RemoteShutdownCountdownWindow(int seconds = 15)
        {
            InitializeComponent();
            _secondsRemaining = seconds > 0 ? seconds : 15;
            UpdateCountdownLabel();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _secondsRemaining--;
            if (_secondsRemaining <= 0)
            {
                _timer.Stop();
                try
                {
                    DialogResult = true;
                }
                catch (InvalidOperationException) { }
                Close();
            }
            else
            {
                UpdateCountdownLabel();
            }
        }

        private void UpdateCountdownLabel()
        {
            lblCountdown.Text = $"Shutting down in {_secondsRemaining} seconds";
        }

        public void Cancel()
        {
            _timer.Stop();
            try
            {
                DialogResult = false;
            }
            catch (InvalidOperationException) { }
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }
    }
}
