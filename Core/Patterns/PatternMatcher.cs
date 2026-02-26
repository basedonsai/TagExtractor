using OCRTool.Core.Configuration;
using OCRTool.Core.Models;
using System.Text.RegularExpressions;

namespace OCRTool.Core.Patterns
{
    public class PatternMatcher
    {
        private readonly List<(Regex regex, string type)> _compiledPatterns;

        public PatternMatcher(ExtractionConfig config)
        {
            _compiledPatterns = config.Patterns?
                .Where(p => !string.IsNullOrWhiteSpace(p.Regex))
                .Select(p => (
                    new Regex(p.Regex, RegexOptions.Compiled | RegexOptions.IgnoreCase),
                    p.Type?.ToUpper() ?? "TAG"
                ))
                .ToList()
                ?? new List<(Regex, string)>();
        }

        public ExtractionResult Match(
            string text,
            string sourceFile = "",
            int pageNumber = 0,
            double confidence = 100)
        {
            var result = new ExtractionResult
            {
                SourceFile = sourceFile,
                PageNumber = pageNumber,
                Confidence = confidence,
                RawText = text
            };

            if (string.IsNullOrWhiteSpace(text))
                return result;

            var uniqueTags = new Dictionary<string, string>();
            var uniqueEquipment = new HashSet<string>();

            foreach (var (regex, type) in _compiledPatterns)
            {
                var matches = regex.Matches(text);

                foreach (Match match in matches)
                {
                    var rawValue = match.Value.Trim();

                    if (string.IsNullOrWhiteSpace(rawValue))
                        continue;

                    var value = rawValue.Trim();
                    value = Regex.Replace(value, @"[^\w\-\+]+$", "");

                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    if (type == "EQUIP")
                    {
                        uniqueEquipment.Add(value);
                    }
                    else
                    {
                        if (!uniqueTags.ContainsKey(value))
                            uniqueTags.Add(value, type);
                    }
                }
            }

            result.Tags = uniqueTags
                .Select(t => new TagItem
                {
                    Value = t.Key,
                    Type = t.Value,
                    PageNumber = pageNumber,
                    SourceFile = sourceFile,
                    Confidence = confidence
                })
                .ToList();

            result.Equipment = uniqueEquipment
                .Select(e => new EquipmentItem
                {
                    Value = e,
                    PageNumber = pageNumber,
                    SourceFile = sourceFile,
                    Confidence = confidence
                })
                .ToList();

            return result;
        }
        private string CleanMergedTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            // Stop at first lowercase letter (OCR merges words like 640052Noted)
            int lowerIndex = value.IndexOfAny("abcdefghijklmnopqrstuvwxyz".ToCharArray());
            if (lowerIndex > 0)
                return value.Substring(0, lowerIndex);

            // Stop at first letter after long numeric block (e.g., 640052Page)
            var match = Regex.Match(value, @"^[A-Z0-9+\-]+");
            return match.Success ? match.Value : value;
        }
    }
}
