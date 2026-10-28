using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace _8F.Services.ConfigKey
{
    /// <summary>
    /// Central gateway for all 32 whitelisted configuration settings.
    /// Direct reads of whitelisted keys from ConfigurationManager.AppSettings are prohibited.
    /// In Release mode, fails closed (all toggles false) unless a valid config.key was verified.
    /// In Debug mode (#if DEBUG), falls back to App.Debug.config for developer convenience without a key.
    /// </summary>
    public static class ProtectedSettings
    {
        private static readonly object _syncLock = new();
        private static readonly Dictionary<string, object> _settings = new(StringComparer.OrdinalIgnoreCase);
        private static bool _isVerified;

        static ProtectedSettings()
        {
            Reset();
        }

        public static bool IsVerified
        {
            get
            {
                lock (_syncLock)
                {
                    return _isVerified;
                }
            }
        }

        public static void Reset()
        {
            lock (_syncLock)
            {
                _isVerified = false;
                _settings.Clear();

                // Fail-closed defaults: all 29 booleans are false
                foreach (string boolKey in ConfigurationWhitelist.BooleanKeys)
                {
                    _settings[boolKey] = false;
                }

                _settings[ConfigurationWhitelist.ModbusServerPort] = 5020;
                _settings[ConfigurationWhitelist.ModbusHeartbeatIntervalMs] = 1000;
                _settings[ConfigurationWhitelist.FrequencyNo] = 8;

#if DEBUG
                LoadDebugFallback();
#endif
            }
        }

        public static void Apply(IReadOnlyDictionary<string, object> verifiedSettings)
        {
            if (verifiedSettings == null) throw new ArgumentNullException(nameof(verifiedSettings));

            lock (_syncLock)
            {
                foreach (var kvp in verifiedSettings)
                {
                    if (ConfigurationWhitelist.IsWhitelisted(kvp.Key))
                    {
                        _settings[kvp.Key] = kvp.Value;
                    }
                }
                _isVerified = true;
            }
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            lock (_syncLock)
            {
                if (_settings.TryGetValue(key, out var val))
                {
                    if (val is bool b) return b;
                    if (bool.TryParse(val?.ToString(), out bool parsed)) return parsed;
                }
                return defaultValue;
            }
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            lock (_syncLock)
            {
                if (_settings.TryGetValue(key, out var val))
                {
                    if (val is int i) return i;
                    if (int.TryParse(val?.ToString(), out int parsed)) return parsed;
                }
                return defaultValue;
            }
        }

#if DEBUG
        private static void LoadDebugFallback()
        {
            try
            {
                // Look for App.Debug.config in current directory or base directory
                string? baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string debugConfigPath = Path.Combine(baseDir, "App.Debug.config");
                if (!File.Exists(debugConfigPath))
                {
                    string parentConfig = Path.Combine(baseDir, "..", "..", "..", "App.Debug.config");
                    if (File.Exists(parentConfig))
                    {
                        debugConfigPath = Path.GetFullPath(parentConfig);
                    }
                }

                if (File.Exists(debugConfigPath))
                {
                    var doc = XDocument.Load(debugConfigPath);
                    var appSettings = doc.Root?.Element("appSettings");
                    if (appSettings != null)
                    {
                        foreach (var add in appSettings.Elements("add"))
                        {
                            string? k = add.Attribute("key")?.Value;
                            string? v = add.Attribute("value")?.Value;
                            if (string.IsNullOrEmpty(k) || v == null) continue;

                            if (ConfigurationWhitelist.BooleanKeys.Contains(k))
                            {
                                if (bool.TryParse(v, out bool b))
                                {
                                    _settings[k] = b;
                                }
                            }
                            else if (ConfigurationWhitelist.IntegerKeys.Contains(k))
                            {
                                if (int.TryParse(v, out int i))
                                {
                                    _settings[k] = i;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"App.Debug.config read error: {ex.Message}");
            }
        }
#endif

        // Strongly-typed accessors for all 32 whitelisted settings:

        // File Menu (11 booleans)
        public static bool IsOpenWithDb => GetBool(ConfigurationWhitelist.IsOpenWithDb);
        public static bool IsOpenWithFile => GetBool(ConfigurationWhitelist.IsOpenWithFile);
        public static bool IsSaveWithDb => GetBool(ConfigurationWhitelist.IsSaveWithDb);
        public static bool IsSaveWithFile => GetBool(ConfigurationWhitelist.IsSaveWithFile);
        public static bool IsSaveAsWithDb => GetBool(ConfigurationWhitelist.IsSaveAsWithDb);
        public static bool IsSaveAsWithFile => GetBool(ConfigurationWhitelist.IsSaveAsWithFile);
        public static bool IsImportConfigurationWithDb => GetBool(ConfigurationWhitelist.IsImportConfigurationWithDb);
        public static bool IsImportConfigurationWithFile => GetBool(ConfigurationWhitelist.IsImportConfigurationWithFile);
        public static bool IsExportConfigurationWithDb => GetBool(ConfigurationWhitelist.IsExportConfigurationWithDb);
        public static bool IsExportConfigurationWithFile => GetBool(ConfigurationWhitelist.IsExportConfigurationWithFile);
        public static bool IsExitEnable => GetBool(ConfigurationWhitelist.IsExitEnable);

        // Modbus TCP Slave (1 boolean, 2 integers)
        public static bool IsModbusServerEnable => GetBool(ConfigurationWhitelist.IsModbusServerEnable);
        public static int ModbusServerPort => GetInt(ConfigurationWhitelist.ModbusServerPort, 5020);
        public static int ModbusHeartbeatIntervalMs => GetInt(ConfigurationWhitelist.ModbusHeartbeatIntervalMs, 1000);

        // Configuration Menu (8 booleans)
        public static bool IsPasswordEnable => GetBool(ConfigurationWhitelist.IsPasswordEnable);
        public static bool IsChangeConfigurationEnable => GetBool(ConfigurationWhitelist.IsChangeConfigurationEnable);
        public static bool IsThresholdSettingEnable => GetBool(ConfigurationWhitelist.IsThresholdSettingEnable);
        public static bool IsAutoEllipseEnable => GetBool(ConfigurationWhitelist.IsAutoEllipseEnable);
        public static bool IsReNewConfig => GetBool(ConfigurationWhitelist.IsReNewConfig);
        public static bool IsWriteConfigurationEnable => GetBool(ConfigurationWhitelist.IsWriteConfigurationEnable);
        public static bool IsCopyChannel1ConfigurationEnable => GetBool(ConfigurationWhitelist.IsCopyChannel1ConfigurationEnable);
        public static bool IsChangePasswordEnable => GetBool(ConfigurationWhitelist.IsChangePasswordEnable);

        // View Log Menu (3 booleans)
        public static bool IsBatchWiseLogEnable => GetBool(ConfigurationWhitelist.IsBatchWiseLogEnable);
        public static bool IsSerialNumberLogEnable => GetBool(ConfigurationWhitelist.IsSerialNumberLogEnable);
        public static bool IsPdfReportEnable => GetBool(ConfigurationWhitelist.IsPdfReportEnable);

        // Counter Visibility (3 booleans)
        public static bool IsTotalCountVisible => GetBool(ConfigurationWhitelist.IsTotalCountVisible);
        public static bool IsOkCountVisible => GetBool(ConfigurationWhitelist.IsOkCountVisible);
        public static bool IsNotOkCountVisible => GetBool(ConfigurationWhitelist.IsNotOkCountVisible);

        // System Hardware Flags (3 booleans)
        public static bool IsLogRequiredOnBalance => GetBool(ConfigurationWhitelist.IsLogRequiredOnBalance);
        public static bool IsTxStrengthEnable => GetBool(ConfigurationWhitelist.IsTxStrengthEnable);
        public static bool IsTestLogOff => GetBool(ConfigurationWhitelist.IsTestLogOff);

        // Frequency (1 integer)
        public static int FrequencyNo => GetInt(ConfigurationWhitelist.FrequencyNo, 8);
    }
}
