using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;

namespace OCRTool.Core.Classification
{
    /// <summary>
    /// Implements page classification logic by analyzing spatial distribution of layout tokens.
    /// Classifies pages as Table (structured rows/columns), Scattered (irregular distribution),
    /// or Sparse (minimal content) to enable intelligent extraction strategy selection.
    /// </summary>
    public class PageClassifier : IPageClassifier
    {
        private const int MinTokensForAnalysis = 10;
        private const int MinRowsForTable = 3;
        private const int MinColumnsForTable = 2;
        private const double RowTolerancePixels = 5.0;
        private const double ColumnTolerancePixels = 10.0;

        /// <summary>
        /// Classify page based on layout token distribution.
        /// Analyzes spatial distribution to determine page type.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens extracted from the page</param>
        /// <returns>PageClassification containing the determined PageType, reasoning, and row/column counts</returns>
        public PageClassification Classify(List<LayoutToken> tokens)
        {
            // Handle null or empty token collections
            if (tokens == null || tokens.Count == 0)
            {
                return new PageClassification
                {
                    PageType = PageType.Sparse,
                    Reasoning = "No tokens available for classification",
                    RowCount = 0,
                    ColumnCount = 0
                };
            }

            // Sparse check: fewer than 10 tokens
            if (tokens.Count < MinTokensForAnalysis)
            {
                return new PageClassification
                {
                    PageType = PageType.Sparse,
                    Reasoning = $"Token count ({tokens.Count}) below threshold ({MinTokensForAnalysis})",
                    RowCount = 0,
                    ColumnCount = 0
                };
            }

            // Check for table structure
            if (HasTableStructure(tokens, out int rowCount, out int columnCount))
            {
                return new PageClassification
                {
                    PageType = PageType.Table,
                    Reasoning = $"Detected {rowCount} rows and {columnCount} columns with regular alignment",
                    RowCount = rowCount,
                    ColumnCount = columnCount
                };
            }

            // Default to scattered
            return new PageClassification
            {
                PageType = PageType.Scattered,
                Reasoning = "Irregular token distribution without clear row/column alignment",
                RowCount = 0,
                ColumnCount = 0
            };
        }

        /// <summary>
        /// Detect table-like row and column alignment in the layout tokens.
        /// Clusters tokens by Y-coordinate (rows) and X-coordinate (columns).
        /// </summary>
        /// <param name="tokens">Collection of layout tokens to analyze</param>
        /// <param name="rowCount">Output parameter containing the number of distinct row clusters detected</param>
        /// <param name="columnCount">Output parameter containing the number of distinct column clusters detected</param>
        /// <returns>True if the page has table structure (at least 3 rows and 2 columns), false otherwise</returns>
        public bool HasTableStructure(List<LayoutToken> tokens, out int rowCount, out int columnCount)
        {
            // Cluster by Y coordinate (rows)
            var rowClusters = ClusterByCoordinate(tokens, t => t.Y, RowTolerancePixels);
            rowCount = rowClusters.Count;

            // Cluster by X coordinate (columns)
            var columnClusters = ClusterByCoordinate(tokens, t => t.X, ColumnTolerancePixels);
            columnCount = columnClusters.Count;

            // Check thresholds
            return rowCount >= MinRowsForTable && columnCount >= MinColumnsForTable;
        }

        /// <summary>
        /// Generic clustering algorithm that groups layout tokens by a specified coordinate.
        /// Sorts tokens by the coordinate and groups tokens within tolerance into clusters.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens to cluster</param>
        /// <param name="coordinateSelector">Function to extract the coordinate value from a token (e.g., t => t.Y for rows, t => t.X for columns)</param>
        /// <param name="tolerance">Maximum distance between tokens to be considered part of the same cluster</param>
        /// <returns>List of token clusters, where each cluster contains tokens with similar coordinate values</returns>
        private List<List<LayoutToken>> ClusterByCoordinate(
            List<LayoutToken> tokens,
            Func<LayoutToken, double> coordinateSelector,
            double tolerance)
        {
            // Handle null or empty input
            if (tokens == null || tokens.Count == 0)
                return new List<List<LayoutToken>>();

            var clusters = new List<List<LayoutToken>>();
            
            // Sort tokens by the specified coordinate
            var sortedTokens = tokens.OrderBy(coordinateSelector).ToList();

            foreach (var token in sortedTokens)
            {
                var coord = coordinateSelector(token);

                // Find cluster within tolerance
                var matchingCluster = clusters.FirstOrDefault(cluster =>
                {
                    var clusterCoord = coordinateSelector(cluster.First());
                    return Math.Abs(clusterCoord - coord) <= tolerance;
                });

                if (matchingCluster != null)
                {
                    // Add to existing cluster
                    matchingCluster.Add(token);
                }
                else
                {
                    // Create new cluster
                    clusters.Add(new List<LayoutToken> { token });
                }
            }

            return clusters;
        }
    }
}
