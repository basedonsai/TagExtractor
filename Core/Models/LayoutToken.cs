namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents a text element with associated spatial metadata including text content,
    /// bounding box coordinates, confidence score, and page number
    /// </summary>
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
