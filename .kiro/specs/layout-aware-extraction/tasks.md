# Implementation Plan: Layout-Aware Extraction

## Overview

This implementation plan transforms the OCR system from a text-based extraction engine into a layout-aware industrial document intelligence engine. The system will extract spatial metadata (bounding boxes), classify pages by layout type, and apply specialized extraction strategies (table-based or proximity-based) to build structured records from industrial documents.

The implementation follows 5 priority-based phases, with each phase building on the previous one. All changes maintain backward compatibility with the existing regex-based extraction system.

## Tasks

### Phase 1: Layout Token Extraction (Foundation)

- [x] 1. Set up data models for layout tokens
  - [x] 1.1 Create LayoutToken model with spatial metadata
    - Create `OCRTool.Core.Models.LayoutToken` class with properties: Text, X, Y, Width, Height, Confidence, PageNumber
    - Add computed properties: CenterX, CenterY, BoundingBox
    - _Requirements: 1.2, 2.2_
  
  - [x] 1.2 Create BoundingBox model
    - Create `OCRTool.Core.Models.BoundingBox` class with X, Y, Width, Height properties
    - Add computed properties: Left, Right, Top, Bottom, CenterX, CenterY
    - Implement DistanceTo method for Euclidean distance calculation
    - _Requirements: 1.2, 2.2_
  
  - [x] 1.3 Extend ExtractionResult model
    - Add `LayoutTokens` collection property (initialized to empty list, never null)
    - Ensure existing fields (SourceFile, PageNumber, IsSearchable, Tags, Equipment, Confidence, RawText) remain unchanged
    - _Requirements: 4.1, 4.2, 4.4, 4.5, 13.3_

- [x] 2. Implement layout token extraction from PdfPig
  - [x] 2.1 Create ILayoutTokenExtractor interface
    - Define ExtractFromPdfPigPage method signature
    - Define ExtractFromTesseractTsv method signature
    - Define NormalizeCoordinates method signature
    - _Requirements: 1.1, 2.1_
  
  - [x] 2.2 Implement PdfPig extraction in LayoutTokenExtractor
    - Extract bounding box coordinates from PdfPig word elements
    - Create LayoutToken for each text element with confidence = 100
    - Preserve PdfPig coordinate system without transformation
    - Return empty collection (not null) on extraction failure
    - Add error logging with try-catch wrapper
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_
  
  - [ ]* 2.3 Write property test for PdfPig layout token structure
    - **Property 1: PdfPig Layout Token Structure**
    - **Validates: Requirements 1.1, 1.2, 1.3**
    - Verify all text elements produce tokens with valid coordinates and confidence = 100
  
  - [ ]* 2.4 Write unit tests for PdfPig extraction edge cases
    - Test empty page returns empty collection
    - Test malformed PDF handling
    - Test coordinate preservation
    - _Requirements: 1.4_

- [x] 3. Implement layout token extraction from Tesseract
  - [x] 3.1 Modify Tesseract invocation to output TSV format
    - Add `--tsv` flag to Tesseract command line arguments
    - Update TesseractOCRProvider to capture TSV output
    - _Requirements: 2.1_
  
  - [x] 3.2 Implement Tesseract TSV parsing in LayoutTokenExtractor
    - Parse TSV format (level, page_num, block_num, par_num, line_num, word_num, left, top, width, height, conf, text)
    - Extract only word-level entries (level 5)
    - Create LayoutToken for each word with confidence from TSV
    - Handle confidence scores in range 0-100
    - Return empty collection on parsing failure
    - Add error logging with try-catch wrapper
    - _Requirements: 2.1, 2.2, 2.3, 2.4_
  
  - [x] 3.3 Implement coordinate normalization
    - Convert Tesseract top-left origin to PdfPig bottom-left origin
    - Apply formula: Y_pdfpig = pageHeight - Y_tesseract - Height
    - Validate coordinates (check for NaN, Infinity)
    - _Requirements: 2.5_
  
  - [ ]* 3.4 Write property test for Tesseract layout token structure
    - **Property 3: Tesseract Layout Token Structure**
    - **Validates: Requirements 2.1, 2.2, 2.3**
    - Verify all words produce tokens with valid coordinates and confidence 0-100
  
  - [ ]* 3.5 Write property test for coordinate normalization
    - **Property 4: Tesseract Coordinate Normalization**
    - **Validates: Requirements 2.5**
    - Verify normalized coordinates match PdfPig coordinate system
  
  - [ ]* 3.6 Write unit tests for Tesseract extraction edge cases
    - Test invalid TSV format handling
    - Test empty OCR results
    - Test coordinate validation (NaN, Infinity)
    - _Requirements: 2.4_

- [x] 4. Integrate layout token extraction into BatchProcessor
  - [x] 4.1 Inject ILayoutTokenExtractor into BatchProcessor
    - Add constructor parameter for ILayoutTokenExtractor
    - Register LayoutTokenExtractor in dependency injection container
    - _Requirements: 4.2_
  
  - [x] 4.2 Call layout token extractor in ProcessPageAsync
    - Invoke ExtractFromPdfPigPage for searchable pages
    - Invoke ExtractFromTesseractTsv for scanned pages
    - Populate ExtractionResult.LayoutTokens collection
    - Ensure RawText extraction continues unchanged
    - Add graceful fallback on extraction failure (empty collection, continue processing)
    - _Requirements: 3.1, 3.3, 4.2, 4.3_
  
  - [x] 4.3 Add layout token extraction logging
    - Log token count for each page
    - Log extraction time
    - Log extraction failures with details
    - _Requirements: 11.2_
  
  - [ ]* 4.4 Write property test for backward compatible text extraction
    - **Property 5: Backward Compatible Text Extraction**
    - **Validates: Requirements 3.1, 3.2, 3.5**
    - Verify RawText field unchanged and Regex_System receives same input
  
  - [ ]* 4.5 Write property test for graceful degradation
    - **Property 6: Graceful Degradation on Failure**
    - **Validates: Requirements 3.3**
    - Verify text-only extraction succeeds when layout extraction fails
  
  - [ ]* 4.6 Write property test for non-null layout token collections
    - **Property 9: Non-Null Layout Token Collections**
    - **Validates: Requirements 4.4**
    - Verify LayoutTokens collection never null, even when empty

- [x] 5. Checkpoint - Phase 1 complete
  - Ensure all tests pass, ask the user if questions arise.

### Phase 2: Page Classification

- [x] 6. Set up page classification models
  - [x] 6.1 Create PageType enumeration
    - Define enum with values: Table, Scattered, Sparse
    - Add XML documentation for each value
    - _Requirements: 5.2_
  
  - [x] 6.2 Create PageClassification model
    - Create class with properties: PageType, Reasoning, RowCount, ColumnCount
    - _Requirements: 5.6, 5.7_
  
  - [x] 6.3 Extend ExtractionResult with Classification field
    - Add nullable PageClassification property
    - _Requirements: 5.6_

- [x] 7. Implement page classification logic
  - [x] 7.1 Create IPageClassifier interface
    - Define Classify method signature
    - Define HasTableStructure method signature
    - _Requirements: 5.1_
  
  - [x] 7.2 Implement clustering algorithm
    - Create ClusterByCoordinate helper method
    - Accept coordinate selector function and tolerance parameter
    - Sort tokens by coordinate
    - Group tokens within tolerance into clusters
    - Return list of token clusters
    - _Requirements: 6.1, 6.2_
  
  - [x] 7.3 Implement sparse page detection
    - Check if token count < 10
    - Return Sparse classification with reasoning
    - _Requirements: 5.5_
  
  - [x] 7.4 Implement table structure detection
    - Cluster tokens by Y-coordinate with 5-pixel tolerance (rows)
    - Cluster tokens by X-coordinate with 10-pixel tolerance (columns)
    - Check if rowCount >= 3 and columnCount >= 2
    - Return true if thresholds met
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
  
  - [x] 7.5 Implement Classify method in PageClassifier
    - Handle empty/null token collections (return Sparse)
    - Check sparse threshold first
    - Check table structure second
    - Default to Scattered if neither condition met
    - Include reasoning in classification result
    - _Requirements: 5.1, 5.2, 5.3, 5.4_
  
  - [ ]* 7.6 Write property test for classification output domain
    - **Property 10: Classification Output Domain**
    - **Validates: Requirements 5.1, 5.2, 5.6**
    - Verify exactly one PageType assigned from {Table, Scattered, Sparse}
  
  - [ ]* 7.7 Write property test for sparse page classification
    - **Property 11: Sparse Page Classification**
    - **Validates: Requirements 5.5**
    - Verify pages with < 10 tokens classified as Sparse
  
  - [ ]* 7.8 Write property test for table page classification
    - **Property 12: Table Page Classification**
    - **Validates: Requirements 5.3, 6.1, 6.2, 6.3, 6.4, 6.5**
    - Verify pages with 3+ rows and 2+ columns classified as Table
  
  - [ ]* 7.9 Write property test for scattered page classification
    - **Property 13: Scattered Page Classification**
    - **Validates: Requirements 5.4**
    - Verify pages with 10+ tokens but no table structure classified as Scattered
  
  - [ ]* 7.10 Write unit tests for page classification
    - Test equipment schedule layout returns Table
    - Test P&ID drawing layout returns Scattered
    - Test cover page returns Sparse
    - Test clustering algorithm with various tolerances
    - _Requirements: 5.3, 5.4, 5.5_

- [x] 8. Integrate page classification into BatchProcessor
  - [x] 8.1 Inject IPageClassifier into BatchProcessor
    - Add constructor parameter for IPageClassifier
    - Register PageClassifier in dependency injection container
    - _Requirements: 5.1_
  
  - [x] 8.2 Call page classifier in ProcessPageAsync
    - Invoke Classify after layout token extraction
    - Populate ExtractionResult.Classification field
    - Handle classification failures (default to Sparse, log error)
    - _Requirements: 5.1, 5.6_
  
  - [x] 8.3 Add classification logging
    - Log PageType for each page
    - Log reasoning for classification decision
    - Log row and column counts for Table pages
    - _Requirements: 5.7, 11.3_
  
  - [ ]* 8.4 Write property test for independent page classification
    - **Property 29: Independent Page Classification**
    - **Validates: Requirements 12.1**
    - Verify classification of one page doesn't affect another
  
  - [ ]* 8.5 Write integration test for mixed document classification
    - Test document with Table, Scattered, and Sparse pages
    - Verify each page classified independently
    - _Requirements: 12.1, 12.2_

- [x] 9. Checkpoint - Phase 2 complete
  - Ensure all tests pass, ask the user if questions arise.

### Phase 3: Structured Record Builders

- [x] 10. Set up structured record models
  - [x] 10.1 Create ExtractionMethod enumeration
    - Define enum with values: TableEngine, ProximityEngine, RegexSystem
    - _Requirements: 12.5_
  
  - [x] 10.2 Create StructuredRecord model
    - Create class with properties: Tag, Equipment, Rating, Description, Source, Page, Method
    - Initialize string properties to empty string
    - _Requirements: 10.2, 10.3, 12.5_
  
  - [x] 10.3 Create RowCluster and ColumnCluster helper models
    - RowCluster: Tokens list, MinY, MaxY, AvgY properties
    - ColumnCluster: Tokens list, MinX, MaxX, AvgX, ColumnIndex properties
    - _Requirements: 7.1, 7.2_
  
  - [x] 10.4 Extend ExtractionResult with StructuredRecords field
    - Add StructuredRecords collection property (initialized to empty list)
    - _Requirements: 9.5_

- [-] 11. Implement Table Engine
  - [x] 11.1 Create IRecordBuilder interface
    - Define BuildRecords method signature
    - Define CanProcess method signature
    - _Requirements: 7.1_
  
  - [x] 11.2 Implement row and column clustering in TableEngine
    - Reuse ClusterByCoordinate algorithm from PageClassifier
    - Cluster tokens by Y-coordinate with 5-pixel tolerance (rows)
    - Cluster tokens by X-coordinate with 10-pixel tolerance (columns)
    - Sort row clusters by MinY (top to bottom)
    - _Requirements: 7.1, 7.2_
  
  - [x] 11.3 Implement header identification and mapping
    - Identify first row cluster as header row
    - Map header tokens to field names (Tag, Equipment, Rating, Description)
    - Use fuzzy matching on header text (contains "tag", "equipment", "rating", "desc")
    - Create column index to field name mapping
    - _Requirements: 7.3, 7.4_
  
  - [x] 11.4 Implement record building from table rows
    - Iterate through data rows (skip header row)
    - For each row, extract tokens from each column
    - Map column tokens to record fields using header mapping
    - Create StructuredRecord only if TAG field populated
    - Set Source and Page fields
    - Set Method to TableEngine
    - _Requirements: 7.5, 7.6, 7.7_
  
  - [ ]* 11.5 Write property test for table engine row clustering
    - **Property 14: Table Engine Row Clustering**
    - **Validates: Requirements 7.1, 15.5**
    - Verify all tokens in row cluster have Y-coordinate variance < 10 pixels
  
  - [ ]* 11.6 Write property test for table engine record count bound
    - **Property 15: Table Engine Record Count Bound**
    - **Validates: Requirements 15.1**
    - Verify at most N-1 records produced from N rows
  
  - [ ]* 11.7 Write property test for table engine record validity
    - **Property 16: Table Engine Record Validity**
    - **Validates: Requirements 7.7, 15.2**
    - Verify all records have non-empty TAG field
  
  - [ ]* 11.8 Write property test for table engine field count bound
    - **Property 17: Table Engine Field Count Bound**
    - **Validates: Requirements 15.3**
    - Verify record with K columns has at most K populated fields
  
  - [ ]* 11.9 Write property test for table engine row order preservation
    - **Property 18: Table Engine Row Order Preservation**
    - **Validates: Requirements 15.4**
    - Verify records appear in top-to-bottom order
  
  - [ ]* 11.10 Write unit tests for TableEngine
    - Test 3-row table produces 2 records
    - Test row without TAG is skipped
    - Test header mapping with various header texts
    - Test empty table handling
    - _Requirements: 7.7, 15.1_

- [~] 12. Implement Proximity Engine
  - [x] 12.1 Implement TAG token identification
    - Use existing PatternMatcher to identify TAG patterns
    - Filter layout tokens by TAG pattern matching
    - Return list of anchor tokens
    - _Requirements: 8.1_
  
  - [x] 12.2 Implement spatial distance calculation
    - Calculate Euclidean distance between token centers
    - Use formula: sqrt((x1-x2)^2 + (y1-y2)^2)
    - _Requirements: 8.2_
  
  - [x] 12.3 Implement proximity grouping algorithm
    - For each anchor token, calculate distance to all other tokens
    - Filter tokens within 100-pixel threshold
    - Sort grouped tokens by distance (nearest first)
    - _Requirements: 8.2, 8.3, 8.7_
  
  - [x] 12.4 Implement token classification (Equipment vs Rating)
    - Use pattern matching to classify grouped tokens
    - Identify Equipment tokens (descriptive text)
    - Identify Rating tokens (numeric specifications)
    - Concatenate multiple tokens of same type with space separator
    - _Requirements: 8.4, 8.6_
  
  - [x] 12.5 Implement record building from proximity groups
    - For each anchor token with grouped elements, create StructuredRecord
    - Set Tag field to anchor token text
    - Set Equipment field to concatenated equipment tokens
    - Set Rating field to concatenated rating tokens
    - Set Source and Page fields
    - Set Method to ProximityEngine
    - _Requirements: 8.5_
  
  - [ ]* 12.6 Write property test for proximity engine anchor identification
    - **Property 19: Proximity Engine Anchor Identification**
    - **Validates: Requirements 8.1, 16.1**
    - Verify all anchor tokens match TAG pattern
  
  - [ ]* 12.7 Write property test for proximity engine distance threshold
    - **Property 20: Proximity Engine Distance Threshold**
    - **Validates: Requirements 8.2, 8.3, 16.2, 16.4**
    - Verify all grouped tokens within distance threshold
  
  - [ ]* 12.8 Write property test for proximity engine distance sorting
    - **Property 21: Proximity Engine Distance Sorting**
    - **Validates: Requirements 8.7**
    - Verify grouped tokens sorted by distance before concatenation
  
  - [ ]* 12.9 Write property test for proximity engine record validity
    - **Property 22: Proximity Engine Record Validity**
    - **Validates: Requirements 8.5, 16.3**
    - Verify all records have TAG field populated with anchor text
  
  - [ ]* 12.10 Write property test for proximity engine record count
    - **Property 23: Proximity Engine Record Count**
    - **Validates: Requirements 16.5**
    - Verify at most one record per anchor token
  
  - [ ]* 12.11 Write unit tests for ProximityEngine
    - Test TAG with nearby equipment grouped correctly
    - Test tokens beyond threshold excluded
    - Test multiple equipment tokens concatenated
    - Test distance sorting
    - Test page with no TAG anchors
    - _Requirements: 8.3, 8.6, 8.7_

- [x] 13. Checkpoint - Phase 3 complete
  - Ensure all tests pass, ask the user if questions arise.

### Phase 4: Hybrid Orchestration

- [~] 14. Implement routing logic in BatchProcessor
  - [x] 14.1 Inject IRecordBuilder implementations into BatchProcessor
    - Add constructor parameters for TableEngine and ProximityEngine
    - Register both engines in dependency injection container
    - _Requirements: 9.1, 9.2_
  
  - [x] 14.2 Implement page routing based on classification
    - After classification, check PageType
    - If Table: invoke TableEngine.BuildRecords
    - If Scattered: invoke ProximityEngine.BuildRecords
    - If Sparse: skip structured record building
    - Populate ExtractionResult.StructuredRecords
    - _Requirements: 9.1, 9.2, 9.3_
  
  - [~] 14.3 Implement fallback to Regex_System
    - Check if StructuredRecords collection is empty
    - If empty, invoke existing Regex_System extraction
    - Always run Regex_System for Sparse pages
    - _Requirements: 9.3, 9.4_
  
  - [~] 14.4 Implement result merging logic
    - Combine structured records with regex-based extraction results
    - Avoid duplicates (check for matching Tag values)
    - Preserve extraction method indicator for each record
    - _Requirements: 9.5_
  
  - [~] 14.5 Add orchestration logging
    - Log which extraction engine used for each page
    - Log record counts from each engine
    - Log fallback events
    - _Requirements: 9.6, 11.4, 11.5_
  
  - [ ]* 14.6 Write property test for hybrid orchestration routing
    - **Property 24: Hybrid Orchestration Routing**
    - **Validates: Requirements 9.1, 9.2, 9.3**
    - Verify correct engine invoked based on PageType
  
  - [ ]* 14.7 Write property test for fallback to regex system
    - **Property 25: Fallback to Regex System**
    - **Validates: Requirements 9.4**
    - Verify Regex_System invoked when structured extraction produces zero records
  
  - [ ]* 14.8 Write property test for result merging
    - **Property 26: Result Merging**
    - **Validates: Requirements 9.5**
    - Verify final result contains records from both structured and regex extraction
  
  - [ ]* 14.9 Write integration test for mixed document processing
    - **Property 30: Mixed Document Processing**
    - **Validates: Requirements 12.2**
    - Test document with Table, Scattered, and Sparse pages
    - Verify appropriate engine applied to each page
    - Verify results combined correctly
    - _Requirements: 12.1, 12.2, 12.3, 12.4_
  
  - [ ]* 14.10 Write integration test for error handling and fallback
    - Test layout extraction failure falls back to text-only
    - Test classification failure defaults to Sparse
    - Test record building failure falls back to Regex_System
    - _Requirements: 3.3, 9.4, 11.6_

- [~] 15. Implement configuration and feature flags
  - [~] 15.1 Create LayoutAwareConfig model
    - Add properties: Enabled, MinTokensForAnalysis, RowTolerancePixels, ColumnTolerancePixels, MinRowsForTable, MinColumnsForTable, DistanceThresholdPixels
    - Set default values matching design specification
    - _Requirements: 13.1, 13.2_
  
  - [~] 15.2 Add layout-aware configuration section to extraction_config.xml
    - Add LayoutAware section with all configuration parameters
    - Add Enabled flag for feature toggle
    - _Requirements: 13.1_
  
  - [~] 15.3 Implement configuration loading
    - Load LayoutAwareConfig from XML configuration file
    - Inject configuration into components (PageClassifier, TableEngine, ProximityEngine)
    - _Requirements: 13.1_
  
  - [~] 15.4 Implement feature flag check in BatchProcessor
    - Check LayoutAwareConfig.Enabled before invoking layout-aware processing
    - If disabled, skip layout token extraction and classification
    - Ensure existing behavior unchanged when disabled
    - _Requirements: 13.1, 13.2_
  
  - [ ]* 15.5 Write property test for output format backward compatibility
    - **Property 7: Output Format Backward Compatibility**
    - **Validates: Requirements 3.4, 13.2**
    - Verify Excel output identical when layout-aware features disabled
  
  - [ ]* 15.6 Write unit test for backward compatibility
    - Test ProcessPage with layout-aware disabled produces same output
    - Verify LayoutTokens empty and Classification null when disabled
    - _Requirements: 13.2_

- [~] 16. Checkpoint - Phase 4 complete
  - Ensure all tests pass, ask the user if questions arise.

### Phase 5: Output Upgrade

- [~] 17. Implement serialization and debugging utilities
  - [~] 17.1 Create LayoutTokenSerializer
    - Implement Serialize method (LayoutToken collection to JSON)
    - Implement Deserialize method (JSON to LayoutToken collection)
    - Use System.Text.Json or Newtonsoft.Json
    - _Requirements: 14.1, 14.2_
  
  - [~] 17.2 Create LayoutTokenPrettyPrinter
    - Implement Format method for human-readable output
    - Include bounding box coordinates and text content
    - Format as table or structured text
    - _Requirements: 14.3, 14.5_
  
  - [ ]* 17.3 Write property test for serialization round trip
    - **Property 31: Layout Token Serialization Round Trip**
    - **Validates: Requirements 14.1, 14.2, 14.4**
    - Verify serialize then deserialize produces equivalent collection
  
  - [ ]* 17.4 Write property test for pretty printer output completeness
    - **Property 32: Pretty Printer Output Completeness**
    - **Validates: Requirements 14.5**
    - Verify output includes bounding box coordinates and text content
  
  - [ ]* 17.5 Write unit tests for serialization
    - Test empty collection serialization
    - Test collection with various token types
    - Test deserialization of invalid JSON
    - _Requirements: 14.1, 14.2_

- [~] 18. Extend Excel export with structured output
  - [~] 18.1 Add Source and Page columns to Excel export
    - Modify ExcelExporter to include Source column (file name)
    - Add Page column (page number)
    - Populate from StructuredRecord.Source and StructuredRecord.Page
    - _Requirements: 10.1, 10.2, 10.3_
  
  - [~] 18.2 Add ExtractionMethod column to Excel export
    - Add Method column showing TableEngine, ProximityEngine, or RegexSystem
    - Populate from StructuredRecord.Method
    - _Requirements: 12.5_
  
  - [~] 18.3 Implement summary sheet generation
    - Create second sheet named "Summary"
    - Calculate statistics: Total Pages, Table Pages, Scattered Pages, Sparse Pages
    - Calculate record counts: Records from TableEngine, ProximityEngine, RegexSystem
    - Format as two-column table (Metric, Value)
    - _Requirements: 10.5_
  
  - [~] 18.4 Implement legacy format mode
    - Add configuration flag: LayoutAwareConfig.LegacyFormatMode
    - When true, export without Source, Page, Method columns and without summary sheet
    - Maintain exact pre-transformation format
    - _Requirements: 10.4, 13.4_
  
  - [ ]* 18.5 Write property test for source and page tracking
    - **Property 27: Source and Page Tracking**
    - **Validates: Requirements 10.2, 10.3**
    - Verify all records have Source and Page fields populated
  
  - [ ]* 18.6 Write property test for extraction method tracking
    - **Property 28: Extraction Method Tracking**
    - **Validates: Requirements 12.5**
    - Verify all records have ExtractionMethod indicator
  
  - [ ]* 18.7 Write unit tests for Excel export
    - Test export includes Source, Page, Method columns
    - Test summary sheet generation
    - Test legacy format mode
    - Test large document export (100+ pages)
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

- [~] 19. Implement comprehensive logging
  - [~] 19.1 Add logging for all processing stages
    - Log page processing status (searchable vs scanned)
    - Log layout token extraction statistics (token count, extraction time)
    - Log classification decisions with PageType and reasoning
    - Log table engine row and column cluster counts
    - Log proximity engine anchor token count and grouping statistics
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_
  
  - [~] 19.2 Add error logging with fallback
    - Log layout-aware processing failures with full details
    - Log fallback events (to text-only, to Regex_System)
    - Ensure errors don't crash system
    - _Requirements: 11.6_
  
  - [ ]* 19.3 Write property test for logging completeness
    - **Property 33: Logging Completeness**
    - **Validates: Requirements 5.7, 9.6, 11.1, 11.2, 11.3, 11.4, 11.5**
    - Verify all required log entries present for each page
  
  - [ ]* 19.4 Write property test for error logging and fallback
    - **Property 34: Error Logging and Fallback**
    - **Validates: Requirements 11.6**
    - Verify errors logged and text-only extraction succeeds
  
  - [ ]* 19.5 Write unit tests for logging
    - Test log entries for each processing stage
    - Test error log entries
    - Test fallback log entries
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

- [~] 20. Final integration and validation
  - [ ]* 20.1 Write end-to-end integration test
    - Test complete workflow from PDF input to Excel output
    - Test mixed document with all page types
    - Verify all extraction methods used
    - Verify Excel output format correct
    - _Requirements: 17.1, 17.2, 17.3, 17.4, 17.5, 17.6_
  
  - [ ]* 20.2 Write backward compatibility integration test
    - Test system with layout-aware features disabled
    - Verify output identical to pre-transformation system
    - Verify no performance degradation
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 13.2_
  
  - [ ]* 20.3 Write property-based test suite with FsCheck
    - Set up FsCheck with xUnit integration
    - Create custom generators for LayoutToken, table layouts, scattered layouts
    - Run all property tests with 100 iterations
    - Tag each property test with feature name and property number
    - _Requirements: All property tests from design document_

- [~] 21. Final checkpoint - Phase 5 complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional testing tasks and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation after each phase
- Property tests validate universal correctness properties (34 total properties)
- Unit tests validate specific examples and edge cases
- Integration tests validate end-to-end workflows
- All changes maintain backward compatibility with existing regex-based extraction
- Feature flag allows instant rollback if issues arise
- Implementation uses C# with .NET framework
- Testing uses xUnit and FsCheck for property-based testing
