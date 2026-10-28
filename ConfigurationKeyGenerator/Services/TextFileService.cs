using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ConfigurationKeyGenerator.Services
{
    public class TextFileService
    {
        private static readonly Regex XmlAddRegex = new(
            @"key\s*=\s*""([^""]+)""\s+value\s*=\s*""([^""]*)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Dictionary<string, string> ReadConfiguration(string filePath)
        {
            Dictionary<string, string> data = new(StringComparer.OrdinalIgnoreCase);

            foreach (string line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmed = line.Trim();

                if (trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                    continue;

                // Support XML <add key="..." value="..." /> from App.config
                if (trimmed.StartsWith("<add", StringComparison.OrdinalIgnoreCase))
                {
                    var match = XmlAddRegex.Match(trimmed);
                    if (match.Success)
                    {
                        data[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
                        continue;
                    }
                }

                // Standard Key=Value
                string[] parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    data[parts[0].Trim()] = parts[1].Trim();
                }
            }

            return data;
        }
    }
}
