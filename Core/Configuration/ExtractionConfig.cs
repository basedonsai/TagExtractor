using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace OCRTool.Core.Configuration
{
    /// <summary>
    /// Configuration settings for extraction process
    /// </summary>
    [XmlRoot("ExtractionConfig")]
    public class ExtractionConfig
    {
        /// <summary>
        /// When true, only process scanned pages (skip text-searchable pages)
        /// </summary>
        [XmlElement("ProcessOnlyScannedPages")]
        public bool ProcessOnlyScannedPages { get; set; } = false;

        /// <summary>
        /// OCR settings
        /// </summary>
        [XmlElement("OCR")]
        public OCRSettings? OCR { get; set; }

        /// <summary>
        /// List of pattern definitions
        /// </summary>
        [XmlArray("Patterns")]
        [XmlArrayItem("Pattern")]
        public List<PatternDefinition> Patterns { get; set; } = new();

        /// <summary>
        /// List of equipment keywords
        /// </summary>
        [XmlArray("EquipmentKeywords")]
        [XmlArrayItem("Word")]
        public List<string> EquipmentKeywords { get; set; } = new();

        /// <summary>
        /// List of reject line keywords
        /// </summary>
        [XmlArray("RejectLines")]
        [XmlArrayItem("Word")]
        public List<string> RejectLines { get; set; } = new();
    }

    /// <summary>
    /// OCR-specific settings
    /// </summary>
    public class OCRSettings
    {
        [XmlElement("MinConfidence")]
        public int MinConfidence { get; set; } = 60;
    }

    /// <summary>
    /// Pattern definition for tag/equipment matching
    /// </summary>
    public class PatternDefinition
    {
        [XmlElement("Name")]
        public string Name { get; set; } = string.Empty;

        [XmlElement("Type")]
        public string Type { get; set; } = string.Empty;

        [XmlElement("Regex")]
        public string Regex { get; set; } = string.Empty;
    }
}
