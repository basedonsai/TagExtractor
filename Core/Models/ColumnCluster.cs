using System.Collections.Generic;
using System.Linq;

namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents a group of layout tokens with similar X coordinates forming a vertical column.
    /// Used by the TableEngine to organize tokens into table columns.
    /// </summary>
    public class ColumnCluster
    {
        /// <summary>
        /// Collection of layout tokens in this column cluster
        /// </summary>
        public List<LayoutToken> Tokens { get; set; } = new List<LayoutToken>();

        /// <summary>
        /// Minimum X coordinate among all tokens in the cluster
        /// </summary>
        public double MinX => Tokens.Any() ? Tokens.Min(t => t.X) : 0;

        /// <summary>
        /// Maximum X coordinate among all tokens in the cluster
        /// </summary>
        public double MaxX => Tokens.Any() ? Tokens.Max(t => t.X) : 0;

        /// <summary>
        /// Average X coordinate of all tokens in the cluster
        /// </summary>
        public double AvgX => Tokens.Any() ? Tokens.Average(t => t.X) : 0;

        /// <summary>
        /// Index of this column in the table (0-based, left to right)
        /// </summary>
        public int ColumnIndex { get; set; }
    }
}
