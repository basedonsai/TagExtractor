using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;
using OCRTool.Core.Patterns;

namespace OCRTool.Core.RecordBuilders
{
    /// <summary>
    /// Implements structured record building for scattered page layouts (e.g., P&ID drawings).
    /// Identifies TAG tokens as anchors and groups nearby elements based on spatial distance.
    /// </summary>
    public class ProximityEngine : IRecordBuilder
    {
        private const double DistanceThresholdPixels = 100.0;
        private readonly PatternMatcher _patternMatcher;

        /// <summary>
        /// Initializes a new instance of the ProximityEngine class.
        /// </summary>
        /// <param name="patternMatcher">Pattern matcher for identifying TAG patterns</param>
        public ProximityEngine(PatternMatcher patternMatcher)
        {
            _patternMatcher = patternMatcher ?? throw new ArgumentNullException(nameof(patternMatcher));
        }

        /// <summary>
        /// Check if this builder can process the given page type.
        /// ProximityEngine processes pages classified as Scattered.
        /// </summary>
        /// <param name="pageType">The page type classification</param>
        /// <returns>True if pageType is Scattered, false otherwise</returns>
        public bool CanProcess(PageType pageType) => pageType == PageType.Scattered;

        /// <summary>
        /// Build structured records from scattered layout tokens.
        /// Identifies TAG tokens as anchors, groups nearby tokens, and creates records.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens extracted from a page</param>
        /// <param name="pageNumber">Page number within the source document</param>
        /// <param name="sourceFile">Source file name</param>
        /// <returns>List of structured records extracted from proximity groups</returns>
        public List<StructuredRecord> BuildRecords(List<LayoutToken> tokens, int pageNumber, string sourceFile)
        {
            if (tokens == null || tokens.Count == 0)
                return new List<StructuredRecord>();

            // Task 12.1: Identify TAG tokens as anchors
            var anchorTokens = IdentifyTagTokens(tokens);

            if (anchorTokens.Count == 0)
                return new List<StructuredRecord>();

            var records = new List<StructuredRecord>();

            // For each anchor token, build a record with nearby elements
            foreach (var anchor in anchorTokens)
            {
                // Task 12.3: Group tokens within distance threshold and sort by distance
                var nearbyTokens = FindNearbyTokens(anchor, tokens, DistanceThresholdPixels);

                // Task 12.4: Classify grouped tokens as Equipment or Rating
                var equipment = new List<string>();
                var ratings = new List<string>();

                foreach (var token in nearbyTokens)
                {
                    if (IsEquipmentToken(token))
                        equipment.Add(token.Text);
                    else if (IsRatingToken(token))
                        ratings.Add(token.Text);
                }

                // Task 12.5: Build structured record from proximity group
                var record = new StructuredRecord
                {
                    Tag = anchor.Text,
                    Equipment = string.Join(" ", equipment),
                    Rating = string.Join(" ", ratings),
                    Source = sourceFile,
                    Page = pageNumber,
                    Method = ExtractionMethod.ProximityEngine
                };

                records.Add(record);
            }

            return records;
        }


        /// <summary>
        /// Identify TAG tokens from layout tokens using pattern matching.
        /// Uses the existing PatternMatcher to identify tokens that match TAG patterns.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens to filter</param>
        /// <returns>List of layout tokens that match TAG patterns (anchor tokens)</returns>
        private List<LayoutToken> IdentifyTagTokens(List<LayoutToken> tokens)
        {
            var tagTokens = new List<LayoutToken>();

            foreach (var token in tokens)
            {
                // Use PatternMatcher to check if token text matches TAG pattern
                // PatternMatcher.Match returns ExtractionResult with Tags collection
                var matchResult = _patternMatcher.Match(token.Text);

                // If the token text matches any TAG pattern, it's an anchor token
                if (matchResult.Tags != null && matchResult.Tags.Count > 0)
                {
                    tagTokens.Add(token);
                }
            }

            return tagTokens;
        }

        /// <summary>
        /// Find all tokens within the distance threshold from an anchor token.
        /// Calculates distance to all other tokens, filters by threshold, and sorts by distance.
        /// </summary>
        /// <param name="anchor">The anchor token (TAG token) to measure distances from</param>
        /// <param name="allTokens">All layout tokens on the page</param>
        /// <param name="threshold">Maximum distance in pixels for grouping</param>
        /// <returns>List of tokens within threshold, sorted by distance (nearest first), excluding the anchor itself</returns>
        private List<LayoutToken> FindNearbyTokens(LayoutToken anchor, List<LayoutToken> allTokens, double threshold)
        {
            var nearbyTokens = new List<(LayoutToken token, double distance)>();

            // Calculate distance from anchor to all other tokens
            foreach (var token in allTokens)
            {
                // Skip the anchor token itself
                if (token == anchor)
                    continue;

                // Calculate Euclidean distance
                var distance = CalculateDistance(anchor, token);

                // Filter tokens within threshold
                if (distance <= threshold)
                {
                    nearbyTokens.Add((token, distance));
                }
            }

            // Sort by distance (nearest first) and return just the tokens
            return nearbyTokens
                .OrderBy(t => t.distance)
                .Select(t => t.token)
                .ToList();
        }

        /// <summary>
        /// Calculate Euclidean distance between two layout tokens based on their center points.
        /// Uses the formula: sqrt((x1-x2)^2 + (y1-y2)^2)
        /// </summary>
        /// <param name="token1">First layout token</param>
        /// <param name="token2">Second layout token</param>
        /// <returns>Euclidean distance in pixels between the token centers</returns>
        private double CalculateDistance(LayoutToken token1, LayoutToken token2)
        {
            var dx = token1.CenterX - token2.CenterX;
            var dy = token1.CenterY - token2.CenterY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Determine if a token represents equipment (descriptive text).
        /// Equipment tokens are primarily alphabetic with possible hyphens/spaces.
        /// Examples: "Motor", "Pump", "Control-Valve", "Heat Exchanger"
        /// </summary>
        /// <param name="token">The layout token to classify</param>
        /// <returns>True if the token is classified as equipment, false otherwise</returns>
        private bool IsEquipmentToken(LayoutToken token)
        {
            if (string.IsNullOrWhiteSpace(token.Text))
                return false;

            var text = token.Text.Trim();

            // Check if token is primarily alphabetic (descriptive text)
            // Count alphabetic characters
            int alphaCount = text.Count(c => char.IsLetter(c));
            int digitCount = text.Count(c => char.IsDigit(c));
            int totalSignificant = alphaCount + digitCount;

            if (totalSignificant == 0)
                return false;

            // Equipment tokens should be primarily alphabetic (>50% letters)
            // This captures: "Motor", "Pump", "Valve", "Heat-Exchanger", etc.
            return alphaCount > digitCount;
        }

        /// <summary>
        /// Determine if a token represents a rating (numeric specification).
        /// Rating tokens contain numbers with optional units.
        /// Examples: "5HP", "10kW", "240V", "60Hz", "100PSI", "3.5A"
        /// </summary>
        /// <param name="token">The layout token to classify</param>
        /// <returns>True if the token is classified as a rating, false otherwise</returns>
        private bool IsRatingToken(LayoutToken token)
        {
            if (string.IsNullOrWhiteSpace(token.Text))
                return false;

            var text = token.Text.Trim();

            // Check if token contains digits (numeric specification)
            // Count alphabetic characters and digits
            int alphaCount = text.Count(c => char.IsLetter(c));
            int digitCount = text.Count(c => char.IsDigit(c));
            int totalSignificant = alphaCount + digitCount;

            if (totalSignificant == 0)
                return false;

            // Rating tokens should contain digits and be primarily numeric (>=50% digits)
            // This captures: "5HP", "10kW", "240V", "3.5A", "100", etc.
            return digitCount > 0 && digitCount >= alphaCount;
        }
    }
}
