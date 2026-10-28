using System.Security.Cryptography;
using _8F.Services.ConfigKey;
using Xunit;

namespace ConfigurationKeyGenerator.Tests
{
    public class TestVectorTests
    {
        private static byte[] CreateTestSecret()
        {
            byte[] secret = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                secret[i] = (byte)i;
            }
            return secret;
        }

        [Fact]
        public void Encode_TestVector_MatchesExpectedMachineId()
        {
            byte[] secret = CreateTestSecret();
            const string serial = "A1234567890B";
            const string expectedMachineId = "06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A523";

            string actual = MachineIdCodec.Encode(serial, secret);

            Assert.Equal(expectedMachineId, actual);
        }

        [Fact]
        public void Decode_TestVector_ReturnsNormalizedSerial()
        {
            byte[] secret = CreateTestSecret();
            const string machineId = "06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A523";

            string decoded = MachineIdCodec.Decode(machineId, secret);

            Assert.Equal("A1234567890B", decoded);
        }
    }
}
