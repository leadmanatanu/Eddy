using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using _8F.Models;
using _8F.Services;
using _8F.ViewModels;

namespace _8F
{
    public partial class CircleSetting : Window
    {
        public CircleSettingViewModel ViewModel { get; }

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

        public CircleSetting(string selectChannel)
        {
            InitializeComponent();
            ViewModel = new CircleSettingViewModel(selectChannel);
            ViewModel.CloseAction = Close;
            DataContext = ViewModel;

            Loaded += (s, e) =>
            {
                var selCh = DeviceCOM.channelDatas?.FirstOrDefault(c => c.IsSeleted);
                int chId = selCh?.Id ?? 1;
                lblTitle.Content = $"Threshold Setting - Channel-{chId}";
                Title = $"Threshold Setting - Channel-{chId}";

                ViewModel.OwnerWindow = Owner;
                gdFreq.ItemsSource = ViewModel.Ellipses;
            };
        }

        private void btnConfigSave_Click(object sender, RoutedEventArgs e)
        {
            gdFreq.CommitEdit(DataGridEditingUnit.Cell, true);
            gdFreq.CommitEdit(DataGridEditingUnit.Row, true);
            ViewModel.SaveConfigCommand.Execute(null);
            lblMsg.Content = ViewModel.StatusMessage;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CloseCommand.Execute(null);
        }

        private void btnNew_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewModel.AddNewCommand.Execute(null);
        }

        private void btn_installSnippet_Click(object sender, RoutedEventArgs e)
        {
            if (gdFreq.SelectedItem is EllipsDTO item)
            {
                ViewModel.DeleteSelectedCommand.Execute(item);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }
    }
}
