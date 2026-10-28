using System;
using System.Threading;
using System.Threading.Tasks;
using _8F.Services.ConfigKey;
using MachineInfo.Application.Interfaces;
using MachineInfo.Application.UseCases;
using MachineInfo.Domain.Entities;
using Moq;
using Xunit;

namespace MachineInfo.Tests
{
    public class GenerateMachineIdUseCaseTests
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

        [Fact]
        public async Task ExecuteAsync_WhenSerialIsNew_InsertsIntoRegistryAndReturnsMachineId()
        {
            var registryMock = new Mock<IMachineRegistry>();
            var secretProviderMock = new Mock<IAppSecretProvider>();
            secretProviderMock.Setup(s => s.GetSecret()).Returns(TestSecret);

            const string rawSerial = " a1234567890b ";
            const string normalizedSerial = "A1234567890B";
            string expectedId = MachineIdCodec.Encode(normalizedSerial, TestSecret);

            registryMock.Setup(r => r.GetBySerialNumberAsync(normalizedSerial, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((MachineRegistryRecord?)null);

            registryMock.Setup(r => r.InsertAsync(It.IsAny<MachineRegistryRecord>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((MachineRegistryRecord rec, CancellationToken _) => rec with { Id = 42 });

            var useCase = new GenerateMachineId(registryMock.Object, secretProviderMock.Object);

            string result = await useCase.ExecuteAsync(rawSerial);

            Assert.Equal(expectedId, result);
            registryMock.Verify(r => r.GetBySerialNumberAsync(normalizedSerial, It.IsAny<CancellationToken>()), Times.Once);
            registryMock.Verify(r => r.InsertAsync(
                It.Is<MachineRegistryRecord>(rec => rec.SerialNumber == normalizedSerial && rec.MachineId == expectedId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WhenSerialExistsWithSameMachineId_ReturnsStoredIdWithoutInsert()
        {
            var registryMock = new Mock<IMachineRegistry>();
            var secretProviderMock = new Mock<IAppSecretProvider>();
            secretProviderMock.Setup(s => s.GetSecret()).Returns(TestSecret);

            const string rawSerial = "A1234567890B";
            string expectedId = MachineIdCodec.Encode(rawSerial, TestSecret);

            var existingRecord = new MachineRegistryRecord(
                Id: 10,
                SerialNumber: rawSerial,
                MachineId: expectedId,
                CreatedAt: DateTimeOffset.UtcNow
            );

            registryMock.Setup(r => r.GetBySerialNumberAsync(rawSerial, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(existingRecord);

            var useCase = new GenerateMachineId(registryMock.Object, secretProviderMock.Object);

            string result = await useCase.ExecuteAsync(rawSerial);

            Assert.Equal(expectedId, result);
            registryMock.Verify(r => r.GetBySerialNumberAsync(rawSerial, It.IsAny<CancellationToken>()), Times.Once);
            registryMock.Verify(r => r.InsertAsync(It.IsAny<MachineRegistryRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WhenSerialExistsWithDifferentMachineId_ThrowsAndDoesNotOverwrite()
        {
            var registryMock = new Mock<IMachineRegistry>();
            var secretProviderMock = new Mock<IAppSecretProvider>();
            secretProviderMock.Setup(s => s.GetSecret()).Returns(TestSecret);

            const string rawSerial = "A1234567890B";
            const string differentMachineId = "99999999999999999999999999999999999999999999999999999999999999999999999999999999";

            var existingRecord = new MachineRegistryRecord(
                Id: 10,
                SerialNumber: rawSerial,
                MachineId: differentMachineId,
                CreatedAt: DateTimeOffset.UtcNow
            );

            registryMock.Setup(r => r.GetBySerialNumberAsync(rawSerial, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(existingRecord);

            var useCase = new GenerateMachineId(registryMock.Object, secretProviderMock.Object);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(rawSerial));
            Assert.Contains("already registered with a different Machine ID", ex.Message);
            registryMock.Verify(r => r.InsertAsync(It.IsAny<MachineRegistryRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("CTRL\0CHAR")]
        public async Task ExecuteAsync_WhenSerialIsInvalid_ThrowsArgumentException(string invalidSerial)
        {
            var registryMock = new Mock<IMachineRegistry>();
            var secretProviderMock = new Mock<IAppSecretProvider>();

            var useCase = new GenerateMachineId(registryMock.Object, secretProviderMock.Object);

            await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(invalidSerial));
        }
    }
}
