using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using _8F.Models;

namespace _8F.Services
{
    public class ConfigSnapshot
    {
        public List<ChannelSnapshot> Channels { get; set; } = new();
    }

    public class ChannelSnapshot
    {
        public int Id { get; set; }
        public int TxStrength { get; set; }
        public bool IsSelected { get; set; }
        public List<GraphSnapshot> Graphs { get; set; } = new();
    }

    public class GraphSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Freq { get; set; }
        public int Gain { get; set; }
        public int Phase { get; set; }
        public bool IsEnable { get; set; }
        public int Strength { get; set; }
        public int PostGain { get; set; }
        public List<EllipseSnapshot> Ellipses { get; set; } = new();
    }

    public class EllipseSnapshot
    {
        public int Id { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public double Ex { get; set; }
        public double Ey { get; set; }
        public double Angle { get; set; }
    }

    public class FieldDiff
    {
        public string Field { get; set; } = string.Empty;
        public string OldValue { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
    }

    public static class ConfigSnapshotService
    {
        public static ConfigSnapshot TakeSnapshot()
        {
            var snap = new ConfigSnapshot();
            if (DeviceCOM.channelDatas == null) return snap;

            foreach (var ch in DeviceCOM.channelDatas)
            {
                var chSnap = new ChannelSnapshot
                {
                    Id = ch.Id,
                    TxStrength = ch.TxStrength,
                    IsSelected = ch.IsSeleted
                };

                if (ch.graphDatas != null)
                {
                    foreach (var g in ch.graphDatas)
                    {
                        var gSnap = new GraphSnapshot
                        {
                            Id = g.Id,
                            Name = g.Name ?? $"D{g.Id}",
                            Freq = g.freq,
                            Gain = g.gain,
                            Phase = g.phase,
                            IsEnable = g.isEnable,
                            Strength = g.strength,
                            PostGain = g.postGain
                        };

                        if (g.ellipses != null)
                        {
                            foreach (var el in g.ellipses)
                            {
                                gSnap.Ellipses.Add(new EllipseSnapshot
                                {
                                    Id = el.Id,
                                    Height = el.height,
                                    Width = el.width,
                                    Ex = el.ex,
                                    Ey = el.ey,
                                    Angle = el.angel
                                });
                            }
                        }

                        chSnap.Graphs.Add(gSnap);
                    }
                }

                snap.Channels.Add(chSnap);
            }

            return snap;
        }

        public static (string Summary, string DetailsJson)? Compare(ConfigSnapshot before, ConfigSnapshot after)
        {
            if (before == null || after == null) return null;

            var diffs = new List<FieldDiff>();

            foreach (var afterCh in after.Channels)
            {
                var beforeCh = before.Channels.FirstOrDefault(c => c.Id == afterCh.Id);
                if (beforeCh == null) continue;

                if (beforeCh.TxStrength != afterCh.TxStrength)
                {
                    diffs.Add(new FieldDiff
                    {
                        Field = $"CH{afterCh.Id} TxStrength",
                        OldValue = beforeCh.TxStrength.ToString(),
                        NewValue = afterCh.TxStrength.ToString()
                    });
                }

                foreach (var afterG in afterCh.Graphs)
                {
                    var beforeG = beforeCh.Graphs.FirstOrDefault(g => g.Id == afterG.Id);
                    if (beforeG == null) continue;

                    string prefix = $"CH{afterCh.Id} {afterG.Name}";

                    if (beforeG.Freq != afterG.Freq)
                    {
                        diffs.Add(new FieldDiff
                        {
                            Field = $"{prefix} Freq",
                            OldValue = $"{beforeG.Freq}Hz",
                            NewValue = $"{afterG.Freq}Hz"
                        });
                    }

                    if (beforeG.Gain != afterG.Gain)
                    {
                        diffs.Add(new FieldDiff
                        {
                            Field = $"{prefix} Gain",
                            OldValue = beforeG.Gain.ToString(),
                            NewValue = afterG.Gain.ToString()
                        });
                    }

                    if (beforeG.Phase != afterG.Phase)
                    {
                        diffs.Add(new FieldDiff
                        {
                            Field = $"{prefix} Phase",
                            OldValue = $"{beforeG.Phase}°",
                            NewValue = $"{afterG.Phase}°"
                        });
                    }

                    if (beforeG.IsEnable != afterG.IsEnable)
                    {
                        diffs.Add(new FieldDiff
                        {
                            Field = $"{prefix} Status",
                            OldValue = beforeG.IsEnable ? "ON" : "OFF",
                            NewValue = afterG.IsEnable ? "ON" : "OFF"
                        });
                    }

                    int maxEll = Math.Max(beforeG.Ellipses.Count, afterG.Ellipses.Count);
                    for (int i = 0; i < maxEll; i++)
                    {
                        var bEll = i < beforeG.Ellipses.Count ? beforeG.Ellipses[i] : null;
                        var aEll = i < afterG.Ellipses.Count ? afterG.Ellipses[i] : null;

                        if (bEll == null && aEll != null)
                        {
                            diffs.Add(new FieldDiff
                            {
                                Field = $"{prefix} Ellipse#{aEll.Id}",
                                OldValue = "None",
                                NewValue = $"Added (H:{aEll.Height:0.#}, W:{aEll.Width:0.#})"
                            });
                        }
                        else if (bEll != null && aEll == null)
                        {
                            diffs.Add(new FieldDiff
                            {
                                Field = $"{prefix} Ellipse#{bEll.Id}",
                                OldValue = $"H:{bEll.Height:0.#}, W:{bEll.Width:0.#}",
                                NewValue = "Removed"
                            });
                        }
                        else if (bEll != null && aEll != null)
                        {
                            if (Math.Abs(bEll.Height - aEll.Height) > 0.1)
                            {
                                diffs.Add(new FieldDiff
                                {
                                    Field = $"{prefix} Height",
                                    OldValue = bEll.Height.ToString("0.#"),
                                    NewValue = aEll.Height.ToString("0.#")
                                });
                            }

                            if (Math.Abs(bEll.Width - aEll.Width) > 0.1)
                            {
                                diffs.Add(new FieldDiff
                                {
                                    Field = $"{prefix} Width",
                                    OldValue = bEll.Width.ToString("0.#"),
                                    NewValue = aEll.Width.ToString("0.#")
                                });
                            }

                            if (Math.Abs(bEll.Ex - aEll.Ex) > 0.1 || Math.Abs(bEll.Ey - aEll.Ey) > 0.1)
                            {
                                diffs.Add(new FieldDiff
                                {
                                    Field = $"{prefix} Center",
                                    OldValue = $"({bEll.Ex:0.#},{bEll.Ey:0.#})",
                                    NewValue = $"({aEll.Ex:0.#},{aEll.Ey:0.#})"
                                });
                            }

                            if (Math.Abs(bEll.Angle - aEll.Angle) > 0.1)
                            {
                                diffs.Add(new FieldDiff
                                {
                                    Field = $"{prefix} Angle",
                                    OldValue = $"{bEll.Angle:0.#}°",
                                    NewValue = $"{aEll.Angle:0.#}°"
                                });
                            }
                        }
                    }
                }
            }

            if (diffs.Count == 0)
                return null;

            var summaryParts = diffs.Select(d => $"{d.Field} {d.OldValue}→{d.NewValue}");
            string summary = string.Join(", ", summaryParts);
            if (summary.Length > 295)
            {
                summary = summary.Substring(0, 292) + "...";
            }

            string detailsJson = JsonSerializer.Serialize(diffs, new JsonSerializerOptions { WriteIndented = true });
            return (summary, detailsJson);
        }
    }
}
