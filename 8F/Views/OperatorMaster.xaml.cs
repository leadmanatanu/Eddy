using _8F.Auth;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using _8F.ViewModels;

namespace _8F
{
    public partial class OperatorMaster : Window
    {
        public OperatorMasterViewModel ViewModel { get; }

        public OperatorMaster()
        {
            InitializeComponent();
            if (!AppServices.AuthService.Can(Permission.EditSettings))
            {
                ViewModel = null!;
                MessageBox.Show("Admin access required for Operator Master.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                Loaded += (s, e) => Close();
                return;
            }

            ViewModel = new OperatorMasterViewModel();
            ViewModel.CloseAction = Close;
            DataContext = ViewModel;

            Loaded += (s, e) => UpdateGrid();
        }

        private void UpdateGrid()
        {
            grdOperator.ItemsSource = ViewModel.OperatorList;
            if (ViewModel.OperatorList.Count == 0)
            {
                txtOperatorMessage.Visibility = Visibility.Visible;
                grdOperator.Visibility = Visibility.Collapsed;
            }
            else
            {
                txtOperatorMessage.Visibility = Visibility.Collapsed;
                grdOperator.Visibility = Visibility.Visible;
            }
        }

        private void btnAddSave_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.OperatorName = txtOperatorName.Text;
            ViewModel.SaveCommand.Execute(null);
            UpdateGrid();
            ResetForm();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is Operator op)
            {
                ViewModel.EditOperator(op);
                txtOperatorName.Text = op.OperatorName;
                lblAddSave.Content = "Save";
                grdOperator.SelectedItem = op;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is Operator op)
            {
                ViewModel.DeleteOperator(op);
                UpdateGrid();
                ResetForm();
            }
        }

        private void grdOperator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (grdOperator.SelectedItem is Operator op)
            {
                ViewModel.EditOperator(op);
                txtOperatorName.Text = op.OperatorName;
                lblAddSave.Content = "Save";
            }
        }

        private void txtOperatorName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOperatorName.Text))
            {
                ResetForm();
            }
        }

        private void ResetForm()
        {
            txtOperatorName.Clear();
            ViewModel.ResetForm();
            lblAddSave.Content = "Add";
            grdOperator.SelectedItem = null;
        }

        private void btnClose_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.CloseCommand.Execute(null);
        }
    }
}