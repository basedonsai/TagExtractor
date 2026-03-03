using System.Collections.Generic;
using System.Linq;

namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents a group of layout tokens with similar Y coordinates forming a horizontal row.
    /// Used by the TableEngine to organize tokens into table rows.
    /// </summary>
    public class RowCluster
    {
        /// <summary>
        /// Collection of layout tokens in this row cluster
        /// </summary>
        public List<LayoutToken> Tokens { get; set; } = new List<LayoutToken>();

        /// <summary>
        /// Minimum Y coordinate among all tokens in the cluster
        /// </summary>
        public double MinY => Tokens.Any() ? Tokens.Min(t => t.Y) : 0;

        /// <summary>
        /// Maximum Y coordinate among all tokens in the cluster
        /// </summary>
        public double MaxY => Tokens.Any() ? Tokens.Max(t => t.Y) : 0;

        /// <summary>
        /// Average Y coordinate of all tokens in the cluster
        /// </summary>
        public double AvgY => Tokens.Any() ? Tokens.Average(t => t.Y) : 0;
    }
}
