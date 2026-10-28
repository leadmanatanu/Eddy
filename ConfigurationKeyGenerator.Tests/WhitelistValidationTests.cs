using System.Collections.Generic;
using _8F.Services.ConfigKey;
using Xunit;

namespace ConfigurationKeyGenerator.Tests
{
    public class WhitelistValidationTests
    {
        [Fact]
        public void FilterAndValidate_ValidSettings_Passes()
        {
            var raw = new Dictionary<string, object?>
            {
                ["IsOpenWithDb"] = "true",
                ["FrequencyNo"] = 4,
                ["IsModbusServerEnable"] = true,
                ["ModbusServerPort"] = 5020,
                ["ModbusHeartbeatIntervalMs"] = 1000
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var filtered, out string? error);

            Assert.True(ok, error);
            Assert.Null(error);
            Assert.True((bool)filtered[ConfigurationWhitelist.IsOpenWithDb]);
            Assert.False((bool)filtered[ConfigurationWhitelist.IsOpenWithFile]); // default false
            Assert.Equal(4, filtered[ConfigurationWhitelist.FrequencyNo]);
            Assert.Equal(5020, filtered[ConfigurationWhitelist.ModbusServerPort]);
            Assert.Equal(1000, filtered[ConfigurationWhitelist.ModbusHeartbeatIntervalMs]);
        }

        [Fact]
        public void FilterAndValidate_UnknownKeys_AreIgnored()
        {
            var raw = new Dictionary<string, object?>
            {
                ["UnknownCustomSetting"] = "SomeValue",
                ["AnotherUnknown"] = 12345,
                ["FrequencyNo"] = 1
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var filtered, out string? error);

            Assert.True(ok, error);
            Assert.False(filtered.ContainsKey("UnknownCustomSetting"));
            Assert.False(filtered.ContainsKey("AnotherUnknown"));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(8)]
        public void FilterAndValidate_ValidFrequencyNo_Passes(int freq)
        {
            var raw = new Dictionary<string, object?>
            {
                ["FrequencyNo"] = freq
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var filtered, out string? error);

            Assert.True(ok, error);
            Assert.Equal(freq, filtered[ConfigurationWhitelist.FrequencyNo]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(16)]
        public void FilterAndValidate_InvalidFrequencyNo_Fails(int invalidFreq)
        {
            var raw = new Dictionary<string, object?>
            {
                ["FrequencyNo"] = invalidFreq
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var _, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
            Assert.Contains("FrequencyNo", error);
        }

        [Fact]
        public void FilterAndValidate_MissingFrequencyNo_Fails()
        {
            var raw = new Dictionary<string, object?>
            {
                ["IsOpenWithDb"] = true
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var _, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
            Assert.Contains("FrequencyNo", error);
        }

        [Fact]
        public void FilterAndValidate_ModbusEnabled_MissingPort_Fails()
        {
            var raw = new Dictionary<string, object?>
            {
                ["FrequencyNo"] = 1,
                ["IsModbusServerEnable"] = true,
                ["ModbusHeartbeatIntervalMs"] = 500
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var _, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
            Assert.Contains("ModbusServerPort", error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        public void FilterAndValidate_ModbusEnabled_InvalidPort_Fails(int badPort)
        {
            var raw = new Dictionary<string, object?>
            {
                ["FrequencyNo"] = 1,
                ["IsModbusServerEnable"] = true,
                ["ModbusServerPort"] = badPort,
                ["ModbusHeartbeatIntervalMs"] = 500
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var _, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
            Assert.Contains("ModbusServerPort", error);
        }

        [Fact]
        public void FilterAndValidate_ModbusEnabled_MissingHeartbeat_Fails()
        {
            var raw = new Dictionary<string, object?>
            {
                ["FrequencyNo"] = 1,
                ["IsModbusServerEnable"] = true,
                ["ModbusServerPort"] = 5020
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var _, out string? error);

            Assert.False(ok);
            Assert.NotNull(error);
            Assert.Contains("ModbusHeartbeatIntervalMs", error);
        }

        [Fact]
        public void FilterAndValidate_ModbusDisabled_DefaultsUsedAndPasses()
        {
            var raw = new Dictionary<string, object?>
            {
                ["FrequencyNo"] = 1,
                ["IsModbusServerEnable"] = false
            };

            bool ok = ConfigurationWhitelist.TryFilterAndValidate(raw, out var filtered, out string? error);

            Assert.True(ok, error);
            Assert.Equal(5020, filtered[ConfigurationWhitelist.ModbusServerPort]);
            Assert.Equal(1000, filtered[ConfigurationWhitelist.ModbusHeartbeatIntervalMs]);
        }
    }
}
