using System;

namespace OCRTool
{
    /// <summary>
    /// Console test runner for Phase 3 checkpoint
    /// </summary>
    public class TestRunner
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=======================================================");
            Console.WriteLine("Phase 3 Checkpoint - Testing Structured Record Builders");
            Console.WriteLine("=======================================================\n");

            // Run TableEngine tests
            TestTableEngine.RunAllTests();
            
            Console.WriteLine("\n\n");
            
            // Run ProximityEngine tests
            TestProximityEngine.RunAllTests();

            Console.WriteLine("\n=======================================================");
            Console.WriteLine("Phase 3 Checkpoint Complete");
            Console.WriteLine("=======================================================");
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
