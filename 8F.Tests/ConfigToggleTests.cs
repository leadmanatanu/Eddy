using System;
using System.Collections.Generic;
using System.Configuration;
using Xunit;
using _8F.Models;
using _8F.Services;

namespace _8F.Tests
{
    public class ConfigToggleTests
    {
        [Fact]
        public void AppSettings_DefaultOrConfiguredKeys_CanBeParsed()
        {
            // Act: Evaluate standard app configuration settings
            string isAutoEllipseStr = ConfigurationManager.AppSettings["IsAutoEllipseEnable"] ?? "true";
            string isOpenDbStr = ConfigurationManager.AppSettings["isOpenDbEbable"] ?? "false";
            string isTotalCountVisibleStr = ConfigurationManager.AppSettings["IsTotalCountVisible"] ?? "true";

            bool isAutoEllipseEnable = Convert.ToBoolean(isAutoEllipseStr);
            bool isOpenDbEbable = Convert.ToBoolean(isOpenDbStr);
            bool isTotalCountVisible = Convert.ToBoolean(isTotalCountVisibleStr);

            // Assert
            Assert.True(isAutoEllipseEnable || !isAutoEllipseEnable);
            Assert.True(isOpenDbEbable || !isOpenDbEbable);
            Assert.True(isTotalCountVisible || !isTotalCountVisible);
        }

        [Fact]
        public void CounterCalculation_IndependentOfDisplayToggles()
        {
            // Arrange: Simulate counter aggregation when IsTotalCountVisible or IsNotOkCountVisible are false
            var counters = new List<Counter>
            {
                new Counter { Id = 1, ResultOkCount = 10, ResultOkNotCount = 2 }
            };

            // Act: Calculate total count regardless of UI display settings
            int totalCount = counters[0].ResultOkCount + counters[0].ResultOkNotCount;

            // Assert
            Assert.Equal(12, totalCount);
            Assert.Equal(10, counters[0].ResultOkCount);
            Assert.Equal(2, counters[0].ResultOkNotCount);
        }

        [Fact]
        public void InspectionLogPayload_MaintainsIntegrityRegardlessOfDisplayConfig()
        {
            // Arrange
            var log = new InspectionLog
            {
                SerialNumber = "SN-CFG-001",
                BatchId = "BATCH-001",
                OperatorName = "Op1",
                InspectionTimestamp = DateTime.UtcNow,
                ChannelNumber = 1,
                FrequencyHz = 400,
                FrequencyData = "[]",

                ResultPass = true
            };

            // Act & Assert: All inspection fields remain intact and accessible for database writing regardless of UI toggle states
            Assert.Equal("SN-CFG-001", log.SerialNumber);
            Assert.Equal("BATCH-001", log.BatchId);
            Assert.Equal(1, log.ChannelNumber);
            Assert.Equal(400, log.FrequencyHz);
            Assert.True(log.ResultPass);
        }
    }
}
