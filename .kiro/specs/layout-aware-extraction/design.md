# Design Document: Layout-Aware Extraction

## Overview

This design transforms the OCR system from a text-based extraction engine into a layout-aware industrial document intelligence engine. The system currently processes industrial documents (P&ID drawings, equipment schedules, panel layouts) using a hybrid extraction approach with PdfPig for searchable PDFs and Tesseract OCR for scanned pages. The existing pipeline flattens pages into text blobs and uses regex patterns for extraction.

The transformation preserves the existing stable text extraction foundation while layering spatial intelligence on top. The system will extract and preserve layout information (bounding boxes, X/Y coordinates), classify pages by layout type, and apply appropriate structured extraction strategies (table-based or proximity-based) to build structured records from industrial documents.

### Key Design Principles

1. **Non-Breaking Enhancement**: All changes maintain backward compatibility with existing regex-based extraction
2. **Incremental Development**: Five priority-based phases ensure stable progression
3. **Hybrid Intelligence**: Combines spatial awareness with existing pattern matching
4. **Graceful Degradation**: Falls back to text-only extraction when layout processing fails
5. **Separation of Concerns**: Layout extraction, classification, and record building are independent components

### Transformation Goals

- Extract spatial metadata (bounding boxes) from both searchable PDFs and OCR results
- Classify pages by layout type (Table, Scattered, Sparse) based on token distribution
- Apply specialized extraction engines (Table Engine for structured layouts, Proximity Engine for scattered layouts)
- Maintain existing text extraction and regex matching as fallback mechanisms
- Export structured records with source tracking (file name, page number)


## Architecture

### System Architecture Overview

The layout-aware extraction system extends the existing architecture with three new layers:

```
┌─────────────────────────────────────────────────────────────┐
│                      BatchProcessor                          │
│  (Orchestration: file processing, progress, export)         │
└────────────┬────────────────────────────────────────────────┘
             │
             ├──────────────────────────────────────────────────┐
             │                                                   │
┌────────────▼──────────┐                    ┌─────────────────▼────────┐
│  Existing Text Path   │                    │  New Layout-Aware Path   │
│  (Preserved)          │                    │  (Added)                 │
└───────────────────────┘                    └──────────────────────────┘
             │                                                   │
    ┌────────┴────────┐                          ┌──────────────┴──────────┐
    │                 │                          │                         │
┌───▼────┐    ┌──────▼──────┐          ┌────────▼────────┐   ┌───────────▼──────┐
│PdfPig  │    │  Tesseract  │          │LayoutToken      │   │  LayoutToken     │
│        │    │  OCR        │          │ Extractor       │   │  Extractor       │
│(Search)│    │  (Scanned)  │          │ (PdfPig)        │   │  (Tesseract)     │
└───┬────┘    └──────┬──────┘          └────────┬────────┘   └───────────┬──────┘
    │                │                          │                         │
    └────────┬───────┘                          └────────────┬────────────┘
             │                                               │
    ┌────────▼────────┐                          ┌──────────▼──────────┐
    │  RawText        │                          │  Layout Token       │
    │  Extraction     │                          │  Collection         │
    └────────┬────────┘                          └──────────┬──────────┘
             │                                               │
    ┌────────▼────────┐                          ┌──────────▼──────────┐
    │  Regex Pattern  │                          │  Page Classifier    │
    │  Matcher        │                          │  (Table/Scattered/  │
    │  (Existing)     │                          │   Sparse)           │
    └────────┬────────┘                          └──────────┬──────────┘
             │                                               │
             │                                    ┌──────────┴──────────┐
             │                                    │                     │
             │                          ┌─────────▼────────┐  ┌────────▼─────────┐
             │                          │  Table Engine    │  │ Proximity Engine │
             │                          │  (Row/Column     │  │ (Spatial         │
             │                          │   Clustering)    │  │  Grouping)       │
             │                          └─────────┬────────┘  └────────┬─────────┘
             │                                    │                     │
             │                                    └──────────┬──────────┘
             │                                               │
             └───────────────────┬───────────────────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │  Structured Records     │
                    │  (Tag, Equipment,       │
                    │   Rating, Description,  │
                    │   Source, Page)         │
                    └────────────┬────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │  Excel Exporter         │
                    │  (Flat format +         │
                    │   Summary sheet)        │
                    └─────────────────────────┘
```



### Component Responsibilities

#### BatchProcessor (Modified)
- Orchestrates file and page processing
- Invokes both text extraction and layout token extraction paths
- Routes pages to appropriate extraction engines based on classification
- Merges results from multiple extraction strategies
- Handles fallback to regex-only extraction on failures
- Maintains existing logging and progress reporting

#### LayoutTokenExtractor (New)
- Extracts spatial metadata from PdfPig text elements
- Extracts spatial metadata from Tesseract TSV output
- Normalizes coordinate systems between PdfPig and Tesseract
- Creates LayoutToken objects with bounding boxes
- Returns empty collections on extraction failures

#### PageClassifier (New)
- Analyzes spatial distribution of layout tokens
- Clusters tokens by Y-coordinate (rows) and X-coordinate (columns)
- Classifies pages as Table, Scattered, or Sparse
- Logs classification decisions with reasoning
- Stores PageType in ExtractionResult

#### TableEngine (New)
- Processes pages classified as Table
- Clusters tokens into rows and columns
- Identifies header row and maps to field names
- Builds structured records by extracting tokens from each column
- Produces one record per data row containing a TAG

#### ProximityEngine (New)
- Processes pages classified as Scattered
- Identifies TAG tokens as anchor points
- Calculates spatial distance from anchors to all other tokens
- Groups tokens within distance threshold (100 pixels)
- Classifies grouped tokens as Equipment or Rating
- Produces one record per anchor with associated elements

#### RegexPatternMatcher (Existing - Preserved)
- Continues to extract tags and equipment from normalized text
- Serves as fallback when layout-aware processing fails
- Provides baseline extraction for all pages

#### ExcelExporter (Modified)
- Exports structured records with Source and Page columns
- Maintains existing flat export format for backward compatibility
- Adds summary sheet with aggregated statistics
- Indicates extraction method used for each record



## Components and Interfaces

### ILayoutTokenExtractor Interface

```csharp
namespace OCRTool.Core.Interfaces
{
    public interface ILayoutTokenExtractor
    {
        /// <summary>
        /// Extract layout tokens from PdfPig page
        /// </summary>
        List<LayoutToken> ExtractFromPdfPigPage(UglyToad.PdfPig.Content.Page page, int pageNumber);
        
        /// <summary>
        /// Extract layout tokens from Tesseract TSV output
        /// </summary>
        List<LayoutToken> ExtractFromTesseractTsv(string tsvOutput, int pageNumber);
        
        /// <summary>
        /// Normalize Tesseract coordinates to PdfPig coordinate system
        /// </summary>
        LayoutToken NormalizeCoordinates(LayoutToken token, double pageWidth, double pageHeight);
    }
}
```

### IPageClassifier Interface

```csharp
namespace OCRTool.Core.Interfaces
{
    public interface IPageClassifier
    {
        /// <summary>
        /// Classify page based on layout token distribution
        /// </summary>
        PageClassification Classify(List<LayoutToken> tokens);
        
        /// <summary>
        /// Detect table-like row and column alignment
        /// </summary>
        bool HasTableStructure(List<LayoutToken> tokens, out int rowCount, out int columnCount);
    }
}
```

### IRecordBuilder Interface

```csharp
namespace OCRTool.Core.Interfaces
{
    public interface IRecordBuilder
    {
        /// <summary>
        /// Build structured records from layout tokens
        /// </summary>
        List<StructuredRecord> BuildRecords(List<LayoutToken> tokens, int pageNumber, string sourceFile);
        
        /// <summary>
        /// Check if this builder can process the given page type
        /// </summary>
        bool CanProcess(PageType pageType);
    }
}
```

### TableEngine Implementation

```csharp
namespace OCRTool.Core.RecordBuilders
{
    public class TableEngine : IRecordBuilder
    {
        private const double RowTolerancePixels = 5.0;
        private const double ColumnTolerancePixels = 10.0;
        
        public bool CanProcess(PageType pageType) => pageType == PageType.Table;
        
        public List<StructuredRecord> BuildRecords(List<LayoutToken> tokens, int pageNumber, string sourceFile)
        {
            // Cluster tokens by Y coordinate (rows)
            var rowClusters = ClusterByYCoordinate(tokens, RowTolerancePixels);
            
            // Cluster tokens by X coordinate (columns)
            var columnClusters = ClusterByXCoordinate(tokens, ColumnTolerancePixels);
            
            // Identify header row (first row)
            var headerRow = rowClusters.OrderBy(r => r.MinY).First();
            var headerMapping = MapHeadersToFields(headerRow, columnClusters);
            
            // Build records from data rows
            var records = new List<StructuredRecord>();
            foreach (var row in rowClusters.Skip(1).OrderBy(r => r.MinY))
            {
                var record = BuildRecordFromRow(row, headerMapping, columnClusters, pageNumber, sourceFile);
                if (record != null && !string.IsNullOrEmpty(record.Tag))
                {
                    records.Add(record);
                }
            }
            
            return records;
        }
        
        private List<RowCluster> ClusterByYCoordinate(List<LayoutToken> tokens, double tolerance);
        private List<ColumnCluster> ClusterByXCoordinate(List<LayoutToken> tokens, double tolerance);
        private Dictionary<int, string> MapHeadersToFields(RowCluster headerRow, List<ColumnCluster> columns);
        private StructuredRecord BuildRecordFromRow(RowCluster row, Dictionary<int, string> headerMapping, 
            List<ColumnCluster> columns, int pageNumber, string sourceFile);
    }
}
```

### ProximityEngine Implementation

```csharp
namespace OCRTool.Core.RecordBuilders
{
    public class ProximityEngine : IRecordBuilder
    {
        private const double DistanceThresholdPixels = 100.0;
        private readonly PatternMatcher _patternMatcher;
        
        public ProximityEngine(PatternMatcher patternMatcher)
        {
            _patternMatcher = patternMatcher;
        }
        
        public bool CanProcess(PageType pageType) => pageType == PageType.Scattered;
        
        public List<StructuredRecord> BuildRecords(List<LayoutToken> tokens, int pageNumber, string sourceFile)
        {
            // Identify TAG tokens as anchors
            var anchorTokens = IdentifyTagTokens(tokens);
            
            var records = new List<StructuredRecord>();
            foreach (var anchor in anchorTokens)
            {
                // Calculate distances to all other tokens
                var nearbyTokens = FindNearbyTokens(anchor, tokens, DistanceThresholdPixels);
                
                // Sort by distance
                nearbyTokens = nearbyTokens.OrderBy(t => CalculateDistance(anchor, t)).ToList();
                
                // Classify tokens as Equipment or Rating
                var equipment = new List<string>();
                var ratings = new List<string>();
                
                foreach (var token in nearbyTokens)
                {
                    if (IsEquipmentToken(token))
                        equipment.Add(token.Text);
                    else if (IsRatingToken(token))
                        ratings.Add(token.Text);
                }
                
                // Build record
                var record = new StructuredRecord
                {
                    Tag = anchor.Text,
                    Equipment = string.Join(" ", equipment),
                    Rating = string.Join(" ", ratings),
                    Source = sourceFile,
                    Page = pageNumber
                };
                
                records.Add(record);
            }
            
            return records;
        }
        
        private List<LayoutToken> IdentifyTagTokens(List<LayoutToken> tokens);
        private List<LayoutToken> FindNearbyTokens(LayoutToken anchor, List<LayoutToken> allTokens, double threshold);
        private double CalculateDistance(LayoutToken token1, LayoutToken token2);
        private bool IsEquipmentToken(LayoutToken token);
        private bool IsRatingToken(LayoutToken token);
    }
}
```



### PageClassifier Implementation

```csharp
namespace OCRTool.Core.Classification
{
    public class PageClassifier : IPageClassifier
    {
        private const int MinTokensForAnalysis = 10;
        private const int MinRowsForTable = 3;
        private const int MinColumnsForTable = 2;
        private const double RowTolerancePixels = 5.0;
        private const double ColumnTolerancePixels = 10.0;
        
        public PageClassification Classify(List<LayoutToken> tokens)
        {
            // Sparse check
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
        
        public bool HasTableStructure(List<LayoutToken> tokens, out int rowCount, out int columnCount)
        {
            // Cluster by Y coordinate
            var rowClusters = ClusterByCoordinate(tokens, t => t.Y, RowTolerancePixels);
            rowCount = rowClusters.Count;
            
            // Cluster by X coordinate
            var columnClusters = ClusterByCoordinate(tokens, t => t.X, ColumnTolerancePixels);
            columnCount = columnClusters.Count;
            
            // Check thresholds
            return rowCount >= MinRowsForTable && columnCount >= MinColumnsForTable;
        }
        
        private List<List<LayoutToken>> ClusterByCoordinate(
            List<LayoutToken> tokens, 
            Func<LayoutToken, double> coordinateSelector, 
            double tolerance)
        {
            var clusters = new List<List<LayoutToken>>();
            var sortedTokens = tokens.OrderBy(coordinateSelector).ToList();
            
            foreach (var token in sortedTokens)
            {
                var coord = coordinateSelector(token);
                var matchingCluster = clusters.FirstOrDefault(c => 
                    Math.Abs(coordinateSelector(c.First()) - coord) <= tolerance);
                
                if (matchingCluster != null)
                {
                    matchingCluster.Add(token);
                }
                else
                {
                    clusters.Add(new List<LayoutToken> { token });
                }
            }
            
            return clusters;
        }
    }
}
```



## Data Models

### LayoutToken

Represents a text element with spatial metadata.

```csharp
namespace OCRTool.Core.Models
{
    public class LayoutToken
    {
        /// <summary>Text content of the token</summary>
        public string Text { get; set; } = string.Empty;
        
        /// <summary>X coordinate of bounding box (left edge)</summary>
        public double X { get; set; }
        
        /// <summary>Y coordinate of bounding box (top edge)</summary>
        public double Y { get; set; }
        
        /// <summary>Width of bounding box</summary>
        public double Width { get; set; }
        
        /// <summary>Height of bounding box</summary>
        public double Height { get; set; }
        
        /// <summary>Confidence score (0-100)</summary>
        public double Confidence { get; set; }
        
        /// <summary>Page number</summary>
        public int PageNumber { get; set; }
        
        /// <summary>Center X coordinate (computed)</summary>
        public double CenterX => X + Width / 2;
        
        /// <summary>Center Y coordinate (computed)</summary>
        public double CenterY => Y + Height / 2;
        
        /// <summary>Bounding box representation</summary>
        public BoundingBox BoundingBox => new BoundingBox(X, Y, Width, Height);
    }
}
```

### BoundingBox

Represents a rectangular region on a page.

```csharp
namespace OCRTool.Core.Models
{
    public class BoundingBox
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        
        public BoundingBox(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
        
        public double Left => X;
        public double Right => X + Width;
        public double Top => Y;
        public double Bottom => Y + Height;
        public double CenterX => X + Width / 2;
        public double CenterY => Y + Height / 2;
        
        /// <summary>Calculate Euclidean distance between centers of two bounding boxes</summary>
        public double DistanceTo(BoundingBox other)
        {
            var dx = CenterX - other.CenterX;
            var dy = CenterY - other.CenterY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
```

### PageType Enumeration

```csharp
namespace OCRTool.Core.Models
{
    public enum PageType
    {
        /// <summary>Page has regular row and column alignment (equipment schedules)</summary>
        Table,
        
        /// <summary>Page has irregular token distribution (P&ID drawings)</summary>
        Scattered,
        
        /// <summary>Page has fewer than 10 tokens (cover pages, blank pages)</summary>
        Sparse
    }
}
```

### PageClassification

```csharp
namespace OCRTool.Core.Models
{
    public class PageClassification
    {
        public PageType PageType { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
    }
}
```

### StructuredRecord

Represents an extracted record with source tracking.

```csharp
namespace OCRTool.Core.Models
{
    public class StructuredRecord
    {
        public string Tag { get; set; } = string.Empty;
        public string Equipment { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int Page { get; set; }
        public ExtractionMethod Method { get; set; }
    }
    
    public enum ExtractionMethod
    {
        TableEngine,
        ProximityEngine,
        RegexSystem
    }
}
```

### ExtractionResult (Extended)

The existing ExtractionResult model is extended with layout-aware fields.

```csharp
namespace OCRTool.Core.Models
{
    public class ExtractionResult
    {
        // Existing fields (preserved)
        public string SourceFile { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public bool IsSearchable { get; set; }
        public List<TagItem> Tags { get; set; } = new List<TagItem>();
        public List<EquipmentItem> Equipment { get; set; } = new List<EquipmentItem>();
        public double Confidence { get; set; }
        public string RawText { get; set; } = string.Empty;
        
        // New fields (added)
        public List<LayoutToken> LayoutTokens { get; set; } = new List<LayoutToken>();
        public PageClassification? Classification { get; set; }
        public List<StructuredRecord> StructuredRecords { get; set; } = new List<StructuredRecord>();
    }
}
```

### RowCluster and ColumnCluster

Helper models for table processing.

```csharp
namespace OCRTool.Core.Models
{
    public class RowCluster
    {
        public List<LayoutToken> Tokens { get; set; } = new List<LayoutToken>();
        public double MinY => Tokens.Min(t => t.Y);
        public double MaxY => Tokens.Max(t => t.Y);
        public double AvgY => Tokens.Average(t => t.Y);
    }
    
    public class ColumnCluster
    {
        public List<LayoutToken> Tokens { get; set; } = new List<LayoutToken>();
        public double MinX => Tokens.Min(t => t.X);
        public double MaxX => Tokens.Max(t => t.X);
        public double AvgX => Tokens.Average(t => t.X);
        public int ColumnIndex { get; set; }
    }
}
```



## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property Reflection

After analyzing all acceptance criteria, several properties can be consolidated to eliminate redundancy:

- Properties 1.1 and 1.2 both validate PdfPig extraction - combined into comprehensive property about PdfPig token structure
- Properties 2.1 and 2.2 both validate Tesseract extraction - combined into comprehensive property about Tesseract token structure
- Properties 5.1 and 5.2 both validate classification behavior - combined into property about classification output domain
- Properties 7.1 and 7.2 both validate table engine clustering - combined into property about table clustering
- Properties 11.1, 11.2, 11.3, 11.4, 11.5 all validate logging - combined into property about logging completeness
- Properties 15.2 and 16.3 both validate that records have TAG fields - kept separate as they apply to different engines

The following properties provide unique validation value and will be included:



### Property 1: PdfPig Layout Token Structure

*For any* searchable PDF page processed by PdfPig, each text element SHALL produce a LayoutToken with text content, X coordinate, Y coordinate, Width, Height, confidence score of 100, and page number, all stored in a collection associated with that page.

**Validates: Requirements 1.1, 1.2, 1.3**

### Property 2: PdfPig Coordinate Preservation

*For any* text element extracted by PdfPig, the LayoutToken coordinates SHALL match the original PdfPig coordinate system without transformation.

**Validates: Requirements 1.5**

### Property 3: Tesseract Layout Token Structure

*For any* word in Tesseract TSV output, the Layout_Token_Extractor SHALL create a LayoutToken with text content, X coordinate, Y coordinate, Width, Height, word-level confidence score (0-100), and page number.

**Validates: Requirements 2.1, 2.2, 2.3**

### Property 4: Tesseract Coordinate Normalization

*For any* LayoutToken extracted from Tesseract, after normalization the coordinates SHALL be in the PdfPig coordinate system.

**Validates: Requirements 2.5**

### Property 5: Backward Compatible Text Extraction

*For any* page processed, the RawText field SHALL contain the same text content as before the layout-aware transformation, and the Regex_System SHALL receive the same normalized text input.

**Validates: Requirements 3.1, 3.2, 3.5**

### Property 6: Graceful Degradation on Failure

*For any* page where layout token extraction fails, the Batch_Processor SHALL complete text-only extraction without errors and produce valid results.

**Validates: Requirements 3.3**

### Property 7: Output Format Backward Compatibility

*For any* document processed with layout-aware features disabled, the Excel output format SHALL be identical to the pre-transformation format.

**Validates: Requirements 3.4, 13.2**

### Property 8: ExtractionResult Model Extension

*For any* ExtractionResult object, it SHALL contain both the original fields (SourceFile, PageNumber, IsSearchable, Tags, Equipment, Confidence, RawText) and new fields (LayoutTokens, Classification, StructuredRecords) without modification to original fields.

**Validates: Requirements 4.1, 4.2, 4.5, 13.3**

### Property 9: Non-Null Layout Token Collections

*For any* ExtractionResult, the LayoutTokens collection SHALL never be null, even when no tokens are extracted (empty collection instead).

**Validates: Requirements 4.4**

### Property 10: Classification Output Domain

*For any* page with LayoutTokens, the Page_Classifier SHALL assign exactly one PageType from the set {Table, Scattered, Sparse} and store it in the ExtractionResult.

**Validates: Requirements 5.1, 5.2, 5.6**

### Property 11: Sparse Page Classification

*For any* page with fewer than 10 LayoutTokens, the Page_Classifier SHALL classify it as Sparse.

**Validates: Requirements 5.5**

### Property 12: Table Page Classification

*For any* page where LayoutTokens form at least 3 distinct row clusters (Y-coordinate tolerance 5 pixels) and 2 distinct column clusters (X-coordinate tolerance 10 pixels), the Page_Classifier SHALL classify it as Table.

**Validates: Requirements 5.3, 6.1, 6.2, 6.3, 6.4, 6.5**

### Property 13: Scattered Page Classification

*For any* page with 10 or more LayoutTokens that does not meet table structure criteria, the Page_Classifier SHALL classify it as Scattered.

**Validates: Requirements 5.4**

### Property 14: Table Engine Row Clustering

*For any* page classified as Table, the Table_Engine SHALL cluster LayoutTokens into RowClusters where all tokens in a cluster have Y-coordinate variance less than 10 pixels.

**Validates: Requirements 7.1, 15.5**

### Property 15: Table Engine Record Count Bound

*For any* page with N rows processed by the Table_Engine, it SHALL produce at most N-1 StructuredRecords (excluding the header row).

**Validates: Requirements 15.1**

### Property 16: Table Engine Record Validity

*For any* StructuredRecord produced by the Table_Engine, it SHALL contain at least a non-empty TAG field.

**Validates: Requirements 7.7, 15.2**

### Property 17: Table Engine Field Count Bound

*For any* table row with K columns, the corresponding StructuredRecord SHALL contain at most K populated fields.

**Validates: Requirements 15.3**

### Property 18: Table Engine Row Order Preservation

*For any* page processed by the Table_Engine, the StructuredRecords SHALL appear in the same top-to-bottom order as the rows on the page.

**Validates: Requirements 15.4**

### Property 19: Proximity Engine Anchor Identification

*For any* LayoutToken identified as an Anchor_Token by the Proximity_Engine, its text SHALL match the TAG pattern from the Regex_System.

**Validates: Requirements 8.1, 16.1**

### Property 20: Proximity Engine Distance Threshold

*For any* Anchor_Token, all LayoutTokens grouped with it SHALL have a spatial distance (Euclidean distance between bounding box centers) of at most 100 pixels.

**Validates: Requirements 8.2, 8.3, 16.2, 16.4**

### Property 21: Proximity Engine Distance Sorting

*For any* Anchor_Token with multiple grouped tokens, the tokens SHALL be sorted by spatial distance before concatenation into Equipment or Rating fields.

**Validates: Requirements 8.7**

### Property 22: Proximity Engine Record Validity

*For any* StructuredRecord produced by the Proximity_Engine, the TAG field SHALL be populated with the Anchor_Token text.

**Validates: Requirements 8.5, 16.3**

### Property 23: Proximity Engine Record Count

*For any* page processed by the Proximity_Engine, it SHALL produce at most one StructuredRecord per Anchor_Token.

**Validates: Requirements 16.5**

### Property 24: Hybrid Orchestration Routing

*For any* page, the Batch_Processor SHALL invoke Table_Engine if classified as Table, Proximity_Engine if classified as Scattered, and skip structured extraction if classified as Sparse.

**Validates: Requirements 9.1, 9.2, 9.3**

### Property 25: Fallback to Regex System

*For any* page where structured record building produces zero records, the Batch_Processor SHALL invoke the Regex_System for extraction.

**Validates: Requirements 9.4**

### Property 26: Result Merging

*For any* page, the final ExtractionResult SHALL contain records from both structured extraction (if any) and regex-based extraction, merged together.

**Validates: Requirements 9.5**

### Property 27: Source and Page Tracking

*For any* StructuredRecord, the Source field SHALL contain the source file name and the Page field SHALL contain the page number.

**Validates: Requirements 10.2, 10.3**

### Property 28: Extraction Method Tracking

*For any* StructuredRecord, it SHALL have an ExtractionMethod indicator showing whether it came from Table_Engine, Proximity_Engine, or Regex_System.

**Validates: Requirements 12.5**

### Property 29: Independent Page Classification

*For any* two pages in a document, the classification of one page SHALL not affect the classification of the other page.

**Validates: Requirements 12.1**

### Property 30: Mixed Document Processing

*For any* document containing pages of different types, the Batch_Processor SHALL apply the appropriate extraction engine to each page based on its individual classification.

**Validates: Requirements 12.2**

### Property 31: Layout Token Serialization Round Trip

*For any* valid LayoutToken collection, serializing to JSON then deserializing SHALL produce an equivalent collection with the same tokens, coordinates, and metadata.

**Validates: Requirements 14.1, 14.2, 14.4**

### Property 32: Pretty Printer Output Completeness

*For any* LayoutToken, the pretty-printed output SHALL include the bounding box coordinates (X, Y, Width, Height) and text content.

**Validates: Requirements 14.5**

### Property 33: Logging Completeness

*For any* page processed, the system SHALL log: page processing status (searchable vs scanned), layout token extraction statistics, classification decision with reasoning, and which extraction engine was used.

**Validates: Requirements 5.7, 9.6, 11.1, 11.2, 11.3, 11.4, 11.5**

### Property 34: Error Logging and Fallback

*For any* page where layout-aware processing fails, the Batch_Processor SHALL log the error and successfully complete text-only extraction.

**Validates: Requirements 11.6**



## Error Handling

### Error Handling Strategy

The layout-aware extraction system implements a defense-in-depth error handling strategy with graceful degradation at multiple levels:

#### Level 1: Layout Token Extraction Failures

**Failure Scenarios:**
- PdfPig fails to extract bounding boxes from malformed PDFs
- Tesseract TSV output is corrupted or missing
- Coordinate normalization encounters invalid values

**Handling:**
- Return empty LayoutToken collection (never null)
- Log extraction failure with details
- Continue with text-only extraction path
- Populate RawText field normally
- Set Classification to null

**Code Pattern:**
```csharp
try
{
    var tokens = ExtractLayoutTokens(page);
    result.LayoutTokens = tokens;
}
catch (Exception ex)
{
    _logger.LogError($"Layout token extraction failed: {ex.Message}");
    result.LayoutTokens = new List<LayoutToken>(); // Empty, not null
    // Continue processing with text-only path
}
```

#### Level 2: Page Classification Failures

**Failure Scenarios:**
- Insufficient tokens for meaningful classification
- Clustering algorithm encounters edge cases
- Invalid token coordinates (NaN, Infinity)

**Handling:**
- Default to Sparse classification for safety
- Log classification failure with reasoning
- Skip structured record building
- Fall back to Regex_System extraction

**Code Pattern:**
```csharp
try
{
    var classification = _classifier.Classify(tokens);
    result.Classification = classification;
}
catch (Exception ex)
{
    _logger.LogWarning($"Classification failed, defaulting to Sparse: {ex.Message}");
    result.Classification = new PageClassification 
    { 
        PageType = PageType.Sparse,
        Reasoning = $"Classification error: {ex.Message}"
    };
}
```

#### Level 3: Record Builder Failures

**Failure Scenarios:**
- Table_Engine cannot identify header row
- Proximity_Engine finds no TAG anchors
- Clustering produces empty or invalid results
- Pattern matching fails on grouped tokens

**Handling:**
- Return empty StructuredRecords collection
- Log record building failure with details
- Trigger fallback to Regex_System
- Ensure at least regex-based extraction succeeds

**Code Pattern:**
```csharp
try
{
    var records = _recordBuilder.BuildRecords(tokens, pageNumber, sourceFile);
    result.StructuredRecords = records;
    
    if (records.Count == 0)
    {
        _logger.LogInfo("Structured extraction produced no records, falling back to regex");
        ApplyRegexExtraction(result);
    }
}
catch (Exception ex)
{
    _logger.LogError($"Record building failed: {ex.Message}");
    result.StructuredRecords = new List<StructuredRecord>();
    ApplyRegexExtraction(result); // Always fall back
}
```

#### Level 4: Export Failures

**Failure Scenarios:**
- Excel file cannot be created (permissions, disk space)
- Invalid characters in structured records
- Summary sheet generation fails

**Handling:**
- Attempt to export without summary sheet
- Log export errors with full details
- Provide user-friendly error message
- Preserve processing logs for debugging

### Error Recovery Guarantees

1. **No Silent Failures**: All errors are logged with context
2. **Graceful Degradation**: System always produces some output (at minimum, regex-based extraction)
3. **Data Preservation**: RawText and existing extraction paths always work
4. **User Notification**: UI shows clear error messages with actionable guidance
5. **Debugging Support**: Logs include enough detail to reproduce and diagnose issues

### Validation and Defensive Programming

#### Input Validation

```csharp
// Validate coordinates
if (double.IsNaN(token.X) || double.IsInfinity(token.X) ||
    double.IsNaN(token.Y) || double.IsInfinity(token.Y))
{
    _logger.LogWarning($"Invalid coordinates in token: {token.Text}");
    continue; // Skip invalid token
}

// Validate confidence scores
if (confidence < 0 || confidence > 100)
{
    _logger.LogWarning($"Confidence {confidence} out of range, clamping to [0,100]");
    confidence = Math.Max(0, Math.Min(100, confidence));
}

// Validate page numbers
if (pageNumber < 1)
{
    throw new ArgumentException($"Invalid page number: {pageNumber}");
}
```

#### Null Safety

```csharp
// Always initialize collections
public class ExtractionResult
{
    public List<LayoutToken> LayoutTokens { get; set; } = new List<LayoutToken>();
    public List<StructuredRecord> StructuredRecords { get; set; } = new List<StructuredRecord>();
}

// Null-coalescing for optional fields
var text = token?.Text ?? string.Empty;
var tokens = result?.LayoutTokens ?? new List<LayoutToken>();
```

#### Boundary Conditions

```csharp
// Handle empty collections
if (tokens == null || tokens.Count == 0)
{
    return new PageClassification 
    { 
        PageType = PageType.Sparse,
        Reasoning = "No tokens available for classification"
    };
}

// Handle single-element collections
if (rowClusters.Count < 2)
{
    _logger.LogInfo("Insufficient rows for table extraction");
    return new List<StructuredRecord>();
}
```



## Testing Strategy

### Dual Testing Approach

The layout-aware extraction system requires both unit testing and property-based testing for comprehensive coverage:

- **Unit tests**: Verify specific examples, edge cases, and error conditions
- **Property tests**: Verify universal properties across all inputs
- Both approaches are complementary and necessary

### Property-Based Testing

#### Framework Selection

For C#, we will use **FsCheck** (https://fscheck.github.io/FsCheck/), a mature property-based testing library that integrates with xUnit, NUnit, and MSTest.

```xml
<PackageReference Include="FsCheck" Version="2.16.5" />
<PackageReference Include="FsCheck.Xunit" Version="2.16.5" />
```

#### Configuration

Each property test will run a minimum of 100 iterations to ensure comprehensive input coverage:

```csharp
[Property(MaxTest = 100)]
public Property PropertyName(/* parameters */)
{
    // Test implementation
}
```

#### Property Test Tagging

Each property test must reference its design document property using a comment tag:

```csharp
// Feature: layout-aware-extraction, Property 1: PdfPig Layout Token Structure
[Property(MaxTest = 100)]
public Property PdfPigLayoutTokenStructure(/* parameters */)
{
    // Test implementation
}
```

#### Custom Generators

Property tests require custom generators for domain-specific types:

```csharp
public static class Generators
{
    // Generate valid LayoutTokens
    public static Arbitrary<LayoutToken> LayoutTokenGenerator()
    {
        return Arb.From(
            from text in Arb.Generate<NonEmptyString>()
            from x in Gen.Choose(0, 1000).Select(i => (double)i)
            from y in Gen.Choose(0, 1000).Select(i => (double)i)
            from width in Gen.Choose(10, 200).Select(i => (double)i)
            from height in Gen.Choose(10, 50).Select(i => (double)i)
            from confidence in Gen.Choose(0, 100).Select(i => (double)i)
            from page in Gen.Choose(1, 100)
            select new LayoutToken
            {
                Text = text.Get,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Confidence = confidence,
                PageNumber = page
            });
    }
    
    // Generate table-like token layouts
    public static Arbitrary<List<LayoutToken>> TableLayoutGenerator()
    {
        return Arb.From(
            from rows in Gen.Choose(3, 10)
            from cols in Gen.Choose(2, 6)
            select GenerateTableLayout(rows, cols));
    }
    
    // Generate scattered token layouts
    public static Arbitrary<List<LayoutToken>> ScatteredLayoutGenerator()
    {
        return Arb.From(
            from count in Gen.Choose(10, 50)
            select GenerateScatteredLayout(count));
    }
}
```

#### Example Property Tests

**Property 1: PdfPig Layout Token Structure**

```csharp
// Feature: layout-aware-extraction, Property 1: PdfPig Layout Token Structure
[Property(MaxTest = 100)]
public Property PdfPigLayoutTokenStructure()
{
    return Prop.ForAll(
        Generators.PdfPigPageGenerator(),
        page =>
        {
            var extractor = new LayoutTokenExtractor();
            var tokens = extractor.ExtractFromPdfPigPage(page, 1);
            
            // All text elements should have corresponding tokens
            var textElements = page.GetWords().ToList();
            return tokens.Count == textElements.Count &&
                   tokens.All(t => 
                       !string.IsNullOrEmpty(t.Text) &&
                       t.X >= 0 && t.Y >= 0 &&
                       t.Width > 0 && t.Height > 0 &&
                       t.Confidence == 100 &&
                       t.PageNumber == 1);
        });
}
```

**Property 11: Sparse Page Classification**

```csharp
// Feature: layout-aware-extraction, Property 11: Sparse Page Classification
[Property(MaxTest = 100)]
public Property SparsePageClassification()
{
    return Prop.ForAll(
        Gen.Choose(0, 9).SelectMany(count => 
            Gen.ListOf(count, Generators.LayoutTokenGenerator().Generator)),
        tokens =>
        {
            var classifier = new PageClassifier();
            var classification = classifier.Classify(tokens);
            
            return classification.PageType == PageType.Sparse;
        });
}
```

**Property 31: Layout Token Serialization Round Trip**

```csharp
// Feature: layout-aware-extraction, Property 31: Layout Token Serialization Round Trip
[Property(MaxTest = 100)]
public Property LayoutTokenSerializationRoundTrip()
{
    return Prop.ForAll(
        Arb.Generate<List<LayoutToken>>(),
        tokens =>
        {
            var serializer = new LayoutTokenSerializer();
            var json = serializer.Serialize(tokens);
            var deserialized = serializer.Deserialize(json);
            
            return tokens.Count == deserialized.Count &&
                   tokens.Zip(deserialized, (a, b) => 
                       a.Text == b.Text &&
                       Math.Abs(a.X - b.X) < 0.001 &&
                       Math.Abs(a.Y - b.Y) < 0.001 &&
                       Math.Abs(a.Width - b.Width) < 0.001 &&
                       Math.Abs(a.Height - b.Height) < 0.001 &&
                       Math.Abs(a.Confidence - b.Confidence) < 0.001 &&
                       a.PageNumber == b.PageNumber
                   ).All(x => x);
        });
}
```

### Unit Testing

Unit tests focus on specific examples, edge cases, and integration points:

#### Example Unit Tests

**Layout Token Extraction**

```csharp
[Fact]
public void ExtractFromPdfPig_EmptyPage_ReturnsEmptyCollection()
{
    // Arrange
    var extractor = new LayoutTokenExtractor();
    var emptyPage = CreateEmptyPdfPigPage();
    
    // Act
    var tokens = extractor.ExtractFromPdfPigPage(emptyPage, 1);
    
    // Assert
    Assert.NotNull(tokens);
    Assert.Empty(tokens);
}

[Fact]
public void ExtractFromTesseract_InvalidTsv_ReturnsEmptyCollection()
{
    // Arrange
    var extractor = new LayoutTokenExtractor();
    var invalidTsv = "corrupted\tdata\there";
    
    // Act
    var tokens = extractor.ExtractFromTesseractTsv(invalidTsv, 1);
    
    // Assert
    Assert.NotNull(tokens);
    Assert.Empty(tokens);
}
```

**Page Classification**

```csharp
[Fact]
public void Classify_EquipmentSchedule_ReturnsTable()
{
    // Arrange
    var classifier = new PageClassifier();
    var tokens = CreateEquipmentScheduleLayout(); // 5 rows, 4 columns
    
    // Act
    var classification = classifier.Classify(tokens);
    
    // Assert
    Assert.Equal(PageType.Table, classification.PageType);
    Assert.Equal(5, classification.RowCount);
    Assert.Equal(4, classification.ColumnCount);
}

[Fact]
public void Classify_PIDDrawing_ReturnsScattered()
{
    // Arrange
    var classifier = new PageClassifier();
    var tokens = CreatePIDDrawingLayout(); // Irregular distribution
    
    // Act
    var classification = classifier.Classify(tokens);
    
    // Assert
    Assert.Equal(PageType.Scattered, classification.PageType);
}
```

**Table Engine**

```csharp
[Fact]
public void BuildRecords_ThreeRowTable_ReturnsTwoRecords()
{
    // Arrange
    var engine = new TableEngine();
    var tokens = CreateTableLayout(
        headers: new[] { "Tag", "Equipment", "Rating" },
        rows: new[]
        {
            new[] { "TAG-001", "Motor", "5HP" },
            new[] { "TAG-002", "Pump", "10HP" }
        });
    
    // Act
    var records = engine.BuildRecords(tokens, 1, "test.pdf");
    
    // Assert
    Assert.Equal(2, records.Count);
    Assert.Equal("TAG-001", records[0].Tag);
    Assert.Equal("Motor", records[0].Equipment);
    Assert.Equal("5HP", records[0].Rating);
}

[Fact]
public void BuildRecords_RowWithoutTag_SkipsRow()
{
    // Arrange
    var engine = new TableEngine();
    var tokens = CreateTableLayout(
        headers: new[] { "Tag", "Equipment" },
        rows: new[]
        {
            new[] { "TAG-001", "Motor" },
            new[] { "", "Pump" }, // No tag
            new[] { "TAG-003", "Valve" }
        });
    
    // Act
    var records = engine.BuildRecords(tokens, 1, "test.pdf");
    
    // Assert
    Assert.Equal(2, records.Count); // Row without tag is skipped
    Assert.DoesNotContain(records, r => r.Equipment == "Pump");
}
```

**Proximity Engine**

```csharp
[Fact]
public void BuildRecords_TagWithNearbyEquipment_GroupsCorrectly()
{
    // Arrange
    var patternMatcher = new PatternMatcher(config);
    var engine = new ProximityEngine(patternMatcher);
    var tokens = new List<LayoutToken>
    {
        new LayoutToken { Text = "TAG-001", X = 100, Y = 100, Width = 50, Height = 20 },
        new LayoutToken { Text = "Motor", X = 120, Y = 130, Width = 40, Height = 20 }, // 30 pixels away
        new LayoutToken { Text = "5HP", X = 110, Y = 160, Width = 30, Height = 20 }, // 60 pixels away
        new LayoutToken { Text = "FarAway", X = 500, Y = 500, Width = 50, Height = 20 } // 565 pixels away
    };
    
    // Act
    var records = engine.BuildRecords(tokens, 1, "test.pdf");
    
    // Assert
    Assert.Single(records);
    Assert.Equal("TAG-001", records[0].Tag);
    Assert.Contains("Motor", records[0].Equipment);
    Assert.Contains("5HP", records[0].Rating);
    Assert.DoesNotContain("FarAway", records[0].Equipment);
}
```

**Backward Compatibility**

```csharp
[Fact]
public void ProcessPage_WithLayoutDisabled_ProducesSameOutput()
{
    // Arrange
    var processor = CreateBatchProcessor(layoutAwareEnabled: false);
    var page = CreateTestPage();
    
    // Act
    var result = processor.ProcessPageAsync("test.pdf", page).Result;
    
    // Assert
    Assert.NotNull(result.RawText);
    Assert.NotEmpty(result.Tags);
    Assert.Empty(result.LayoutTokens); // Layout features disabled
    Assert.Null(result.Classification);
}
```

### Integration Testing

Integration tests verify end-to-end workflows across multiple components:

```csharp
[Fact]
public async Task ProcessBatch_MixedDocument_AppliesCorrectEngines()
{
    // Arrange
    var processor = CreateBatchProcessor();
    var pdfPath = CreateMixedDocumentPdf(
        tablePage: 1,
        scatteredPage: 2,
        sparsePage: 3);
    
    // Act
    var results = await processor.ProcessSingleFileAsync(pdfPath);
    
    // Assert
    Assert.Equal(3, results.Count);
    
    // Page 1: Table
    Assert.Equal(PageType.Table, results[0].Classification.PageType);
    Assert.NotEmpty(results[0].StructuredRecords);
    Assert.All(results[0].StructuredRecords, r => 
        Assert.Equal(ExtractionMethod.TableEngine, r.Method));
    
    // Page 2: Scattered
    Assert.Equal(PageType.Scattered, results[1].Classification.PageType);
    Assert.NotEmpty(results[1].StructuredRecords);
    Assert.All(results[1].StructuredRecords, r => 
        Assert.Equal(ExtractionMethod.ProximityEngine, r.Method));
    
    // Page 3: Sparse
    Assert.Equal(PageType.Sparse, results[2].Classification.PageType);
    Assert.Empty(results[2].StructuredRecords);
    Assert.NotEmpty(results[2].Tags); // Regex extraction still works
}
```

### Test Organization

```
OCRTool.Tests/
├── Unit/
│   ├── LayoutTokenExtractorTests.cs
│   ├── PageClassifierTests.cs
│   ├── TableEngineTests.cs
│   ├── ProximityEngineTests.cs
│   └── ExcelExporterTests.cs
├── Properties/
│   ├── LayoutTokenProperties.cs
│   ├── ClassificationProperties.cs
│   ├── TableEngineProperties.cs
│   ├── ProximityEngineProperties.cs
│   └── SerializationProperties.cs
├── Integration/
│   ├── EndToEndTests.cs
│   ├── BackwardCompatibilityTests.cs
│   └── ErrorHandlingTests.cs
└── Helpers/
    ├── TestDataGenerators.cs
    ├── FsCheckGenerators.cs
    └── MockFactories.cs
```

### Test Coverage Goals

- **Unit Tests**: 80%+ code coverage for all new components
- **Property Tests**: 100% coverage of all 34 correctness properties
- **Integration Tests**: All 5 priority phases have end-to-end tests
- **Edge Cases**: All edge cases identified in prework have explicit tests

### Continuous Testing

- Run unit tests on every commit
- Run property tests (100 iterations) on every pull request
- Run integration tests before merging to main branch
- Run extended property tests (1000 iterations) nightly



## Implementation Algorithms

### Layout Token Extraction from PdfPig

```csharp
public List<LayoutToken> ExtractFromPdfPigPage(Page page, int pageNumber)
{
    var tokens = new List<LayoutToken>();
    
    try
    {
        foreach (var word in page.GetWords())
        {
            var token = new LayoutToken
            {
                Text = word.Text,
                X = word.BoundingBox.Left,
                Y = word.BoundingBox.Bottom, // PdfPig uses bottom-left origin
                Width = word.BoundingBox.Width,
                Height = word.BoundingBox.Height,
                Confidence = 100.0, // Searchable text assumed high confidence
                PageNumber = pageNumber
            };
            
            tokens.Add(token);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"PdfPig extraction failed: {ex.Message}");
        return new List<LayoutToken>();
    }
    
    return tokens;
}
```

### Layout Token Extraction from Tesseract TSV

Tesseract TSV format:
```
level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
1	1	0	0	0	0	0	0	1000	1000	-1	
2	1	1	0	0	0	100	100	200	50	-1	
3	1	1	1	0	0	100	100	200	50	-1	
4	1	1	1	1	0	100	100	200	50	-1	
5	1	1	1	1	1	100	100	50	20	95	TAG-001
5	1	1	1	1	2	160	100	40	20	87	Motor
```

```csharp
public List<LayoutToken> ExtractFromTesseractTsv(string tsvOutput, int pageNumber)
{
    var tokens = new List<LayoutToken>();
    
    try
    {
        var lines = tsvOutput.Split('\n');
        
        // Skip header line
        for (int i = 1; i < lines.Length; i++)
        {
            var fields = lines[i].Split('\t');
            
            // Only process word-level entries (level 5)
            if (fields.Length < 12 || fields[0] != "5")
                continue;
            
            var text = fields[11].Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;
            
            var token = new LayoutToken
            {
                Text = text,
                X = double.Parse(fields[6]),
                Y = double.Parse(fields[7]),
                Width = double.Parse(fields[8]),
                Height = double.Parse(fields[9]),
                Confidence = double.Parse(fields[10]),
                PageNumber = pageNumber
            };
            
            // Normalize coordinates to PdfPig system
            token = NormalizeCoordinates(token, pageWidth, pageHeight);
            
            tokens.Add(token);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Tesseract TSV parsing failed: {ex.Message}");
        return new List<LayoutToken>();
    }
    
    return tokens;
}

private LayoutToken NormalizeCoordinates(LayoutToken token, double pageWidth, double pageHeight)
{
    // Tesseract uses top-left origin, PdfPig uses bottom-left origin
    // Convert Y coordinate: Y_pdfpig = pageHeight - Y_tesseract - Height
    token.Y = pageHeight - token.Y - token.Height;
    return token;
}
```

### Clustering Algorithm

Used by both PageClassifier and TableEngine for row/column detection:

```csharp
private List<List<LayoutToken>> ClusterByCoordinate(
    List<LayoutToken> tokens,
    Func<LayoutToken, double> coordinateSelector,
    double tolerance)
{
    if (tokens == null || tokens.Count == 0)
        return new List<List<LayoutToken>>();
    
    var clusters = new List<List<LayoutToken>>();
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
            matchingCluster.Add(token);
        }
        else
        {
            clusters.Add(new List<LayoutToken> { token });
        }
    }
    
    return clusters;
}
```

### Table Header Mapping Algorithm

```csharp
private Dictionary<int, string> MapHeadersToFields(
    RowCluster headerRow,
    List<ColumnCluster> columnClusters)
{
    var mapping = new Dictionary<int, string>();
    
    foreach (var column in columnClusters)
    {
        // Find header token in this column
        var headerToken = headerRow.Tokens
            .Where(t => Math.Abs(t.X - column.AvgX) <= ColumnTolerancePixels)
            .FirstOrDefault();
        
        if (headerToken == null)
            continue;
        
        // Map header text to field name
        var headerText = headerToken.Text.ToLower();
        string fieldName = null;
        
        if (headerText.Contains("tag") || headerText.Contains("id"))
            fieldName = "Tag";
        else if (headerText.Contains("equipment") || headerText.Contains("description"))
            fieldName = "Equipment";
        else if (headerText.Contains("rating") || headerText.Contains("capacity"))
            fieldName = "Rating";
        else if (headerText.Contains("desc"))
            fieldName = "Description";
        
        if (fieldName != null)
            mapping[column.ColumnIndex] = fieldName;
    }
    
    return mapping;
}
```

### Spatial Distance Calculation

```csharp
private double CalculateDistance(LayoutToken token1, LayoutToken token2)
{
    var dx = token1.CenterX - token2.CenterX;
    var dy = token1.CenterY - token2.CenterY;
    return Math.Sqrt(dx * dx + dy * dy);
}
```

### TAG Pattern Identification

```csharp
private List<LayoutToken> IdentifyTagTokens(List<LayoutToken> tokens)
{
    var tagTokens = new List<LayoutToken>();
    
    foreach (var token in tokens)
    {
        // Use existing regex patterns from PatternMatcher
        if (_patternMatcher.IsTagPattern(token.Text))
        {
            tagTokens.Add(token);
        }
    }
    
    return tagTokens;
}
```

### Proximity Grouping Algorithm

```csharp
private List<LayoutToken> FindNearbyTokens(
    LayoutToken anchor,
    List<LayoutToken> allTokens,
    double threshold)
{
    var nearbyTokens = new List<LayoutToken>();
    
    foreach (var token in allTokens)
    {
        // Skip the anchor itself
        if (token == anchor)
            continue;
        
        var distance = CalculateDistance(anchor, token);
        
        if (distance <= threshold)
        {
            nearbyTokens.Add(token);
        }
    }
    
    // Sort by distance
    return nearbyTokens.OrderBy(t => CalculateDistance(anchor, t)).ToList();
}
```

## Implementation Phases

### Priority 1: Layout Token Extraction (Foundation)

**Objective**: Extract and store spatial metadata without changing existing behavior.

**Components to Implement:**
1. `LayoutToken` model
2. `BoundingBox` model
3. `ILayoutTokenExtractor` interface
4. `LayoutTokenExtractor` implementation
5. Extend `ExtractionResult` with `LayoutTokens` field
6. Modify `BatchProcessor.ProcessPageAsync()` to call extractor

**Integration Points:**
- Hook into existing PdfPig extraction in `PdfPigPDFProcessor`
- Hook into existing Tesseract extraction in `TesseractOCRProvider`
- Modify Tesseract to output TSV format (add `--tsv` flag)

**Testing:**
- Unit tests for PdfPig extraction
- Unit tests for Tesseract TSV parsing
- Unit tests for coordinate normalization
- Property tests for token structure validity
- Integration test: verify RawText unchanged

**Acceptance Criteria:**
- All pages produce LayoutToken collections
- Existing text extraction unchanged
- No performance degradation (< 5% overhead)

### Priority 2: Page Classification

**Objective**: Classify pages by layout type to enable intelligent routing.

**Components to Implement:**
1. `PageType` enumeration
2. `PageClassification` model
3. `IPageClassifier` interface
4. `PageClassifier` implementation
5. Extend `ExtractionResult` with `Classification` field
6. Modify `BatchProcessor` to invoke classifier

**Algorithms:**
- Clustering by Y-coordinate (rows)
- Clustering by X-coordinate (columns)
- Table structure detection
- Sparse page detection

**Testing:**
- Unit tests for each page type
- Unit tests for clustering algorithm
- Property tests for classification output domain
- Property tests for threshold behavior
- Integration test: mixed document classification

**Acceptance Criteria:**
- All pages assigned a PageType
- Classification logged with reasoning
- Table detection works on equipment schedules
- Scattered detection works on P&ID drawings

### Priority 3: Structured Record Builders

**Objective**: Build structured records from classified pages.

**Components to Implement:**
1. `StructuredRecord` model
2. `ExtractionMethod` enumeration
3. `IRecordBuilder` interface
4. `TableEngine` implementation
5. `ProximityEngine` implementation
6. `RowCluster` and `ColumnCluster` models

**Algorithms:**
- Table row/column clustering
- Header identification and mapping
- Proximity-based grouping
- TAG anchor identification
- Token classification (Equipment vs Rating)

**Testing:**
- Unit tests for TableEngine with various table layouts
- Unit tests for ProximityEngine with various scattered layouts
- Property tests for record count bounds
- Property tests for field validity
- Property tests for distance thresholds
- Integration test: end-to-end record building

**Acceptance Criteria:**
- TableEngine extracts records from equipment schedules
- ProximityEngine extracts records from P&ID drawings
- All records have source tracking (file, page)
- Record building failures don't crash system

### Priority 4: Hybrid Orchestration

**Objective**: Route pages to appropriate engines and merge results.

**Components to Implement:**
1. Routing logic in `BatchProcessor`
2. Fallback to Regex_System
3. Result merging logic
4. Engine selection logging

**Integration Points:**
- Invoke TableEngine for Table pages
- Invoke ProximityEngine for Scattered pages
- Skip structured extraction for Sparse pages
- Always run Regex_System as baseline
- Merge structured and regex results

**Testing:**
- Unit tests for routing logic
- Unit tests for fallback behavior
- Property tests for result merging
- Integration test: mixed document processing
- Integration test: fallback scenarios

**Acceptance Criteria:**
- Correct engine invoked for each page type
- Fallback works when structured extraction fails
- Results merged without duplicates
- Engine usage logged for each page

### Priority 5: Output Upgrade

**Objective**: Export structured records with enhanced metadata.

**Components to Implement:**
1. Extend `ExcelExporter` with Source and Page columns
2. Add ExtractionMethod column
3. Generate summary sheet with statistics
4. Maintain backward compatibility mode

**Export Format:**
```
Main Sheet:
| Source | Page | Tag | Equipment | Rating | Description | Method |

Summary Sheet:
| Metric | Value |
| Total Pages | 15 |
| Table Pages | 8 |
| Scattered Pages | 5 |
| Sparse Pages | 2 |
| Records from TableEngine | 45 |
| Records from ProximityEngine | 23 |
| Records from RegexSystem | 12 |
```

**Testing:**
- Unit tests for Excel export
- Unit tests for summary sheet generation
- Property tests for source/page tracking
- Integration test: full export with all page types
- Integration test: backward compatibility mode

**Acceptance Criteria:**
- Excel includes Source and Page columns
- Summary sheet generated when records exist
- Legacy format available via configuration
- Export handles large documents (100+ pages)

## Configuration

### Feature Flags

```xml
<!-- extraction_config.xml -->
<ExtractionConfig>
  <!-- Existing configuration preserved -->
  <Patterns>
    <!-- ... existing patterns ... -->
  </Patterns>
  
  <!-- New layout-aware configuration -->
  <LayoutAware>
    <Enabled>true</Enabled>
    <PageClassification>
      <MinTokensForAnalysis>10</MinTokensForAnalysis>
      <RowTolerancePixels>5.0</RowTolerancePixels>
      <ColumnTolerancePixels>10.0</ColumnTolerancePixels>
      <MinRowsForTable>3</MinRowsForTable>
      <MinColumnsForTable>2</MinColumnsForTable>
    </PageClassification>
    <ProximityEngine>
      <DistanceThresholdPixels>100.0</DistanceThresholdPixels>
    </ProximityEngine>
    <Export>
      <IncludeSummarySheet>true</IncludeSummarySheet>
      <LegacyFormatMode>false</LegacyFormatMode>
    </Export>
  </LayoutAware>
</ExtractionConfig>
```

### Configuration Loading

```csharp
public class LayoutAwareConfig
{
    public bool Enabled { get; set; } = true;
    public int MinTokensForAnalysis { get; set; } = 10;
    public double RowTolerancePixels { get; set; } = 5.0;
    public double ColumnTolerancePixels { get; set; } = 10.0;
    public int MinRowsForTable { get; set; } = 3;
    public int MinColumnsForTable { get; set; } = 2;
    public double DistanceThresholdPixels { get; set; } = 100.0;
    public bool IncludeSummarySheet { get; set; } = true;
    public bool LegacyFormatMode { get; set; } = false;
}
```

## Performance Considerations

### Expected Performance Impact

- **Layout Token Extraction**: +2-5% processing time (minimal overhead)
- **Page Classification**: +1-2% processing time (simple clustering)
- **Table Engine**: +5-10% processing time (row/column analysis)
- **Proximity Engine**: +10-15% processing time (distance calculations)
- **Overall Impact**: +10-20% total processing time

### Optimization Strategies

1. **Lazy Evaluation**: Only extract layout tokens when layout-aware features enabled
2. **Caching**: Cache classification results to avoid recomputation
3. **Parallel Processing**: Process pages in parallel (already implemented)
4. **Early Exit**: Skip structured extraction for Sparse pages
5. **Efficient Clustering**: Use spatial indexing for large token sets

### Memory Considerations

- LayoutToken storage: ~200 bytes per token
- Typical page: 100-500 tokens = 20-100 KB per page
- 100-page document: 2-10 MB additional memory
- Acceptable overhead for modern systems

## Migration Strategy

### Phase 1: Parallel Deployment (Weeks 1-2)

- Deploy with `LayoutAware.Enabled = false`
- Monitor existing functionality
- Verify no regressions

### Phase 2: Gradual Rollout (Weeks 3-4)

- Enable for 10% of documents
- Compare outputs with legacy system
- Collect user feedback

### Phase 3: Full Deployment (Weeks 5-6)

- Enable for all documents
- Monitor performance metrics
- Provide legacy mode for edge cases

### Rollback Plan

- Configuration flag allows instant rollback
- Legacy extraction path always available
- No data loss during rollback



## Summary

This design document specifies the transformation of the OCR system from a text-based extraction engine into a layout-aware industrial document intelligence engine. The transformation is implemented through five priority-based phases:

1. **Layout Token Extraction**: Foundation layer that captures spatial metadata (bounding boxes) from both PdfPig and Tesseract without changing existing behavior
2. **Page Classification**: Intelligent routing that classifies pages as Table, Scattered, or Sparse based on token distribution
3. **Structured Record Builders**: Specialized engines (TableEngine for schedules, ProximityEngine for drawings) that extract structured records using spatial intelligence
4. **Hybrid Orchestration**: Smart routing that applies the appropriate extraction strategy to each page and merges results
5. **Output Upgrade**: Enhanced Excel export with source tracking, extraction method indicators, and summary statistics

### Key Design Decisions

**Non-Breaking Architecture**: All changes extend existing components without modifying core extraction logic. The RawText field and Regex_System remain unchanged, ensuring backward compatibility.

**Graceful Degradation**: Multiple fallback layers ensure the system always produces output. Layout extraction failures fall back to text-only processing, and structured extraction failures fall back to regex-based extraction.

**Dual Testing Strategy**: Property-based testing (FsCheck with 100+ iterations) validates universal correctness properties, while unit tests verify specific examples and edge cases.

**Configurable Behavior**: Feature flags allow enabling/disabling layout-aware processing, adjusting thresholds, and switching between legacy and enhanced output formats.

**Incremental Development**: Five priority phases ensure each capability is stable before building the next, with integration tests after each phase.

### Success Criteria

The transformation is successful when:

1. All 34 correctness properties pass property-based tests
2. Existing text extraction behavior is preserved (backward compatibility tests pass)
3. Equipment schedules are correctly extracted using TableEngine
4. P&ID drawings are correctly extracted using ProximityEngine
5. Mixed documents are processed with appropriate engines for each page
6. Performance overhead is less than 20%
7. System gracefully handles all error conditions without crashes
8. Excel output includes source tracking and extraction method indicators

### Next Steps

After design approval:

1. Implement Priority 1 (Layout Token Extraction) with full test coverage
2. Run integration tests to verify backward compatibility
3. Implement Priority 2 (Page Classification) with classification tests
4. Continue through remaining priorities in sequence
5. Deploy with feature flag disabled initially
6. Gradually enable for production documents with monitoring

