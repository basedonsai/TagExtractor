using System;

namespace OCRTool
{
    /// <summary>
    /// Simple test runner to execute all manual tests
    /// </summary>
    public class RunTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("=======================================================");
            Console.WriteLine("OCRTool Manual Test Suite");
            Console.WriteLine("=======================================================\n");

            // Run Phase 3 tests (Structured Record Builders)
            TestTableEngine.RunAllTests();
            Console.WriteLine("\n");
            TestProximityEngine.RunAllTests();

            Console.WriteLine("\n=======================================================");
            Console.WriteLine("Test Suite Complete");
            Console.WriteLine("=======================================================");
        }
    }
}
