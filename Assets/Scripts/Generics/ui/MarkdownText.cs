using System;
using System.Text;
using UnityEngine;

namespace Baseline
{
    internal static class MarkdownText
    {
        public static string Format(string markdown, int titleSizePercent, int sectionSizePercent,
            int titleBlankLines, int sectionBlankLines, string bulletSymbol, Color bulletColor,
            int bulletSpacing, int listItemBlankLines)
        {
            var result = new StringBuilder();
            string bulletHex = ColorUtility.ToHtmlStringRGB(bulletColor);
            string[] lines = markdown.Replace("\r", string.Empty).Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                if (line.StartsWith("# ", StringComparison.Ordinal))
                    result.Append("<size=").Append(titleSizePercent).Append("%><b>")
                        .Append(line[2..]).Append("</b></size>").Append('\n', 1 + titleBlankLines);
                else if (line.StartsWith("## ", StringComparison.Ordinal))
                    result.Append("<size=").Append(sectionSizePercent).Append("%><b>")
                        .Append(line[3..]).Append("</b></size>").Append('\n', 1 + sectionBlankLines);
                else if (line.StartsWith("- ", StringComparison.Ordinal))
                    result.Append("<color=#").Append(bulletHex).Append('>').Append(bulletSymbol)
                        .Append("</color>").Append(' ', bulletSpacing).Append(line[2..])
                        .Append('\n', 1 + listItemBlankLines);
                else
                    result.Append(line).Append('\n');
            }
            return result.ToString().TrimEnd();
        }
    }
}
