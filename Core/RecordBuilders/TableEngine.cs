using OCRTool.Core.Interfaces;
using OCRTool.Core.Models;

namespace OCRTool.Core.RecordBuilders
{
    /// <summary>
    /// Implements structured record building for table-like page layouts.
    /// Clusters tokens into rows and columns, identifies headers, and extracts structured records.
    /// </summary>
    public class TableEngine : IRecordBuilder
    {
        private const double RowTolerancePixels = 5.0;
        private const double ColumnTolerancePixels = 10.0;

        /// <summary>
        /// Check if this builder can process the given page type.
        /// TableEngine processes pages classified as Table.
        /// </summary>
        /// <param name="pageType">The page type classification</param>
        /// <returns>True if pageType is Table, false otherwise</returns>
        public bool CanProcess(PageType pageType) => pageType == PageType.Table;

        /// <summary>
        /// Build structured records from table-like layout tokens.
        /// Clusters tokens into rows and columns, identifies header row, and extracts data records.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens extracted from a page</param>
        /// <param name="pageNumber">Page number within the source document</param>
        /// <param name="sourceFile">Source file name</param>
        /// <returns>List of structured records extracted from the table</returns>
        public List<StructuredRecord> BuildRecords(List<LayoutToken> tokens, int pageNumber, string sourceFile)
        {
            if (tokens == null || tokens.Count == 0)
                return new List<StructuredRecord>();

            // Cluster tokens by Y coordinate (rows)
            var rowClusters = ClusterByYCoordinate(tokens);

            // Cluster tokens by X coordinate (columns)
            var columnClusters = ClusterByXCoordinate(tokens);

            // Need at least 2 rows (header + data) and 1 column
            if (rowClusters.Count < 2 || columnClusters.Count < 1)
                return new List<StructuredRecord>();

            // Identify header row (first row) and map to field names
            var headerRow = rowClusters.First();
            var headerMapping = MapHeadersToFields(headerRow, columnClusters);

            // Build records from data rows (skip header row)
            var records = new List<StructuredRecord>();
            foreach (var row in rowClusters.Skip(1))
            {
                var record = BuildRecordFromRow(row, headerMapping, columnClusters, pageNumber, sourceFile);
                
                // Only add record if TAG field is populated
                if (record != null && !string.IsNullOrEmpty(record.Tag))
                {
                    records.Add(record);
                }
            }

            return records;
        }

        /// <summary>
        /// Cluster layout tokens by Y-coordinate to identify table rows.
        /// Tokens with Y-coordinates within 5 pixels are grouped into the same row.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens to cluster</param>
        /// <returns>List of row clusters, sorted by MinY (top to bottom)</returns>
        private List<RowCluster> ClusterByYCoordinate(List<LayoutToken> tokens)
        {
            // Reuse ClusterByCoordinate algorithm from PageClassifier
            var tokenClusters = ClusterByCoordinate(tokens, t => t.Y, RowTolerancePixels);

            // Convert to RowCluster objects
            var rowClusters = tokenClusters.Select(cluster => new RowCluster
            {
                Tokens = cluster
            }).ToList();

            // Sort row clusters by MinY (top to bottom)
            return rowClusters.OrderBy(r => r.MinY).ToList();
        }

        /// <summary>
        /// Cluster layout tokens by X-coordinate to identify table columns.
        /// Tokens with X-coordinates within 10 pixels are grouped into the same column.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens to cluster</param>
        /// <returns>List of column clusters with assigned column indices</returns>
        private List<ColumnCluster> ClusterByXCoordinate(List<LayoutToken> tokens)
        {
            // Reuse ClusterByCoordinate algorithm from PageClassifier
            var tokenClusters = ClusterByCoordinate(tokens, t => t.X, ColumnTolerancePixels);

            // Convert to ColumnCluster objects with column indices
            var columnClusters = new List<ColumnCluster>();
            int columnIndex = 0;

            // Sort clusters by MinX (left to right) and assign indices
            foreach (var cluster in tokenClusters.OrderBy(c => c.Min(t => t.X)))
            {
                columnClusters.Add(new ColumnCluster
                {
                    Tokens = cluster,
                    ColumnIndex = columnIndex++
                });
            }

            return columnClusters;
        }

        /// <summary>
        /// Generic clustering algorithm that groups layout tokens by a specified coordinate.
        /// This is the same algorithm used by PageClassifier for consistency.
        /// Sorts tokens by the coordinate and groups tokens within tolerance into clusters.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens to cluster</param>
        /// <param name="coordinateSelector">Function to extract the coordinate value from a token</param>
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

        /// <summary>
        /// Map header tokens to field names using fuzzy matching.
        /// Identifies which columns correspond to Tag, Equipment, Rating, and Description fields.
        /// </summary>
        /// <param name="headerRow">The first row cluster containing header tokens</param>
        /// <param name="columnClusters">List of column clusters with assigned indices</param>
        /// <returns>Dictionary mapping column index to field name</returns>
        private Dictionary<int, string> MapHeadersToFields(RowCluster headerRow, List<ColumnCluster> columnClusters)
        {
            var headerMapping = new Dictionary<int, string>();

            // For each column, find the header token in that column
            foreach (var column in columnClusters)
            {
                // Find header tokens that belong to this column
                // A token belongs to a column if its X coordinate is within the column's X range
                var headerTokensInColumn = headerRow.Tokens
                    .Where(token => IsTokenInColumn(token, column))
                    .ToList();

                if (headerTokensInColumn.Count == 0)
                    continue;

                // Use the first token if multiple tokens in the same column
                var headerToken = headerTokensInColumn.First();
                var headerText = headerToken.Text.ToLowerInvariant();

                // Fuzzy match header text to field names
                string? fieldName = null;

                if (headerText.Contains("tag"))
                    fieldName = "Tag";
                else if (headerText.Contains("equipment") || headerText.Contains("equip"))
                    fieldName = "Equipment";
                else if (headerText.Contains("rating"))
                    fieldName = "Rating";
                else if (headerText.Contains("desc") || headerText.Contains("description"))
                    fieldName = "Description";

                // Map column index to field name
                if (fieldName != null)
                {
                    headerMapping[column.ColumnIndex] = fieldName;
                }
            }

            return headerMapping;
        }

        /// <summary>
        /// Check if a token belongs to a specific column cluster.
        /// A token belongs to a column if its X coordinate is within the column tolerance.
        /// </summary>
        /// <param name="token">The layout token to check</param>
        /// <param name="column">The column cluster</param>
        /// <returns>True if the token belongs to the column, false otherwise</returns>
        private bool IsTokenInColumn(LayoutToken token, ColumnCluster column)
        {
            // Check if token's X coordinate is within tolerance of the column's average X
            return Math.Abs(token.X - column.AvgX) <= ColumnTolerancePixels;
        }

        /// <summary>
        /// Build a structured record from a single table row.
        /// Extracts tokens from each column and maps them to record fields using the header mapping.
        /// </summary>
        /// <param name="row">The row cluster containing tokens for this row</param>
        /// <param name="headerMapping">Dictionary mapping column index to field name</param>
        /// <param name="columnClusters">List of column clusters with assigned indices</param>
        /// <param name="pageNumber">Page number within the source document</param>
        /// <param name="sourceFile">Source file name</param>
        /// <returns>StructuredRecord if row contains data, null otherwise</returns>
        private StructuredRecord? BuildRecordFromRow(
            RowCluster row,
            Dictionary<int, string> headerMapping,
            List<ColumnCluster> columnClusters,
            int pageNumber,
            string sourceFile)
        {
            var record = new StructuredRecord
            {
                Source = sourceFile,
                Page = pageNumber,
                Method = ExtractionMethod.TableEngine
            };

            // For each column, extract tokens from this row
            foreach (var column in columnClusters)
            {
                // Find tokens in this row that belong to this column
                var tokensInCell = row.Tokens
                    .Where(token => IsTokenInColumn(token, column))
                    .OrderBy(token => token.X) // Sort left to right within cell
                    .ToList();

                if (tokensInCell.Count == 0)
                    continue;

                // Concatenate all tokens in the cell with space separator
                var cellText = string.Join(" ", tokensInCell.Select(t => t.Text));

                // Map to record field based on header mapping
                if (headerMapping.TryGetValue(column.ColumnIndex, out var fieldName))
                {
                    switch (fieldName)
                    {
                        case "Tag":
                            record.Tag = cellText;
                            break;
                        case "Equipment":
                            record.Equipment = cellText;
                            break;
                        case "Rating":
                            record.Rating = cellText;
                            break;
                        case "Description":
                            record.Description = cellText;
                            break;
                    }
                }
            }

            return record;
        }
    }
}
