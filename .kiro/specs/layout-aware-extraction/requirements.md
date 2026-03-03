# Requirements Document

## Introduction

This document specifies the requirements for transforming the OCR system from a text-based extraction engine into a layout-aware industrial document intelligence engine. The system currently processes industrial documents (P&ID drawings, equipment schedules, panel layouts) using a hybrid extraction approach (PdfPig for searchable PDFs, Tesseract OCR for scanned pages) with a text-based pipeline that flattens pages into text blobs and uses regex patterns for extraction.

The transformation will preserve the existing stable text extraction foundation while layering spatial intelligence on top. The system will extract and preserve layout information (bounding boxes, X/Y coordinates), classify pages by layout type, and apply appropriate structured extraction strategies (table-based or proximity-based) to build structured records from industrial documents.

This is a non-breaking enhancement that maintains backward compatibility with the existing regex-based extraction system while enabling advanced layout-aware processing capabilities.

## Glossary

- **Layout_Token**: A text element with associated spatial metadata including text content, bounding box coordinates (X, Y, Width, Height), confidence score, and page number
- **Bounding_Box**: A rectangular region defined by X coordinate, Y coordinate, Width, and Height that represents the spatial location of text on a page
- **Page_Classifier**: A component that analyzes layout token distributions to determine page layout type
- **Page_Type**: An enumeration representing the layout classification of a page (Table, Scattered, or Sparse)
- **Table_Engine**: A structured record builder that processes table-like layouts by clustering rows and columns
- **Proximity_Engine**: A structured record builder that processes scattered layouts by grouping nearby elements based on spatial distance
- **Hybrid_Extraction**: The current two-path extraction approach using PdfPig for searchable PDFs and Tesseract OCR for scanned pages
- **Structured_Record**: A data object containing Tag, Equipment, Rating, Description, Source, and Page fields extracted from a document
- **PdfPig**: A library for extracting text and metadata from searchable PDF documents
- **Tesseract**: An OCR engine for extracting text from scanned images
- **Regex_System**: The existing pattern matching system using regular expressions to extract tags and equipment from text
- **TAG**: An identifier extracted from industrial documents (e.g., equipment tag numbers, instrument identifiers)
- **Equipment**: Equipment names or descriptions extracted from industrial documents
- **Rating**: Technical specifications or ratings associated with equipment
- **Row_Cluster**: A group of layout tokens with similar Y coordinates representing a horizontal row
- **Column_Cluster**: A group of layout tokens with similar X coordinates representing a vertical column
- **Anchor_Token**: A layout token used as a reference point for proximity-based grouping (typically a TAG)
- **Spatial_Distance**: The Euclidean distance between two layout tokens based on their bounding box centers

## Requirements

### Requirement 1: Extract Layout Tokens from Searchable PDFs

**User Story:** As a document processing system, I want to extract layout tokens with bounding boxes from searchable PDF pages, so that spatial information is preserved for layout-aware processing.

#### Acceptance Criteria

1. WHEN PdfPig extracts text from a searchable PDF page, THE Layout_Token_Extractor SHALL capture the bounding box coordinates for each text element
2. FOR EACH text element extracted by PdfPig, THE Layout_Token_Extractor SHALL create a Layout_Token containing text content, X coordinate, Y coordinate, Width, Height, confidence score of 100, and page number
3. THE Layout_Token_Extractor SHALL store all Layout_Tokens for a page in a collection associated with that page number
4. WHEN a searchable PDF page contains no text elements, THE Layout_Token_Extractor SHALL return an empty Layout_Token collection
5. THE Layout_Token_Extractor SHALL preserve the coordinate system used by PdfPig without transformation

### Requirement 2: Extract Layout Tokens from OCR Results

**User Story:** As a document processing system, I want to extract layout tokens with bounding boxes from Tesseract OCR results, so that scanned pages have the same spatial information as searchable pages.

#### Acceptance Criteria

1. WHEN Tesseract processes a scanned page, THE Layout_Token_Extractor SHALL extract bounding box data from the Tesseract TSV output format
2. FOR EACH word in the Tesseract TSV output, THE Layout_Token_Extractor SHALL create a Layout_Token containing text content, X coordinate, Y coordinate, Width, Height, word-level confidence score, and page number
3. THE Layout_Token_Extractor SHALL handle Tesseract confidence scores in the range 0 to 100
4. WHEN Tesseract OCR produces no recognized text, THE Layout_Token_Extractor SHALL return an empty Layout_Token collection
5. THE Layout_Token_Extractor SHALL normalize Tesseract coordinates to match the PdfPig coordinate system

### Requirement 3: Maintain Backward Compatibility with Text Extraction

**User Story:** As a system maintainer, I want the layout-aware system to preserve existing text extraction behavior, so that current functionality remains stable during the transition.

#### Acceptance Criteria

1. WHEN a page is processed, THE Batch_Processor SHALL continue to extract raw text exactly as before
2. THE Regex_System SHALL continue to receive the same normalized text input as before
3. WHEN layout token extraction fails, THE Batch_Processor SHALL fall back to text-only extraction without errors
4. THE Batch_Processor SHALL produce identical Excel output format when layout-aware features are disabled
5. FOR ALL pages processed, THE Batch_Processor SHALL maintain the existing hybrid extraction logic (PdfPig for searchable, Tesseract for scanned)

### Requirement 4: Store Layout Tokens Per Page

**User Story:** As a document processing system, I want to store layout tokens separately for each page, so that page-level spatial analysis can be performed.

#### Acceptance Criteria

1. THE Extraction_Result SHALL include a collection of Layout_Tokens associated with the page
2. WHEN a page is processed, THE Batch_Processor SHALL populate the Layout_Token collection alongside the existing RawText field
3. THE Layout_Token collection SHALL be accessible for downstream processing components
4. WHEN no layout tokens are extracted, THE Extraction_Result SHALL contain an empty Layout_Token collection rather than null
5. THE Layout_Token storage SHALL not modify or replace the existing RawText field

### Requirement 5: Classify Page Layout Types

**User Story:** As a document intelligence system, I want to classify pages by layout type, so that appropriate extraction strategies can be applied.

#### Acceptance Criteria

1. WHEN a page contains Layout_Tokens, THE Page_Classifier SHALL analyze the spatial distribution of tokens
2. THE Page_Classifier SHALL assign one of three Page_Types: Table, Scattered, or Sparse
3. WHEN Layout_Tokens show regular row and column alignment, THE Page_Classifier SHALL classify the page as Table
4. WHEN Layout_Tokens are distributed irregularly without clear alignment, THE Page_Classifier SHALL classify the page as Scattered
5. WHEN a page contains fewer than 10 Layout_Tokens, THE Page_Classifier SHALL classify the page as Sparse
6. THE Page_Classifier SHALL store the Page_Type in the Extraction_Result
7. THE Page_Classifier SHALL log the classification decision with reasoning

### Requirement 6: Detect Table-Like Alignment

**User Story:** As a page classifier, I want to detect table-like row and column alignment, so that structured table extraction can be applied.

#### Acceptance Criteria

1. THE Page_Classifier SHALL cluster Layout_Tokens by Y coordinate to identify potential rows
2. THE Page_Classifier SHALL cluster Layout_Tokens by X coordinate to identify potential columns
3. WHEN at least 3 distinct row clusters and 2 distinct column clusters exist, THE Page_Classifier SHALL classify the page as Table
4. THE Page_Classifier SHALL use a Y-coordinate tolerance of 5 pixels for row clustering
5. THE Page_Classifier SHALL use an X-coordinate tolerance of 10 pixels for column clustering
6. THE Page_Classifier SHALL ignore isolated tokens that do not fit into row or column clusters

### Requirement 7: Build Structured Records from Table Layouts

**User Story:** As a document intelligence system, I want to extract structured records from table-like pages, so that equipment schedules and lists are properly parsed.

#### Acceptance Criteria

1. WHEN a page is classified as Table, THE Table_Engine SHALL cluster Layout_Tokens into Row_Clusters by Y coordinate
2. THE Table_Engine SHALL cluster Layout_Tokens into Column_Clusters by X coordinate
3. THE Table_Engine SHALL identify header tokens in the first Row_Cluster
4. THE Table_Engine SHALL map header tokens to field names (Tag, Equipment, Rating, Description)
5. FOR EACH non-header Row_Cluster, THE Table_Engine SHALL build a Structured_Record by extracting tokens from each column
6. THE Table_Engine SHALL assign tokens to record fields based on column-to-header mapping
7. WHEN a row contains a TAG token, THE Table_Engine SHALL create a Structured_Record for that row

### Requirement 8: Build Structured Records from Scattered Layouts

**User Story:** As a document intelligence system, I want to extract structured records from scattered P&ID drawings, so that tags and nearby equipment are properly associated.

#### Acceptance Criteria

1. WHEN a page is classified as Scattered, THE Proximity_Engine SHALL identify all TAG tokens as Anchor_Tokens
2. FOR EACH Anchor_Token, THE Proximity_Engine SHALL calculate Spatial_Distance to all other Layout_Tokens on the page
3. THE Proximity_Engine SHALL group tokens within 100 pixels of an Anchor_Token as related elements
4. THE Proximity_Engine SHALL classify grouped tokens as Equipment or Rating based on pattern matching
5. FOR EACH Anchor_Token with grouped elements, THE Proximity_Engine SHALL create a Structured_Record containing the TAG and associated Equipment and Rating
6. WHEN multiple Equipment tokens are grouped with one TAG, THE Proximity_Engine SHALL concatenate them into a single Equipment field
7. THE Proximity_Engine SHALL sort grouped tokens by Spatial_Distance before concatenation

### Requirement 9: Apply Hybrid Orchestration Logic

**User Story:** As a document processing system, I want to route pages to appropriate extraction engines based on classification, so that optimal extraction strategies are applied.

#### Acceptance Criteria

1. WHEN a page is classified as Table, THE Batch_Processor SHALL invoke the Table_Engine for record building
2. WHEN a page is classified as Scattered, THE Batch_Processor SHALL invoke the Proximity_Engine for record building
3. WHEN a page is classified as Sparse, THE Batch_Processor SHALL skip structured record building and use only Regex_System extraction
4. WHEN structured record building produces zero records, THE Batch_Processor SHALL fall back to Regex_System extraction
5. THE Batch_Processor SHALL merge structured records with regex-based extraction results
6. THE Batch_Processor SHALL log which extraction engine was used for each page

### Requirement 10: Export Structured Output with Source Tracking

**User Story:** As a user, I want extraction results to include source file and page number for each record, so that I can trace results back to original documents.

#### Acceptance Criteria

1. THE Excel_Exporter SHALL include columns for Source, Page, Tag, Equipment, Rating, and Description
2. FOR EACH Structured_Record, THE Excel_Exporter SHALL populate the Source field with the source file name
3. FOR EACH Structured_Record, THE Excel_Exporter SHALL populate the Page field with the page number
4. THE Excel_Exporter SHALL maintain the existing flat export format for backward compatibility
5. WHERE structured records are available, THE Excel_Exporter SHALL include a summary sheet with aggregated statistics

### Requirement 11: Preserve Existing Logging and Diagnostics

**User Story:** As a system maintainer, I want layout-aware processing to maintain existing logging behavior, so that debugging and monitoring capabilities are preserved.

#### Acceptance Criteria

1. THE Batch_Processor SHALL continue to log page processing status (searchable vs scanned)
2. THE Batch_Processor SHALL log layout token extraction statistics (token count, extraction time)
3. THE Page_Classifier SHALL log classification decisions with Page_Type and reasoning
4. THE Table_Engine SHALL log row and column cluster counts
5. THE Proximity_Engine SHALL log anchor token count and grouping statistics
6. WHEN layout-aware processing fails, THE Batch_Processor SHALL log the error and fall back to text-only extraction

### Requirement 12: Handle Mixed Content Documents

**User Story:** As a document processing system, I want to handle documents with mixed page types, so that each page is processed with the most appropriate strategy.

#### Acceptance Criteria

1. THE Batch_Processor SHALL classify each page independently
2. WHEN a document contains both Table and Scattered pages, THE Batch_Processor SHALL apply the appropriate engine to each page
3. THE Batch_Processor SHALL maintain page-level extraction results separately
4. THE Excel_Exporter SHALL combine results from all pages into a single output file
5. THE Excel_Exporter SHALL indicate the extraction method used for each record (Table_Engine, Proximity_Engine, or Regex_System)

### Requirement 13: Implement Non-Breaking Changes

**User Story:** As a system maintainer, I want layout-aware features to be added without breaking existing functionality, so that the system remains stable during development.

#### Acceptance Criteria

1. THE Layout_Token_Extractor SHALL be implemented as an optional component that can be disabled
2. WHEN layout-aware processing is disabled, THE Batch_Processor SHALL behave exactly as before
3. THE Extraction_Result model SHALL extend existing fields without removing or modifying them
4. THE Excel_Exporter SHALL support both legacy and structured output formats
5. THE Batch_Processor SHALL include a configuration flag to enable or disable layout-aware processing

### Requirement 14: Parse and Pretty-Print Layout Tokens

**User Story:** As a developer, I want to serialize and deserialize layout tokens, so that intermediate results can be saved and loaded for debugging and testing.

#### Acceptance Criteria

1. THE Layout_Token_Serializer SHALL serialize Layout_Token collections to JSON format
2. THE Layout_Token_Serializer SHALL deserialize JSON back into Layout_Token collections
3. THE Layout_Token_Pretty_Printer SHALL format Layout_Token collections into human-readable text
4. FOR ALL valid Layout_Token collections, serializing then deserializing SHALL produce an equivalent collection (round-trip property)
5. THE Layout_Token_Pretty_Printer SHALL include bounding box coordinates and text content in the output

### Requirement 15: Validate Table Engine Correctness

**User Story:** As a quality assurance engineer, I want to verify that the Table Engine correctly extracts records from table layouts, so that structured data extraction is reliable.

#### Acceptance Criteria

1. WHEN the Table_Engine processes a page with N rows and M columns, THE Table_Engine SHALL produce at most N-1 Structured_Records (excluding header row)
2. FOR ALL Structured_Records produced by the Table_Engine, each record SHALL contain at least a TAG field
3. WHEN a table row contains tokens in K columns, THE corresponding Structured_Record SHALL contain at most K populated fields
4. THE Table_Engine SHALL preserve the order of rows from top to bottom of the page
5. FOR ALL Layout_Tokens in a Row_Cluster, the Y coordinate variance SHALL be less than 10 pixels

### Requirement 16: Validate Proximity Engine Correctness

**User Story:** As a quality assurance engineer, I want to verify that the Proximity Engine correctly groups related elements, so that scattered layout extraction is reliable.

#### Acceptance Criteria

1. FOR ALL Anchor_Tokens identified by the Proximity_Engine, the token text SHALL match the TAG pattern from the Regex_System
2. WHEN the Proximity_Engine groups tokens around an Anchor_Token, all grouped tokens SHALL be within the specified distance threshold
3. FOR ALL Structured_Records produced by the Proximity_Engine, the TAG field SHALL be populated with the Anchor_Token text
4. WHEN multiple tokens are grouped with an Anchor_Token, THE Proximity_Engine SHALL include all tokens within the distance threshold
5. THE Proximity_Engine SHALL produce at most one Structured_Record per Anchor_Token

### Requirement 17: Implement Incremental Priority-Based Development

**User Story:** As a project manager, I want the layout-aware transformation to be implemented in priority order, so that foundational capabilities are stable before advanced features are built.

#### Acceptance Criteria

1. THE development SHALL complete Priority 1 (Layout Token Extraction) before starting Priority 2
2. THE development SHALL complete Priority 2 (Page Classification) before starting Priority 3
3. THE development SHALL complete Priority 3 (Record Builders) before starting Priority 4
4. THE development SHALL complete Priority 4 (Hybrid Orchestration) before starting Priority 5
5. WHEN each priority is completed, THE system SHALL remain in a stable, testable state
6. THE development SHALL include integration tests after each priority completion

