using System;
using System.Windows;
using System.Windows.Input;
using MachineInfo.Application.UseCases;

namespace MachineInfo
{
    public partial class MainWindow : Window
    {
        private readonly IGenerateMachineId _generateMachineId;

        public MainWindow(IGenerateMachineId generateMachineId)
        {
            InitializeComponent();
            _generateMachineId = generateMachineId ?? throw new ArgumentNullException(nameof(generateMachineId));

            txtMachineId.Text = string.Empty;
            btnCopy.IsEnabled = false;
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            await GenerateMachineIdAsync();
        }

        private async void TxtSerialNumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await GenerateMachineIdAsync();
            }
        }

        private async System.Threading.Tasks.Task GenerateMachineIdAsync()
        {
            btnGenerate.IsEnabled = false;
            try
            {
                string rawSerial = txtSerialNumber.Text;
                string machineId = await _generateMachineId.ExecuteAsync(rawSerial);

                txtMachineId.Text = machineId;
                btnCopy.IsEnabled = true;
            }
            catch (Exception ex)
            {
                // Errors never appear in the ID box (clear the box, disable Copy, show a MessageBox)
                txtMachineId.Text = string.Empty;
                btnCopy.IsEnabled = false;

                MessageBox.Show(
                    ex.Message,
                    "Machine ID Generation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                btnGenerate.IsEnabled = true;
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtMachineId.Text))
            {
                Clipboard.SetText(txtMachineId.Text);
                MessageBox.Show(
                    "Machine ID copied successfully.",
                    "Machine Info",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
