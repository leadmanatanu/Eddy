using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using _8F.Services.ConfigKey;
using ConfigurationKeyGenerator.Models;
using ConfigurationKeyGenerator.Services;
using Microsoft.Win32;

namespace ConfigurationKeyGenerator.Views
{
    public partial class AddLicense : Window
    {
        private string selectedFile = string.Empty;
        private readonly bool isEditMode;
        private readonly ConfigurationKeyLog? existingLog;

        public AddLicense(ConfigurationKeyLog? log = null)
        {
            InitializeComponent();

            if (log != null)
            {
                isEditMode = true;
                existingLog = log;

                cmbProduct.Text = log.ProductName;
                txtCustomerName.Text = log.CustomerName;
                txtMachineId.Text = log.MachineId;
                txtConfigFile.Text = log.ConfigurationFileName;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Text File (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                selectedFile = dialog.FileName;
                txtConfigFile.Text = Path.GetFileName(selectedFile);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateForm())
                    return;

                string machineIdInput = txtMachineId.Text.Trim();

                // 1. Decode Machine ID -> serial
                byte[] secret = ConfigurationFileService.GetEmbeddedSecret();
                string decodedSerial;
                try
                {
                    decodedSerial = MachineIdCodec.Decode(machineIdInput, secret);
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "Invalid Machine ID",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // 2. Look up machine_registry (database GenerateMachineKey) by machine_id
                var logService = new ConfigurationKeyLogService();
                string? storedSerial = logService.GetRegistrySerial(machineIdInput);
                if (storedSerial == null)
                {
                    MessageBox.Show(
                        "Machine ID not registered",
                        "Registry Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                if (!string.Equals(MachineIdCodec.Normalize(storedSerial), decodedSerial, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "Machine ID registration serial mismatch.",
                        "Registry Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Duplicate Machine ID check
                if (!isEditMode && logService.ExistsByMachineId(machineIdInput))
                {
                    MessageBox.Show(
                        "Configuration already exists for this Machine ID.\n\nPlease edit the existing configuration.",
                        "Duplicate Machine ID",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // 3. Parse settings file or use stored settings JSON
                var rawSettings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var unknownKeys = new List<string>();
                var missingBooleans = new List<string>();

                if (!string.IsNullOrWhiteSpace(selectedFile))
                {
                    var textService = new TextFileService();
                    var parsedFile = textService.ReadConfiguration(selectedFile);

                    foreach (var kvp in parsedFile)
                    {
                        if (!ConfigurationWhitelist.IsWhitelisted(kvp.Key))
                        {
                            unknownKeys.Add(kvp.Key);
                        }
                        else
                        {
                            rawSettings[kvp.Key] = kvp.Value;
                        }
                    }

                    foreach (var boolKey in ConfigurationWhitelist.BooleanKeys)
                    {
                        if (!rawSettings.ContainsKey(boolKey))
                        {
                            missingBooleans.Add(boolKey);
                        }
                    }
                }
                else if (isEditMode && !string.IsNullOrWhiteSpace(existingLog?.SettingsJson))
                {
                    using var doc = JsonDocument.Parse(existingLog.SettingsJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (!ConfigurationWhitelist.IsWhitelisted(prop.Name))
                        {
                            unknownKeys.Add(prop.Name);
                        }
                        else
                        {
                            rawSettings[prop.Name] = prop.Value.Clone();
                        }
                    }
                }

                // Show warnings for unknown keys and missing booleans
                var warnings = new List<string>();
                if (unknownKeys.Count > 0)
                {
                    warnings.Add($"Unknown keys ignored ({unknownKeys.Count}):\n" + string.Join(", ", unknownKeys));
                }
                if (missingBooleans.Count > 0)
                {
                    warnings.Add($"Missing booleans defaulted to false ({missingBooleans.Count}):\n" + string.Join(", ", missingBooleans));
                }
                if (warnings.Count > 0)
                {
                    MessageBox.Show(
                        string.Join("\n\n", warnings),
                        "Configuration Import Warnings",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Filter & validate through whitelist
                if (!ConfigurationWhitelist.TryFilterAndValidate(rawSettings, out var filteredSettings, out string? valError))
                {
                    MessageBox.Show(
                        $"Configuration validation failed:\n{valError}",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // 4. Encrypt configuration
                var fileService = new ConfigurationFileService();
                string product = GetSelectedProduct().ToString();
                string customerName = txtCustomerName.Text.Trim();

                byte[] encryptedKeyBytes = fileService.GenerateEncryptedKeyFile(
                    product: product,
                    customerName: customerName,
                    machineId: machineIdInput,
                    serial: decodedSerial,
                    settings: filteredSettings,
                    secretOverride: secret);

                string shortMachineId = machineIdInput.Length > 10 ? machineIdInput.Substring(0, 10) : machineIdInput;
                string safeCustomer = string.IsNullOrWhiteSpace(customerName) ? "Customer" : customerName.Replace(" ", "_");
                string generatedFileName = $"{safeCustomer}_{product}_{shortMachineId}.key";

                string settingsJson = JsonSerializer.Serialize(filteredSettings, new JsonSerializerOptions { WriteIndented = true });

                // 5. Save record into ConfigurationKeyLogs
                var log = new ConfigurationKeyLog
                {
                    ProductName = product,
                    CustomerName = customerName,
                    MachineId = machineIdInput,
                    ConfigurationFileName = !string.IsNullOrWhiteSpace(selectedFile)
                        ? Path.GetFileName(selectedFile)
                        : existingLog?.ConfigurationFileName ?? string.Empty,
                    GeneratedFileName = generatedFileName,
                    GeneratedFile = encryptedKeyBytes,
                    SettingsJson = settingsJson,
                    GeneratedDate = DateTime.Now
                };

                if (isEditMode)
                {
                    log.Id = existingLog!.Id;
                    logService.Update(log);
                }
                else
                {
                    logService.Save(log);
                }

                MessageBox.Show(
                    isEditMode ? "Configuration updated successfully." : "Configuration generated successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
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

        private ProductType GetSelectedProduct()
        {
            if (cmbProduct.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                switch (item.Content?.ToString())
                {
                    case "EddyTube":
                        return ProductType.EddyTube;
                    case "EddyFaster":
                        return ProductType.EddyFaster;
                    default:
                        return ProductType.EddyShorter;
                }
            }

            return ProductType.EddyShorter;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text))
            {
                MessageBox.Show("Customer Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtMachineId.Text))
            {
                MessageBox.Show("Machine ID is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!isEditMode && string.IsNullOrWhiteSpace(selectedFile))
            {
                MessageBox.Show("Please select a configuration file.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (isEditMode && string.IsNullOrWhiteSpace(selectedFile) && string.IsNullOrWhiteSpace(existingLog?.SettingsJson))
            {
                MessageBox.Show("Please select a configuration file.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
