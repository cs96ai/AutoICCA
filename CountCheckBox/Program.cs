using System;
using System.Drawing;
using System.IO;


namespace CountCheckBox
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string imagePath = "TimeOptions3.png";
                string targetText = "length of stay";

                // Load the image as a Bitmap
                Bitmap image = new Bitmap(imagePath);

                // Call the method with Bitmap
                var result = TimeOptions.CheckForBlueBarInTimeSelection(image);

                if (result.ColorFound)
                {
                    Console.WriteLine("Blue bar found in time selection.");
                }
                else
                {
                    Console.WriteLine($"Blue bar not found. Suggested click coordinates: x={result.XCoordinate}, y={result.YCoordinate}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
