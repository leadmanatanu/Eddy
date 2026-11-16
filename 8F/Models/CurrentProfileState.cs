using System.Collections.Generic;

namespace _8F.Models
{
    public sealed class ChannelProfileInfo
    {
        public int ChannelNumber { get; set; }
        public int? ProfileId { get; set; }
        public string? Name { get; set; }
        public string? FilePath { get; set; }
        public int? SourceChannelId { get; set; }
        public bool HasOpenProfile => (ProfileId.HasValue && ProfileId.Value > 0) || !string.IsNullOrWhiteSpace(FilePath);
    }

    public sealed class CurrentProfileState
    {
        private readonly Dictionary<int, ChannelProfileInfo> _channelProfiles = new();

        public int? ChannelNumber { get; private set; }
        public int? ProfileId { get; private set; }
        public string? Name { get; private set; }
        public string? FilePath { get; private set; }
        public int? SourceChannelId { get; private set; }

        public bool HasOpenProfile => (ProfileId.HasValue && ProfileId.Value > 0) || !string.IsNullOrWhiteSpace(FilePath);

        public void Set(int? profileId, string? name, string? filePath, int? channelNumber = null, int? sourceChannelId = null)
        {
            ProfileId = profileId is > 0 ? profileId : null;
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
            ChannelNumber = channelNumber;
            SourceChannelId = sourceChannelId;

            if (channelNumber.HasValue && channelNumber.Value >= 1 && channelNumber.Value <= 4)
            {
                _channelProfiles[channelNumber.Value] = new ChannelProfileInfo
                {
                    ChannelNumber = channelNumber.Value,
                    ProfileId = ProfileId,
                    Name = Name,
                    FilePath = FilePath,
                    SourceChannelId = SourceChannelId
                };
            }
        }

        public void SetForChannel(int channelNumber, int? profileId, string? name, string? filePath, int? sourceChannelId = null)
        {
            Set(profileId, name, filePath, channelNumber, sourceChannelId);
        }

        public ChannelProfileInfo? GetForChannel(int channelNumber)
        {
            if (_channelProfiles.TryGetValue(channelNumber, out var info))
            {
                return info;
            }
            if (HasOpenProfile && (ChannelNumber == channelNumber || !ChannelNumber.HasValue))
            {
                return new ChannelProfileInfo
                {
                    ChannelNumber = channelNumber,
                    ProfileId = ProfileId,
                    Name = Name,
                    FilePath = FilePath,
                    SourceChannelId = SourceChannelId
                };
            }
            return null;
        }

        public void SwitchActiveChannel(int channelNumber)
        {
            ChannelNumber = channelNumber;
            if (_channelProfiles.TryGetValue(channelNumber, out var info))
            {
                ProfileId = info.ProfileId;
                Name = info.Name;
                FilePath = info.FilePath;
                SourceChannelId = info.SourceChannelId;
            }
            else
            {
                ProfileId = null;
                Name = null;
                FilePath = null;
                SourceChannelId = null;
            }
        }

        public void ClearChannel(int channelNumber)
        {
            _channelProfiles.Remove(channelNumber);
            if (ChannelNumber == channelNumber)
            {
                Clear();
            }
        }

        public void Clear()
        {
            ProfileId = null;
            Name = null;
            FilePath = null;
            ChannelNumber = null;
            SourceChannelId = null;
            _channelProfiles.Clear();
        }
    }
}
