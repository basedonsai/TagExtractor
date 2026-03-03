using System;

namespace OCRTool.Core.Models
{
    /// <summary>
    /// Represents a rectangular region on a page
    /// </summary>
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
