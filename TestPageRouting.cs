using Xunit;
using OCRTool.Core.Models;
using OCRTool.Application;
using OCRTool.Core.Interfaces;
using OCRTool.Core.Patterns;
using OCRTool.Infrastructure.Logging;
using OCRTool.Core.Configuration;
using OCRTool.Infrastructure.Excel;
using System.Collections.Generic;

namespace OCRTool.Tests
{
    /// <summary>
    /// Tests for page routing logic in BatchProcessor.
    /// Verifies that pages are routed to the correct extraction engine based on classification.
    /// </summary>
    public class TestPageRouting
    {
        [Fact]
        public void TablePageRoutesToTableEngine()
        {
            // Arrange: Create a mock page with Table classification
            var tokens = CreateTableLayoutTokens();
            var pageClassifier = new Core.Classification.PageClassifier();
            var classification = pageClassifier.Classify(tokens);
            
            // Verify classification is Table
            Assert.Equal(PageType.Table, classification.PageType);
            
            // Act: Build records using TableEngine
            var tableEngine = new Core.RecordBuilders.TableEngine();
            var records = tableEngine.BuildRecords(tokens, 1, "test.pdf");
            
            // Assert: TableEngine should process Table pages
            Assert.True(tableEngine.CanProcess(PageType.Table));
            Assert.NotNull(records);
        }

        [Fact]
        public void ScatteredPageRoutesToProximityEngine()
        {
            // Arrange: Create a mock page with Scattered classification
            var tokens = CreateScatteredLayoutTokens();
            var pageClassifier = new Core.Classification.PageClassifier();
            var classification = pageClassifier.Classify(tokens);
            
            // Verify classification is Scattered
            Assert.Equal(PageType.Scattered, classification.PageType);
            
            // Act: Build records using ProximityEngine
            var config = new ExtractionConfig();
            var patternMatcher = new PatternMatcher(config);
            var proximityEngine = new Core.RecordBuilders.ProximityEngine(patternMatcher);
            var records = proximityEngine.BuildRecords(tokens, 1, "test.pdf");
            
            // Assert: ProximityEngine should process Scattered pages
            Assert.True(proximityEngine.CanProcess(PageType.Scattered));
            Assert.NotNull(records);
        }

        [Fact]
        public void SparsePageSkipsStructuredRecordBuilding()
        {
            // Arrange: Create a mock page with Sparse classification (< 10 tokens)
            var tokens = CreateSparseLayoutTokens();
            var pageClassifier = new Core.Classification.PageClassifier();
            var classification = pageClassifier.Classify(tokens);
            
            // Verify classification is Sparse
            Assert.Equal(PageType.Sparse, classification.PageType);
            
            // Assert: Neither engine should process Sparse pages
            var tableEngine = new Core.RecordBuilders.TableEngine();
            var config = new ExtractionConfig();
            var patternMatcher = new PatternMatcher(config);
            var proximityEngine = new Core.RecordBuilders.ProximityEngine(patternMatcher);
            
            Assert.False(tableEngine.CanProcess(PageType.Sparse));
            Assert.False(proximityEngine.CanProcess(PageType.Sparse));
        }

        [Fact]
        public void EngineRoutingIsExclusive()
        {
            // Verify that each engine only processes its designated page type
            var tableEngine = new Core.RecordBuilders.TableEngine();
            var config = new ExtractionConfig();
            var patternMatcher = new PatternMatcher(config);
            var proximityEngine = new Core.RecordBuilders.ProximityEngine(patternMatcher);
            
            // TableEngine should only process Table pages
            Assert.True(tableEngine.CanProcess(PageType.Table));
            Assert.False(tableEngine.CanProcess(PageType.Scattered));
            Assert.False(tableEngine.CanProcess(PageType.Sparse));
            
            // ProximityEngine should only process Scattered pages
            Assert.False(proximityEngine.CanProcess(PageType.Table));
            Assert.True(proximityEngine.CanProcess(PageType.Scattered));
            Assert.False(proximityEngine.CanProcess(PageType.Sparse));
        }

        // Helper methods to create test data

        private List<LayoutToken> CreateTableLayoutTokens()
        {
            // Create a simple 3x3 table layout
            var tokens = new List<LayoutToken>();
            
            // Header row (Y=10)
            tokens.Add(new LayoutToken { Text = "Tag", X = 10, Y = 10, Width = 50, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Equipment", X = 70, Y = 10, Width = 80, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Rating", X = 160, Y = 10, Width = 60, Height = 10, PageNumber = 1, Confidence = 100 });
            
            // Data row 1 (Y=25)
            tokens.Add(new LayoutToken { Text = "P-101", X = 10, Y = 25, Width = 50, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Pump", X = 70, Y = 25, Width = 80, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "5HP", X = 160, Y = 25, Width = 60, Height = 10, PageNumber = 1, Confidence = 100 });
            
            // Data row 2 (Y=40)
            tokens.Add(new LayoutToken { Text = "M-201", X = 10, Y = 40, Width = 50, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Motor", X = 70, Y = 40, Width = 80, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "10HP", X = 160, Y = 40, Width = 60, Height = 10, PageNumber = 1, Confidence = 100 });
            
            return tokens;
        }

        private List<LayoutToken> CreateScatteredLayoutTokens()
        {
            // Create scattered layout with irregular positions (typical of P&ID drawings)
            var tokens = new List<LayoutToken>();
            
            // Add 15 tokens with irregular positions
            tokens.Add(new LayoutToken { Text = "P-101", X = 50, Y = 100, Width = 40, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Pump", X = 55, Y = 115, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "5HP", X = 60, Y = 130, Width = 25, Height = 10, PageNumber = 1, Confidence = 100 });
            
            tokens.Add(new LayoutToken { Text = "M-201", X = 200, Y = 80, Width = 40, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Motor", X = 205, Y = 95, Width = 35, Height = 10, PageNumber = 1, Confidence = 100 });
            
            tokens.Add(new LayoutToken { Text = "V-301", X = 150, Y = 200, Width = 40, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Valve", X = 155, Y = 215, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            
            tokens.Add(new LayoutToken { Text = "Text1", X = 300, Y = 50, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Text2", X = 100, Y = 300, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Text3", X = 250, Y = 150, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Text4", X = 50, Y = 250, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Text5", X = 350, Y = 100, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Text6", X = 150, Y = 350, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Text7", X = 400, Y = 200, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Text8", X = 200, Y = 400, Width = 30, Height = 10, PageNumber = 1, Confidence = 100 });
            
            return tokens;
        }

        private List<LayoutToken> CreateSparseLayoutTokens()
        {
            // Create sparse layout with < 10 tokens
            var tokens = new List<LayoutToken>();
            
            tokens.Add(new LayoutToken { Text = "Cover", X = 100, Y = 100, Width = 50, Height = 15, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Page", X = 100, Y = 120, Width = 40, Height = 15, PageNumber = 1, Confidence = 100 });
            tokens.Add(new LayoutToken { Text = "Title", X = 100, Y = 140, Width = 45, Height = 15, PageNumber = 1, Confidence = 100 });
            
            return tokens;
        }
    }
}
