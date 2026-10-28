using System;
using System.Collections.Generic;
using System.Text.Json;

namespace _8F.Services.ConfigKey
{
    /// <summary>
    /// Shared configuration whitelist contract for 8F:
    /// 29 booleans, ModbusServerPort, ModbusHeartbeatIntervalMs, FrequencyNo (1/4/8).
    /// </summary>
    public static class ConfigurationWhitelist
    {
        // 29 Booleans
        public const string IsOpenWithDb = "IsOpenWithDb";
        public const string IsOpenWithFile = "IsOpenWithFile";
        public const string IsSaveWithDb = "IsSaveWithDb";
        public const string IsSaveWithFile = "IsSaveWithFile";
        public const string IsSaveAsWithDb = "IsSaveAsWithDb";
        public const string IsSaveAsWithFile = "IsSaveAsWithFile";
        public const string IsImportConfigurationWithDb = "IsImportConfigurationWithDb";
        public const string IsImportConfigurationWithFile = "IsImportConfigurationWithFile";
        public const string IsExportConfigurationWithDb = "IsExportConfigurationWithDb";
        public const string IsExportConfigurationWithFile = "IsExportConfigurationWithFile";
        public const string IsExitEnable = "IsExitEnable";

        public const string IsModbusServerEnable = "IsModbusServerEnable";

        public const string IsPasswordEnable = "IsPasswordEnable";
        public const string IsChangeConfigurationEnable = "IsChangeConfigurationEnable";
        public const string IsThresholdSettingEnable = "IsThresholdSettingEnable";
        public const string IsAutoEllipseEnable = "IsAutoEllipseEnable";
        public const string IsReNewConfig = "IsReNewConfig";
        public const string IsWriteConfigurationEnable = "IsWriteConfigurationEnable";
        public const string IsCopyChannel1ConfigurationEnable = "IsCopyChannel1ConfigurationEnable";
        public const string IsChangePasswordEnable = "IsChangePasswordEnable";

        public const string IsBatchWiseLogEnable = "IsBatchWiseLogEnable";
        public const string IsSerialNumberLogEnable = "IsSerialNumberLogEnable";
        public const string IsPdfReportEnable = "IsPdfReportEnable";

        public const string IsTotalCountVisible = "IsTotalCountVisible";
        public const string IsOkCountVisible = "IsOkCountVisible";
        public const string IsNotOkCountVisible = "IsNotOkCountVisible";

        public const string IsLogRequiredOnBalance = "IsLogRequiredOnBalance";
        public const string IsTxStrengthEnable = "IsTxStrengthEnable";
        public const string IsTestLogOff = "IsTestLogOff";

        // 3 Integers
        public const string ModbusServerPort = "ModbusServerPort";
        public const string ModbusHeartbeatIntervalMs = "ModbusHeartbeatIntervalMs";
        public const string FrequencyNo = "FrequencyNo";

        public static readonly HashSet<string> BooleanKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            IsOpenWithDb,
            IsOpenWithFile,
            IsSaveWithDb,
            IsSaveWithFile,
            IsSaveAsWithDb,
            IsSaveAsWithFile,
            IsImportConfigurationWithDb,
            IsImportConfigurationWithFile,
            IsExportConfigurationWithDb,
            IsExportConfigurationWithFile,
            IsExitEnable,

            IsModbusServerEnable,

            IsPasswordEnable,
            IsChangeConfigurationEnable,
            IsThresholdSettingEnable,
            IsAutoEllipseEnable,
            IsReNewConfig,
            IsWriteConfigurationEnable,
            IsCopyChannel1ConfigurationEnable,
            IsChangePasswordEnable,

            IsBatchWiseLogEnable,
            IsSerialNumberLogEnable,
            IsPdfReportEnable,

            IsTotalCountVisible,
            IsOkCountVisible,
            IsNotOkCountVisible,

            IsLogRequiredOnBalance,
            IsTxStrengthEnable,
            IsTestLogOff
        };

        public static readonly HashSet<string> IntegerKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            ModbusServerPort,
            ModbusHeartbeatIntervalMs,
            FrequencyNo
        };

        public static readonly HashSet<string> AllKeys = new(StringComparer.OrdinalIgnoreCase);

        static ConfigurationWhitelist()
        {
            foreach (var k in BooleanKeys) AllKeys.Add(k);
            foreach (var k in IntegerKeys) AllKeys.Add(k);
        }

        public static bool IsWhitelisted(string key) => AllKeys.Contains(key);

        /// <summary>
        /// Validates and filters incoming settings dictionary against the whitelist rules.
        /// Unknown keys are ignored.
        /// Missing booleans default to false.
        /// FrequencyNo is required and must be 1, 4, or 8.
        /// If IsModbusServerEnable is true, ModbusServerPort (1..65535) and ModbusHeartbeatIntervalMs (>0) are required.
        /// </summary>
        public static bool TryFilterAndValidate(
            IReadOnlyDictionary<string, object?> rawSettings,
            out Dictionary<string, object> filteredSettings,
            out string? validationError)
        {
            filteredSettings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            validationError = null;

            // 1. Process all 29 booleans (missing = false, invalid = error)
            foreach (string boolKey in BooleanKeys)
            {
                if (TryGetRawValue(rawSettings, boolKey, out var rawVal))
                {
                    if (TryParseBool(rawVal, out bool bVal))
                    {
                        filteredSettings[boolKey] = bVal;
                    }
                    else
                    {
                        validationError = $"Invalid boolean value for '{boolKey}'.";
                        return false;
                    }
                }
                else
                {
                    // Missing boolean = false
                    filteredSettings[boolKey] = false;
                }
            }

            // 2. Validate FrequencyNo (required, must be 1, 4, or 8)
            if (!TryGetRawValue(rawSettings, FrequencyNo, out var rawFreq) || !TryParseInt(rawFreq, out int freqVal))
            {
                validationError = "FrequencyNo is missing or not a valid integer.";
                return false;
            }

            if (freqVal != 1 && freqVal != 4 && freqVal != 8)
            {
                validationError = $"FrequencyNo must be 1, 4, or 8 (got {freqVal}).";
                return false;
            }
            filteredSettings[FrequencyNo] = freqVal;

            // 3. Modbus settings validation
            bool isModbusEnabled = (bool)filteredSettings[IsModbusServerEnable];
            if (isModbusEnabled)
            {
                if (!TryGetRawValue(rawSettings, ModbusServerPort, out var rawPort) || !TryParseInt(rawPort, out int portVal) || portVal < 1 || portVal > 65535)
                {
                    validationError = "ModbusServerPort is missing or invalid while IsModbusServerEnable is true.";
                    return false;
                }
                filteredSettings[ModbusServerPort] = portVal;

                if (!TryGetRawValue(rawSettings, ModbusHeartbeatIntervalMs, out var rawHb) || !TryParseInt(rawHb, out int hbVal) || hbVal <= 0)
                {
                    validationError = "ModbusHeartbeatIntervalMs is missing or invalid while IsModbusServerEnable is true.";
                    return false;
                }
                filteredSettings[ModbusHeartbeatIntervalMs] = hbVal;
            }
            else
            {
                // If Modbus is disabled, parse port & heartbeat if present, else use standard defaults (5020, 1000)
                if (TryGetRawValue(rawSettings, ModbusServerPort, out var rawPort) && TryParseInt(rawPort, out int portVal) && portVal >= 1 && portVal <= 65535)
                {
                    filteredSettings[ModbusServerPort] = portVal;
                }
                else
                {
                    filteredSettings[ModbusServerPort] = 5020;
                }

                if (TryGetRawValue(rawSettings, ModbusHeartbeatIntervalMs, out var rawHb) && TryParseInt(rawHb, out int hbVal) && hbVal > 0)
                {
                    filteredSettings[ModbusHeartbeatIntervalMs] = hbVal;
                }
                else
                {
                    filteredSettings[ModbusHeartbeatIntervalMs] = 1000;
                }
            }

            return true;
        }

        private static bool TryGetRawValue(IReadOnlyDictionary<string, object?> dict, string key, out object? val)
        {
            foreach (var kvp in dict)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    val = kvp.Value;
                    return val != null;
                }
            }
            val = null;
            return false;
        }

        private static bool TryParseBool(object? value, out bool result)
        {
            result = false;
            if (value == null) return false;

            if (value is bool b)
            {
                result = b;
                return true;
            }

            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.True) { result = true; return true; }
                if (je.ValueKind == JsonValueKind.False) { result = false; return true; }
                if (je.ValueKind == JsonValueKind.String)
                {
                    return bool.TryParse(je.GetString(), out result);
                }
                return false;
            }

            return bool.TryParse(value.ToString(), out result);
        }

        private static bool TryParseInt(object? value, out int result)
        {
            result = 0;
            if (value == null) return false;

            if (value is int i)
            {
                result = i;
                return true;
            }
            if (value is long l && l >= int.MinValue && l <= int.MaxValue)
            {
                result = (int)l;
                return true;
            }

            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out int n))
                {
                    result = n;
                    return true;
                }
                if (je.ValueKind == JsonValueKind.String)
                {
                    return int.TryParse(je.GetString(), out result);
                }
                return false;
            }

            return int.TryParse(value.ToString(), out result);
        }
    }
}
