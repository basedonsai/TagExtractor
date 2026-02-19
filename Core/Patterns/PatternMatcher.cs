using System.Collections.Generic;
using System.Text.RegularExpressions;
using OCRTool.Core.Configuration;

namespace OCRTool.Core.Patterns
{
    /// <summary>
    /// Pattern matcher for extracting tags and equipment from text
    /// This is the core business logic - mirrors Python version
    /// OPTIMIZED: Cached Regex patterns and finds ALL matches
    /// </summary>
    public class PatternMatcher
    {
        private readonly List<RegexPattern> _patterns;
        private readonly HashSet<string> _phraseIndicators;
        private readonly HashSet<string> _equipmentKeywords;
        private readonly HashSet<string> _rejectLines;
        
        // Pre-compiled Regex patterns for performance
        private readonly Regex _pageRefRegex;
        private readonly Regex _pureNumberRegex;
        private readonly Regex _hasDigitRegex;
        private readonly Regex _whitespaceRegex;

        public PatternMatcher(ExtractionConfig config)
        {
            // Compile regex patterns from config
            _patterns = new List<RegexPattern>();
            foreach (var pattern in config.Patterns)
            {
                _patterns.Add(new RegexPattern
                {
                    Name = pattern.Name,
                    Type = pattern.Type,
                    Regex = new Regex(pattern.Regex, RegexOptions.IgnoreCase | RegexOptions.Compiled)
                });
            }

            // Pre-compile commonly used patterns
            _pageRefRegex = new Regex(@"^\d+\s+of\s+\d+$|^page\s+\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _pureNumberRegex = new Regex(@"^\d+$", RegexOptions.Compiled);
            _hasDigitRegex = new Regex(@"\d", RegexOptions.Compiled);
            _whitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

            // Phrase indicators (words that suggest it's a sentence, not a tag)
            _phraseIndicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "a", "an", "for", "with", "and", "or", "but", "to", "of", "in", "on", "at", "by",
                "from", "into", "onto", "about", "over", "under", "shall", "will", "must", "should",
                "ensure", "noted", "please", "provided", "according", "required", "responsible",
                "this", "that", "these", "those", "than", "then", "when", "where", "which", "who"
            };

            // Equipment keywords from config
            _equipmentKeywords = new HashSet<string>(config.EquipmentKeywords, StringComparer.OrdinalIgnoreCase);
            
            // Reject lines from config
            _rejectLines = new HashSet<string>(config.RejectLines, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Match text against configured patterns - finds ALL matches
        /// </summary>
        public PatternMatchResult Match(string text)
        {
            var result = new PatternMatchResult();
            
            if (string.IsNullOrEmpty(text))
            {
                System.Diagnostics.Debug.WriteLine("[PATTERN] Input text is null or empty!");
                return result;
            }

            System.Diagnostics.Debug.WriteLine($"[PATTERN] Processing text with length: {text.Length}");
            System.Diagnostics.Debug.WriteLine($"[PATTERN] Text preview: {text.Substring(0, Math.Min(200, text.Length)).Replace("\n", " ").Replace("\r", "")}");
            System.Diagnostics.Debug.WriteLine($"[PATTERN] Number of patterns: {_patterns.Count}");

            // Python's exact processing logic
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var lineText = Normalize(line);
                if (lineText.Length < 2)
                    continue;
                    
                // Python's looks_like_header check
                if (lineText.Length > 60 && 
                    (lineText.ToUpper().Contains("GA") || lineText.ToUpper().Contains("DRAWING") || lineText.ToUpper().Contains("PACKAGE")) &&
                    !Regex.IsMatch(lineText.ToUpper(), @"\d{3,}"))
                    continue;
                    
                // Python's reject_lines check
                if (_rejectLines.Any(rl => lineText.ToUpper().Contains(rl)))
                    continue;

                // Python's split_chunks logic
                var chunks = SplitChunks(lineText);
                
                foreach (var chunk in chunks)
                {
                    if (string.IsNullOrEmpty(chunk) || chunk.Length < 2)
                        continue;
                    
                    // Skip page references
                    if (Regex.IsMatch(chunk.Trim(), @"^\d+\s+of\s+\d+$", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(chunk.Trim(), @"^page\s+\d+$", RegexOptions.IgnoreCase))
                        continue;
                        
                    // Skip pure numbers
                    if (Regex.IsMatch(chunk.Trim(), @"^\d+$"))
                        continue;
                        
                    // Skip phrases
                    if (IsLikelyPhrase(chunk))
                        continue;
                    
                    // Pattern match first - Python's match_pattern logic
                    var patternMatch = MatchPattern(chunk);
                    if (patternMatch != null)
                    {
                        result.AddMatch(patternMatch.Type, chunk);
                        continue;
                    }
                    
                    // Equipment fallback - Python's exact logic
                    var upper = chunk.ToUpper();
                    if (_equipmentKeywords.Any(k => upper.Contains(k)) && Regex.IsMatch(chunk, @"\d"))
                    {
                        // Must not look like a phrase
                        if (IsLikelyPhrase(chunk))
                            continue;
                        // Length check
                        if (chunk.Length > 100)
                            continue;
                        // Must not be just a number with keyword
                        if (Regex.IsMatch(chunk.Trim(), @"^\d+$"))
                            continue;
                        result.AddMatch("EQUIP", chunk);
                    }
                }
            }
            
            return result;
        }
        
        // Add debug summary at the end of Match method
        private void LogMatchResult(PatternMatchResult result, string originalText)
        {
            System.Diagnostics.Debug.WriteLine($"[PATTERN] RESULT SUMMARY for text length {originalText.Length}:");
            System.Diagnostics.Debug.WriteLine($"[PATTERN] - Tags found: {result.Tags.Count}");
            foreach (var tag in result.Tags.Take(10)) // Show first 10
            {
                System.Diagnostics.Debug.WriteLine($"[PATTERN]   - {tag.Type}: {tag.Value}");
            }
            if (result.Tags.Count > 10)
            {
                System.Diagnostics.Debug.WriteLine($"[PATTERN]   ... and {result.Tags.Count - 10} more tags");
            }
            
            System.Diagnostics.Debug.WriteLine($"[PATTERN] - Equipment found: {result.Equipment.Count}");
            foreach (var equip in result.Equipment.Take(5)) // Show first 5
            {
                System.Diagnostics.Debug.WriteLine($"[PATTERN]   - EQUIP: {equip.Value}");
            }
            if (result.Equipment.Count > 5)
            {
                System.Diagnostics.Debug.WriteLine($"[PATTERN]   ... and {result.Equipment.Count - 5} more equipment");
            }
        }

        private List<string> SplitIntoLines(string text)
        {
            var lines = new List<string>();
            
            // First try natural line breaks
            var naturalLines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in naturalLines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;
                    
                // If line is very long, try to split it further
                if (trimmed.Length > 200)
                {
                    // Try to split by sentence endings
                    var sentences = Regex.Split(trimmed, @"(?<=[.!?])\s+");
                    foreach (var sentence in sentences)
                    {
                        var s = sentence.Trim();
                        if (s.Length > 0)
                        {
                            if (s.Length > 150)
                            {
                                // Split long sentences by common delimiters
                                var parts = Regex.Split(s, @"[,;]\s*");
                                foreach (var part in parts)
                                {
                                    var p = part.Trim();
                                    if (p.Length > 0)
                                        lines.Add(p);
                                }
                            }
                            else
                            {
                                lines.Add(s);
                            }
                        }
                    }
                }
                else
                {
                    lines.Add(trimmed);
                }
            }
            
            return lines;
        }

        private bool ShouldRejectLine(string line)
        {
            var trimmed = line.Trim();
            var upperLine = trimmed.ToUpper();
            
            // Python's looks_like_header logic - but be more conservative
            if (trimmed.Length > 60 && 
                (upperLine.Contains("GA") || upperLine.Contains("DRAWING") || upperLine.Contains("PACKAGE")) &&
                !Regex.IsMatch(upperLine, @"\d{3,}") &&
                !Regex.IsMatch(upperLine, @"EC-\w+-\d{6}")) // Don't reject if it contains equipment tags
            {
                System.Diagnostics.Debug.WriteLine($"[PATTERN] Rejected header line: {trimmed.Substring(0, Math.Min(50, trimmed.Length))}...");
                return true;
            }
            
            // Python's reject_lines logic
            foreach (var rejectWord in _rejectLines)
            {
                if (upperLine.Contains(rejectWord))
                {
                    // But don't reject if it contains equipment tags
                    if (Regex.IsMatch(upperLine, @"EC-\w+-\d{6}|ES-\w+-\d{6}|ACS880-\d{2}-\d{4}-\d"))
                    {
                        System.Diagnostics.Debug.WriteLine($"[PATTERN] NOT rejecting line with equipment tag despite '{rejectWord}': {trimmed.Substring(0, Math.Min(50, trimmed.Length))}...");
                        return false;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[PATTERN] Rejected line containing '{rejectWord}': {trimmed.Substring(0, Math.Min(50, trimmed.Length))}...");
                    return true;
                }
            }
            
            return false;
        }

        private List<string> SplitChunks(string line)
        {
            // Python's exact splitting logic - simple and consistent
            var chunks = new List<string>();
            
            // Python: parts = re.split(r"\||:|\bAPPLICATION\b|\bCOOLING METHOD\b|\s{2,}| - ", line, flags=re.IGNORECASE)
            var parts = Regex.Split(line, @"\||:|\bAPPLICATION\b|\bCOOLING METHOD\b|\s{2,}| - ", RegexOptions.IgnoreCase);
            
            foreach (var part in parts)
            {
                var normalized = Normalize(part);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    chunks.Add(normalized);
                }
            }
            
            return chunks;
        }

        private bool IsPageReference(string text)
        {
            var trimmed = text.Trim();
            return _pageRefRegex.IsMatch(trimmed);
        }

        private RegexPattern? MatchPattern(string text)
        {
            var trimmed = text.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return null;

            // Python's exact logic: prefer fullmatch, fallback to search
            // No prioritization - simple and effective
            
            // First try full match on all patterns
            foreach (var pattern in _patterns)
            {
                var match = pattern.Regex.Match(trimmed);
                if (match.Success && match.Value == trimmed) // Full match only
                {
                    pattern.MatchedValue = trimmed;
                    return pattern;
                }
            }
            
            // Then fallback to partial search on all patterns
            foreach (var pattern in _patterns)
            {
                var match = pattern.Regex.Match(trimmed);
                if (match.Success)
                {
                    pattern.MatchedValue = match.Value;
                    return pattern;
                }
            }

            return null;
        }

        private bool IsPureNumber(string text)
        {
            return _pureNumberRegex.IsMatch(text.Trim());
        }

        private bool IsLikelyPhrase(string text)
        {
            // Python's exact is_likely_phrase logic
            var textLower = text.ToLower();
            
            // Check for articles or prepositions
            if (Regex.IsMatch(textLower, @"\b(the|a|an|for|with|and|or|but|to|of|in|on|at|by|from)\b"))
                return true;
            
            // Check for modal/action words
            if (Regex.IsMatch(textLower, @"\b(shall|will|must|should|ensure|noted|please|provided|according|required)\b"))
                return true;
            
            // Check for relative words
            if (Regex.IsMatch(textLower, @"\b(than|then|when|where|which|who|that|this)\b"))
                return true;
            
            return false;
        }

        private bool IsEquipment(string text)
        {
            // Python's exact equipment detection logic - simple and effective
            var upper = text.ToUpper();
            var hasKeyword = _equipmentKeywords.Any(k => upper.Contains(k));
            var hasDigit = _hasDigitRegex.IsMatch(text);
            var lengthOk = text.Length <= 100;
            var notJustNumber = !_pureNumberRegex.IsMatch(text.Trim());
            
            // Basic requirements - exactly as Python
            if (!hasKeyword || !hasDigit || !lengthOk || !notJustNumber)
                return false;
                
            // Must not look like a phrase
            if (IsLikelyPhrase(text))
                return false;
                
            // Must not be just a number with keyword  
            if (Regex.IsMatch(text.Trim(), @"^\d+$"))
                return false;
                
            // Python doesn't have additional pattern checks - it's this simple
            return true;
        }

        private string Normalize(string text)
        {
            return _whitespaceRegex.Replace(text, " ").Trim();
        }
    }

    public class RegexPattern
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "TAG";
        public Regex Regex { get; set; } = null!;
        // Cached matched value for performance
        public string MatchedValue { get; set; } = string.Empty;
        public string Value => MatchedValue; // Alias for compatibility
    }

    public class PatternMatchResult
    {
        public List<TagMatch> Tags { get; } = new List<TagMatch>();
        public List<EquipmentMatch> Equipment { get; } = new List<EquipmentMatch>();

        public void AddMatch(string type, string value)
        {
            // Clean up the value
            var cleanValue = value.Trim();
            
            System.Diagnostics.Debug.WriteLine($"[ADD_MATCH] Adding: type={type}, value='{cleanValue}'");
            
            // Skip empty or very short values
            if (string.IsNullOrWhiteSpace(cleanValue) || cleanValue.Length < 2)
            {
                System.Diagnostics.Debug.WriteLine($"[ADD_MATCH] Skipped empty/short value: '{cleanValue}'");
                return;
            }
            
            // Skip common non-industrial words
            var upperValue = cleanValue.ToUpper();
            var skipWords = new HashSet<string> { "THE", "AND", "OR", "FOR", "WITH", "TO", "OF", "IN", "ON", "AT", "BY", "FROM", "IS", "ARE", "WAS", "WERE", "BE", "BEEN", "HAVE", "HAS", "HAD", "DO", "DOES", "DID", "WILL", "WOULD", "COULD", "SHOULD", "MAY", "MIGHT", "CAN", "MUST", "SHALL" };
            
            if (skipWords.Contains(upperValue))
            {
                System.Diagnostics.Debug.WriteLine($"[ADD_MATCH] Skipped common word: '{cleanValue}'");
                return;
            }
            
            if (type == "EQUIP")
            {
                // Check for duplicates
                if (!Equipment.Any(e => e.Value.Equals(cleanValue, StringComparison.OrdinalIgnoreCase)))
                {
                    Equipment.Add(new EquipmentMatch { Value = cleanValue });
                    System.Diagnostics.Debug.WriteLine($"[ADD_MATCH] Added equipment: '{cleanValue}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ADD_MATCH] Duplicate equipment skipped: '{cleanValue}'");
                }
            }
            else
            {
                // Check for duplicates
                if (!Tags.Any(t => t.Value.Equals(cleanValue, StringComparison.OrdinalIgnoreCase)))
                {
                    Tags.Add(new TagMatch { Type = type, Value = cleanValue });
                    System.Diagnostics.Debug.WriteLine($"[ADD_MATCH] Added tag: type={type}, value='{cleanValue}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ADD_MATCH] Duplicate tag skipped: type={type}, value='{cleanValue}'");
                }
            }
        }
    }

    public class TagMatch
    {
        public string Type { get; set; } = "TAG";
        public string Value { get; set; } = string.Empty;
        public double Confidence { get; set; } = 1.0;
    }

    public class EquipmentMatch
    {
        public string Value { get; set; } = string.Empty;
        public double Confidence { get; set; } = 1.0;
    }
}
