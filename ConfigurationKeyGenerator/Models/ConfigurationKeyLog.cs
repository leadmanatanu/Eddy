using System;

namespace ConfigurationKeyGenerator.Models
{
    public class ConfigurationKeyLog
    {
        public int Id { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string MachineId { get; set; } = string.Empty;

        public string ConfigurationFileName { get; set; } = string.Empty;
            
        public string GeneratedFileName { get; set; } = string.Empty;

        public byte[] GeneratedFile { get; set; } = Array.Empty<byte>();

        public DateTime GeneratedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public string? SettingsJson { get; set; }
    }
}
