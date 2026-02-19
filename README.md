# OCR Tool - PDF Tag & Equipment Extractor

A C# WPF application that extracts equipment tags and model numbers from PDF documents using OCR and pattern matching.

**Status: Work in Progress** - This is a development version. Not ready for production use.

## Features

- Extracts equipment tags, VFD models, and equipment names from PDFs
- Uses Tesseract OCR for scanned documents
- Configurable regex patterns via XML file
- Batch processing of multiple PDFs
- Excel export with Tags and Equipment sheets

## Known Issues

- OCR processing needs refinement to match Python version output
- Pattern matching may produce duplicates or partial matches
- Confidence values not fully implemented

## Development Setup

```bash
git clone https://github.com/basedonsai/TagExtractor.git
cd TagExtractor
dotnet build
dotnet run
```

## Project Structure

```
OCRTool/
├── Application/          # Business logic and orchestration
├── Core/                 # Domain models and interfaces
├── Infrastructure/       # External integrations (PDF, OCR, Excel)
├── UI/                   # WPF user interface
├── assets/               # Configuration files
│   └── extraction_config.xml
└── tessdata/             # Tesseract language data
    └── eng.traineddata
```

## Requirements

- Windows 10 or later (64-bit)
- .NET 10.0 SDK
- Visual Studio 2022 or later (recommended)

## License

MIT
