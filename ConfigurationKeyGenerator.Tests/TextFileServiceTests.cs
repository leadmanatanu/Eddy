using System;
using System.IO;
using ConfigurationKeyGenerator.Services;
using Xunit;

namespace ConfigurationKeyGenerator.Tests
{
    public class TextFileServiceTests
    {
        [Fact]
        public void ReadConfiguration_KeyValueFormat_ParsesSuccessfully()
        {
            string content = @"
# Comments should be ignored
FrequencyNo=8
IsOpenWithDb=false
IsOpenWithFile=true
ModbusServerPort=5020
";
            string temp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(temp, content);
                var service = new TextFileService();
                var dict = service.ReadConfiguration(temp);

                Assert.Equal("8", dict["FrequencyNo"]);
                Assert.Equal("false", dict["IsOpenWithDb"]);
                Assert.Equal("true", dict["IsOpenWithFile"]);
                Assert.Equal("5020", dict["ModbusServerPort"]);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        [Fact]
        public void ReadConfiguration_XmlAddFormat_ParsesSuccessfully()
        {
            string content = @"
<add key=""FrequencyNo"" value=""8"" />
<add key=""IsOpenWithDb"" value=""false"" />
<add key=""IsOpenWithFile"" value=""true"" />
<add key=""ModbusServerPort"" value=""5020"" />
";
            string temp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(temp, content);
                var service = new TextFileService();
                var dict = service.ReadConfiguration(temp);

                Assert.Equal("8", dict["FrequencyNo"]);
                Assert.Equal("false", dict["IsOpenWithDb"]);
                Assert.Equal("true", dict["IsOpenWithFile"]);
                Assert.Equal("5020", dict["ModbusServerPort"]);
            }
            finally
            {
                File.Delete(temp);
            }
        }
    }
}
