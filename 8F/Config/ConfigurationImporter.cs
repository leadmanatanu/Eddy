using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace _8F.Services
{
    public static class ConfigurationImporter
    {
        public static List<ChannelData> ApplyRemapping(List<ChannelData> incomingChannels, Dictionary<int, List<int>> mappings, bool isImportAsIs)
        {
            if (isImportAsIs || mappings == null || mappings.Count == 0)
            {
                return incomingChannels;
            }
            
            List<ChannelData> resultChannels = DeviceCOM.channelDatas != null && DeviceCOM.channelDatas.Count >= 4
                ? DeviceCOM.channelDatas
                : new List<ChannelData>
                {
                    new ChannelData { Id = 1, IsSeleted = true, TxStrength = 100, graphDatas = new List<GraphData>() },
                    new ChannelData { Id = 2, IsSeleted = true, TxStrength = 100, graphDatas = new List<GraphData>() },
                    new ChannelData { Id = 3, IsSeleted = true, TxStrength = 100, graphDatas = new List<GraphData>() },
                    new ChannelData { Id = 4, IsSeleted = true, TxStrength = 100, graphDatas = new List<GraphData>() }
                };

            foreach (var kvp in mappings)
            {
                int srcId = kvp.Key;
                List<int> targetIds = kvp.Value;
                var srcChannel = incomingChannels.FirstOrDefault(c => c.Id == srcId);

                if (srcChannel != null && targetIds != null)
                {
                    foreach (int targetId in targetIds)
                    {
                        var targetChannel = resultChannels.FirstOrDefault(c => c.Id == targetId);
                        if (targetChannel != null)
                        {
                            targetChannel.IsSeleted = srcChannel.IsSeleted;
                            targetChannel.TxStrength = srcChannel.TxStrength;
                            CopyChannelFrequencySettings(srcChannel, targetChannel);
                        }
                    }
                }
            }

            return resultChannels;
        }

        public static void CopyChannelFrequencySettings(ChannelData sourceChannel, ChannelData targetChannel)
        {
            if (sourceChannel?.graphDatas == null) return;
            if (targetChannel.graphDatas == null) targetChannel.graphDatas = new List<GraphData>();

            foreach (var sourceGraph in sourceChannel.graphDatas)
            {
                var targetGraph = targetChannel.graphDatas.FirstOrDefault(g => g.Id == sourceGraph.Id);
                if (targetGraph == null)
                {
                    targetGraph = new GraphData
                    {
                        Id = sourceGraph.Id,
                        Name = sourceGraph.Name ?? $"D{sourceGraph.Id}"
                    };
                    targetChannel.graphDatas.Add(targetGraph);
                }

                targetGraph.freq = sourceGraph.freq;
                targetGraph.gain = sourceGraph.gain;
                targetGraph.phase = sourceGraph.phase;
                targetGraph.isEnable = sourceGraph.isEnable;
                targetGraph.strength = sourceGraph.strength;
                targetGraph.postGain = sourceGraph.postGain;

                targetGraph.height = sourceGraph.height;
                targetGraph.width = sourceGraph.width;
                targetGraph.ex = sourceGraph.ex;
                targetGraph.ey = sourceGraph.ey;
                targetGraph.angel = sourceGraph.angel;

                targetGraph.height_O = sourceGraph.height_O;
                targetGraph.width_O = sourceGraph.width_O;
                targetGraph.ex_O = sourceGraph.ex_O;
                targetGraph.ey_O = sourceGraph.ey_O;
                targetGraph.angel_O = sourceGraph.angel_O;

                if (sourceGraph.ellipses != null)
                {
                    targetGraph.ellipses = sourceGraph.ellipses.Select(e => new Ellips
                    {
                        Id = e.Id,
                        height = e.height,
                        width = e.width,
                        ex = e.ex,
                        ey = e.ey,
                        angel = e.angel
                    }).ToList();
                }
            }
        }
        public static List<ChannelData>? ImportFromJson(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent)) return null;

            try
            {
                // First try standard ChannelData format
                var standard = JsonConvert.DeserializeObject<List<ChannelData>>(jsonContent);
                if (standard != null && standard.Count > 0 && standard.Any(c => c.graphDatas != null && c.graphDatas.Count > 0))
                {
                    return standard;
                }
            }
            catch { }

            try
            {
                // Try pretty-printed formatted JSON
                var formatted = JsonConvert.DeserializeObject<List<FormattedJsonChannel>>(jsonContent);
                if (formatted != null && formatted.Count > 0)
                {
                    List<ChannelData> result = new();
                    foreach (var fCh in formatted)
                    {
                        ChannelData ch = new ChannelData
                        {
                            Id = fCh.ChannelNumber,
                            IsSeleted = fCh.IsSelected,
                            TxStrength = fCh.TxStrength,
                            graphDatas = new List<GraphData>()
                        };

                        if (fCh.Frequencies != null)
                        {
                            foreach (var fFreq in fCh.Frequencies)
                            {
                                GraphData g = new GraphData
                                {
                                    Id = fFreq.FrequencyNumber,
                                    Name = fFreq.FrequencyName ?? $"D{fFreq.FrequencyNumber}",
                                    freq = fFreq.FrequencyHz,
                                    gain = fFreq.Gain,
                                    phase = fFreq.Phase,
                                    isEnable = fFreq.IsEnabled,
                                    strength = fFreq.Strength,
                                    postGain = fFreq.PostGain,
                                    ex = fFreq.DefaultCenterX,
                                    ey = fFreq.DefaultCenterY,
                                    width = fFreq.DefaultWidth,
                                    height = fFreq.DefaultHeight,
                                    angel = fFreq.DefaultRotationAngle,
                                    ex_O = fFreq.OverlayCenterX,
                                    ey_O = fFreq.OverlayCenterY,
                                    width_O = fFreq.OverlayWidth,
                                    height_O = fFreq.OverlayHeight,
                                    angel_O = fFreq.OverlayRotationAngle,
                                    ellipses = new List<Ellips>()
                                };

                                if (fFreq.Ellipses != null)
                                {
                                    foreach (var fEll in fFreq.Ellipses)
                                    {
                                        g.ellipses.Add(new Ellips
                                        {
                                            Id = fEll.EllipseIndex,
                                            ex = fEll.CenterX,
                                            ey = fEll.CenterY,
                                            width = fEll.Width,
                                            height = fEll.Height,
                                            angel = fEll.RotationAngle
                                        });
                                    }
                                }

                                ch.graphDatas.Add(g);
                            }
                        }

                        result.Add(ch);
                    }
                    return result;
                }
            }
            catch { }

            return null;
        }

        private class FormattedJsonChannel
        {
            public int ChannelNumber { get; set; }
            public bool IsSelected { get; set; }
            public int TxStrength { get; set; }
            public List<FormattedJsonFreq>? Frequencies { get; set; }
        }

        private class FormattedJsonFreq
        {
            public int FrequencyNumber { get; set; }
            public string? FrequencyName { get; set; }
            public int FrequencyHz { get; set; }
            public int Gain { get; set; }
            public int Phase { get; set; }
            public bool IsEnabled { get; set; }
            public int Strength { get; set; }
            public int PostGain { get; set; }
            public double DefaultCenterX { get; set; }
            public double DefaultCenterY { get; set; }
            public double DefaultWidth { get; set; }
            public double DefaultHeight { get; set; }
            public double DefaultRotationAngle { get; set; }
            public double OverlayCenterX { get; set; }
            public double OverlayCenterY { get; set; }
            public double OverlayWidth { get; set; }
            public double OverlayHeight { get; set; }
            public double OverlayRotationAngle { get; set; }
            public List<FormattedJsonEll>? Ellipses { get; set; }
        }

        private class FormattedJsonEll
        {
            public int EllipseIndex { get; set; }
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double RotationAngle { get; set; }
        }
        public static string DumpChannelDatas(List<ChannelData>? channels)
        {
            if (channels == null) return "null";
            var sb = new StringBuilder();
            foreach (var ch in channels.OrderBy(c => c.Id))
            {
                sb.Append($"[Ch{ch.Id}:Sel={ch.IsSeleted}:Tx={ch.TxStrength}:Graphs=(");
                if (ch.graphDatas != null)
                {
                    foreach (var g in ch.graphDatas.OrderBy(x => x.Id))
                    {
                        sb.Append($"D{g.Id}:F={g.freq}:G={g.gain}:P={g.phase}:En={g.isEnable};");
                    }
                }
                sb.Append(")] ");
            }
            return sb.ToString().Trim();
        }
    }
}