# OCR Tool - PDF Tag & Equipment Extractor

A C# WPF application that extracts equipment tags and model numbers from PDF documents using OCR (Optical Character Recognition) and pattern matching.

## Features

- **PDF Processing**: Extracts text from both searchable and scanned PDF documents
- **OCR Integration**: Uses Tesseract OCR for accurate text recognition from images
- **Pattern Matching**: Configurable regex patterns for extracting:
  - Equipment tags (e.g., EC-EPE255-640052, ES-EPE255-640001B)
  - VFD model numbers (e.g., ACS880-31-0944-3 0.75kW)
  - Equipment with prefixes (e.g., +INC-01, +SD07)
  - Equipment names (e.g., MOTOR, PUMP, VFD, PANEL)
- **Batch Processing**: Process multiple PDF files in one session
- **Excel Export**: Results exported to Excel with separate sheets for Tags and Equipment
- **User-Configurable**: Patterns and keywords can be customized via XML configuration file

## Requirements

- Windows 10 or later
- .NET 10.0 Runtime
- Tesseract OCR language data (English)

## Installation

1. Clone the repository
2. Build the solution using Visual Studio 2022 or later
3. Ensure Tesseract tessdata is available in the output directory

## Configuration

The application uses `extraction_config.xml` for pattern matching configuration:

```xml
<extraction_config>
  <patterns>
    <pattern name="EC_EQUIP_TAG" regex="\bEC-[A-Z0-9]{2,15}-\d{6}\b" type="TAG" />
    <pattern name="VFD_MODEL_POWER" regex="\bACS880-\d{2}-\d{4}-\d\s*\d+[.,]?\d*kW\b" type="MODEL" />
    <!-- Add more patterns as needed -->
  </patterns>
  
  <equipment_keywords>
    <keyword>MOTOR</keyword>
    <keyword>PUMP</keyword>
    <keyword>VFD</keyword>
    <!-- Add more keywords -->
  </equipment_keywords>
  
  <reject_lines>
    <word>We reserve all rights</word>
    <word>Reproduction, Use or disclosure</word>
    <!-- Add words/phrases to reject -->
  </reject_lines>
</extraction_config>
```

## Usage

1. Launch the application
2. Select the input folder containing PDF files
3. Select the output folder for results
4. Click "Process" to start extraction
5. Results are saved as Excel files with:
   - **Tags sheet**: Equipment tags and model numbers
   - **Equipment sheet**: Equipment names and descriptions

## Output Format

### Tags Sheet
| source_file | page | type | value | confidence |
|------------|------|------|-------|------------|
| document.pdf | 9 | MODEL | ACS880-31-0944-3 0.75kW | 75.7 |
| document.pdf | 9 | TAG | +INC-01 | 86.2 |

### Equipment Sheet
| source_file | page | type | value | confidence |
|------------|------|------|-------|------------|
| document.pdf | 9 | EQUIP | +SD07 ACS880-31-09A4-3 0.75kW NEUTRALIZATION TANK-1B | 87.5 |

## Architecture

- **UI Layer**: WPF-based user interface
- **Application Layer**: Batch processing orchestration
- **Core Layer**: Pattern matching and business logic
- **Infrastructure Layer**: 
  - PDF processing (PdfPig)
  - OCR processing (Tesseract.NET)
  - Excel export (ClosedXML)

## Key Technologies

- **C# 10.0** / **.NET 10.0**
- **WPF** for UI
- **PdfPig** for PDF processing
- **Tesseract.NET** for OCR
- **ClosedXML** for Excel generation

## Project Structure

```
OCRTool/
├── Application/          # Business logic and orchestration
├── Core/                 # Domain models and interfaces
├── Infrastructure/       # External integrations (PDF, OCR, Excel)
├── UI/                   # WPF user interface
└── assets/              # Configuration files
    └── extraction_config.xml
```

## Development

### Building from Source

```bash
dotnet build OCRTool.slnx
```

### Running Tests

```bash
dotnet test
```

## License

[Add your license here]

## Contributing

[Add contribution guidelines here]

## Acknowledgments

- Tesseract OCR engine
- PdfPig library for PDF processing
- ClosedXML for Excel generation
