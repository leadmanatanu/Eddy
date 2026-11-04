using _8F.Auth;
using System;
using System.Windows;
using System.Windows.Controls;
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
            if (!AppServices.AuthService.Can(Permission.EditSettings))
            {
                ViewModel = null!;
                MessageBox.Show("Admin access required to view or edit configuration.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                Loaded += (s, e) => Close();
                return;
            }

            ViewModel = new FreqViewModel();
            ViewModel.CloseAction = Close;
            DataContext = ViewModel;

            Loaded += (s, e) =>
            {
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
            if (!AppServices.AuthService.Can(Permission.EditSettings))
            {
                MessageBox.Show("Admin access required to save configuration.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            gdFreq.CommitEdit(DataGridEditingUnit.Cell, true);
            gdFreq.CommitEdit(DataGridEditingUnit.Row, true);
            ViewModel.TxStrengthValue = txtTxStrength.Value;
            ViewModel.SaveConfigCommand.Execute(null);
            lblMsg.Content = ViewModel.StatusMessage;
        }
    }
}
