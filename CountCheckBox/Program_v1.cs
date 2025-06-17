//using System;
//using System.IO;

//namespace CountCheckBox
//{
//    class Program
//    {
//        static void Main(string[] args)
//        {
//            try
//            {
//                string sourceImagePath = "sample.png";
//                string testImagePath = "test.png";

//                // Copy sample.png to test.png
//                File.Copy(sourceImagePath, testImagePath, true);
//                Console.WriteLine("Created copy of sample.png as test.png");

//                // Create instance of BoxCounter and process the image
//                var boxCounter = new BoxCounter();
//                int numberOfBoxes = boxCounter.ProcessImageAndCountBoxes(testImagePath, "output.png");
//                Console.WriteLine($"Total boxes found: {numberOfBoxes}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//            }
//        }
//    }
//}
