using System;
using System.Security.Cryptography;
using _8F.Services.ConfigKey;
using Xunit;

namespace MachineInfo.Tests
{
    public class MachineIdCodecTests
    {
        private static readonly byte[] TestSecret = CreateTestSecret();

        private static byte[] CreateTestSecret()
        {
            byte[] secret = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                secret[i] = (byte)i;
            }
            return secret;
        }

        [Theory]
        [InlineData("A1234567890B")]
        [InlineData(" a1234567890b ")]
        [InlineData("a1234567890B")]
        public void Encode_TestVector_MatchesExpected(string serial)
        {
            const string expected = "06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A523";

            string actual = MachineIdCodec.Encode(serial, TestSecret);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Encode_CaseAndWhitespaceVariants_ProduceIdenticalMachineId()
        {
            string id1 = MachineIdCodec.Encode("EDDY-MAG-2026-001", TestSecret);
            string id2 = MachineIdCodec.Encode("eddy-mag-2026-001", TestSecret);
            string id3 = MachineIdCodec.Encode("  eddy-mag-2026-001  ", TestSecret);
            string id4 = MachineIdCodec.Encode("\tEDDY-MAG-2026-001\r\n", TestSecret);

            Assert.Equal(id1, id2);
            Assert.Equal(id1, id3);
            Assert.Equal(id1, id4);
        }

        [Fact]
        public void Encode_DifferentSerials_ProduceDifferentMachineIds()
        {
            string idA = MachineIdCodec.Encode("SERIAL-A", TestSecret);
            string idB = MachineIdCodec.Encode("SERIAL-B", TestSecret);

            Assert.NotEqual(idA, idB);
        }

        [Theory]
        [InlineData("A1234567890B")]
        [InlineData("EDDY-8F-9942")]
        [InlineData("X")]
        [InlineData("1234567890123456789012345678901234567890123456789012345678901234")] // 64 chars
        public void Decode_RoundTrip_ReturnsNormalizedSerial(string originalSerial)
        {
            string encoded = MachineIdCodec.Encode(originalSerial, TestSecret);
            string decoded = MachineIdCodec.Decode(encoded, TestSecret);

            Assert.Equal(originalSerial.Trim().ToUpperInvariant(), decoded);
        }

        [Fact]
        public void Decode_TamperedHex_Fails()
        {
            string encoded = MachineIdCodec.Encode("A1234567890B", TestSecret);

            // Flip last hex char
            char lastChar = encoded[^1];
            char replacement = (lastChar == '0') ? '1' : '0';
            string tampered = encoded[..^1] + replacement;

            Assert.Throws<CryptographicException>(() => MachineIdCodec.Decode(tampered, TestSecret));
        }

        [Theory]
        [InlineData("")]
        [InlineData("06B79C1D")] // Too short
        [InlineData("06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A52")] // Odd length
        public void Decode_WrongLength_Fails(string invalidHex)
        {
            Assert.Throws<CryptographicException>(() => MachineIdCodec.Decode(invalidHex, TestSecret));
        }

        [Fact]
        public void Decode_NonHex_Fails()
        {
            string encoded = MachineIdCodec.Encode("A1234567890B", TestSecret);
            string nonHex = encoded[..^2] + "ZZ";

            Assert.Throws<CryptographicException>(() => MachineIdCodec.Decode(nonHex, TestSecret));
        }

        [Fact]
        public void Decode_WrongKey_Fails()
        {
            byte[] otherSecret = new byte[32];
            otherSecret[0] = 0xFF;

            string encoded = MachineIdCodec.Encode("A1234567890B", TestSecret);

            Assert.Throws<CryptographicException>(() => MachineIdCodec.Decode(encoded, otherSecret));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t \r\n")]
        [InlineData("SERIAL\0WITHNULL")]
        [InlineData("SERIAL\u0007BELL")]
        [InlineData("12345678901234567890123456789012345678901234567890123456789012345")] // 65 chars
        public void Normalize_RejectsInvalidSerials(string? invalidSerial)
        {
            Assert.Throws<ArgumentException>(() => MachineIdCodec.Normalize(invalidSerial));
        }

        [Fact]
        public void IsValidFormat_ValidatesCorrectly()
        {
            string valid = "06B79C1DE2974253F751657C2342B47A90D174BB60DDD9FC55462A379E108BE0DC17612CA464A523";
            Assert.True(MachineIdCodec.IsValidFormat(valid));
            Assert.True(MachineIdCodec.IsValidFormat(valid.ToLowerInvariant()));

            Assert.False(MachineIdCodec.IsValidFormat(null));
            Assert.False(MachineIdCodec.IsValidFormat(""));
            Assert.False(MachineIdCodec.IsValidFormat("06B79C1D")); // Too short
            Assert.False(MachineIdCodec.IsValidFormat(valid + "0")); // Odd length
            Assert.False(MachineIdCodec.IsValidFormat(valid.Replace('0', 'G'))); // Non-hex
        }
    }
}
