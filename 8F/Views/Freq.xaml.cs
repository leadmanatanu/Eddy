using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using _8F.Services;
using _8F.ViewModels;

namespace _8F
{
    public partial class Freq : Window
    {
        public FreqViewModel ViewModel { get; }

        public bool IsSaved
        {
            get => ViewModel.IsSaved;
            set => ViewModel.IsSaved = value;
        }

        public DeviceCOM? portCOM
        {
            get => ViewModel.PortCOM;
            set => ViewModel.PortCOM = value;
        }

        public Freq()
        {
            InitializeComponent();
            ViewModel = new FreqViewModel();
            ViewModel.CloseAction = Close;
            DataContext = ViewModel;

            Loaded += (s, e) =>
            {
                var selCh = DeviceCOM.channelDatas?.FirstOrDefault(c => c.IsSeleted);
                int chId = selCh?.Id ?? 1;
                lblTitle.Content = $"System Configuration - Channel-{chId}";
                Title = $"System Configuration - Channel-{chId}";

                ViewModel.OwnerWindow = Owner;
                lblTxStrength.Visibility = ViewModel.TxStrengthVisibility;
                txtTxStrength.Visibility = ViewModel.TxStrengthVisibility;
                gdFreq.ItemsSource = ViewModel.GraphDataList;
                txtTxStrength.Value = ViewModel.TxStrengthValue;
            };
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CloseCommand.Execute(null);
        }

        private void btnConfigSave_Click(object sender, RoutedEventArgs e)
        {
            gdFreq.CommitEdit(DataGridEditingUnit.Cell, true);
            gdFreq.CommitEdit(DataGridEditingUnit.Row, true);
            ViewModel.TxStrengthValue = txtTxStrength.Value;
            ViewModel.SaveConfigCommand.Execute(null);
            lblMsg.Content = ViewModel.StatusMessage;
        }
    }
}
