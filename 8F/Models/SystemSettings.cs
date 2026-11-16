using System.Collections.Generic;

namespace _8F.Models
{
    public class SystemSettings
    {
        public int ChannelCount { get; set; } = 2;
        public int FrequencyNo { get; set; } = 8;
        public double Factor { get; set; } = 1.0;
        public int DefaultHeight { get; set; } = 120;
        public int DefaultWidth { get; set; } = 120;
        public int DefaultHeight_O { get; set; } = 650;
        public int DefaultWidth_O { get; set; } = 650;
        public int DefaultAngel_O { get; set; } = 30;
        public int AppMode { get; set; } = 0;
        public int Mode { get; set; } = 1;
    }

    public class ProfileConfigFile
    {
        public int Version { get; set; } = 2;
        public string? ProfileName { get; set; }
        public SystemSettings? SystemConfig { get; set; }
        public List<ChannelData>? Channels { get; set; }
    }
}
