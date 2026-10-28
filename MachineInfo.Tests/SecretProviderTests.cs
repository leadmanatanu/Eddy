using System;
using System.Reflection;
using MachineInfo.Infrastructure.Security;
using Xunit;

namespace MachineInfo.Tests
{
    public class SecretProviderTests
    {
        [Fact]
        public void EmbeddedResourceSecretProvider_CanLoadSecretFromAssembly()
        {
            var asm = Assembly.LoadFrom(@"D:\New folder\Eddy\MachineInfo\bin\Debug\net8.0-windows\MachineInfo.dll");
            var provider = new EmbeddedResourceSecretProvider(asm);
            byte[] secret = provider.GetSecret();

            Assert.NotNull(secret);
            Assert.Equal(32, secret.Length);
        }
    }
}
