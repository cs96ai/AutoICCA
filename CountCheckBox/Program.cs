using System;
using System.IO;

namespace CountCheckBox
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string imagePath = "TimeOptions2.png";
                string targetText = "length of stay";

                var timeOptions = new TimeOptions();
                var (x, y) = timeOptions.FindTextLineCoordinates(imagePath);

                if (x == -1 && y == -1)
                {
                    Console.WriteLine($"No match found for '{targetText}'.");
                }
                else
                {
                    Console.WriteLine($"Best match for '{targetText}' found at coordinates: x={x}, y={y}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
