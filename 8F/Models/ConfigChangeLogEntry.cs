using System;

namespace _8F.Models
{
    public class ConfigChangeLogEntry
    {
        public long Id { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string ChangedByRole { get; set; } = "Admin";
        public string Area { get; set; } = string.Empty;
        public string? Channel { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
