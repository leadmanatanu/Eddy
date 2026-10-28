using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MachineInfo.Application.Interfaces;

namespace MachineInfo.Infrastructure.Security
{
    /// <summary>
    /// Reads the 32-byte application secret from an embedded resource named Keys\config.secret.
    /// Never stores the secret as a literal in source code.
    /// </summary>
    public class EmbeddedResourceSecretProvider : IAppSecretProvider
    {
        private readonly Assembly _targetAssembly;
        private byte[]? _cachedSecret;

        public EmbeddedResourceSecretProvider(Assembly? targetAssembly = null)
        {
            _targetAssembly = targetAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        }

        public byte[] GetSecret()
        {
            if (_cachedSecret != null)
            {
                return _cachedSecret;
            }

            Stream? stream = FindResourceStream(_targetAssembly);
            if (stream == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.IsDynamic) continue;
                    stream = FindResourceStream(asm);
                    if (stream != null) break;
                }
            }

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "Embedded resource 'Keys\\config.secret' was not found in any loaded assembly. " +
                    "Ensure 'Keys\\config.secret' exists and is configured as an <EmbeddedResource>.");
            }

            using (stream)
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                byte[] bytes = ms.ToArray();
                if (bytes.Length != 32)
                {
                    throw new InvalidOperationException(
                        $"Embedded resource 'Keys\\config.secret' has invalid length ({bytes.Length} bytes). Expected exactly 32 bytes.");
                }
                _cachedSecret = bytes;
                return _cachedSecret;
            }
        }

        private static Stream? FindResourceStream(Assembly assembly)
        {
            try
            {
                string[] names = assembly.GetManifestResourceNames();
                string? match = names.FirstOrDefault(n =>
                    n.EndsWith("Keys.config.secret", StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith("config.secret", StringComparison.OrdinalIgnoreCase));
                return match != null ? assembly.GetManifestResourceStream(match) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
