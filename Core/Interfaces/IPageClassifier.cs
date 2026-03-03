using OCRTool.Core.Models;

namespace OCRTool.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for page classification components that analyze layout token
    /// distributions to determine page layout type and detect table structures.
    /// </summary>
    public interface IPageClassifier
    {
        /// <summary>
        /// Classify page based on layout token distribution.
        /// Analyzes spatial distribution of tokens to determine if the page has
        /// table-like structure, scattered layout, or sparse content.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens extracted from the page</param>
        /// <returns>PageClassification containing the determined PageType, reasoning, and row/column counts</returns>
        PageClassification Classify(List<LayoutToken> tokens);
        
        /// <summary>
        /// Detect table-like row and column alignment in the layout tokens.
        /// Clusters tokens by Y-coordinate (rows) and X-coordinate (columns) to determine
        /// if the page exhibits structured table characteristics.
        /// </summary>
        /// <param name="tokens">Collection of layout tokens to analyze</param>
        /// <param name="rowCount">Output parameter containing the number of distinct row clusters detected</param>
        /// <param name="columnCount">Output parameter containing the number of distinct column clusters detected</param>
        /// <returns>True if the page has table structure (at least 3 rows and 2 columns), false otherwise</returns>
        bool HasTableStructure(List<LayoutToken> tokens, out int rowCount, out int columnCount);
    }
}
