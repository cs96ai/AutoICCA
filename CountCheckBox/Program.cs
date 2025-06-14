using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string sourceImagePath = "sample.png";
        string testImagePath = "test.png";
        
        try
        {
            // Copy sample.png to test.png
            File.Copy(sourceImagePath, testImagePath, true);
            Console.WriteLine("Created copy of sample.png as test.png");

            // Load the copied image
            using (Bitmap image = new Bitmap(testImagePath))
            {
                Console.WriteLine($"Image loaded: {image.Width}x{image.Height} pixels");

                // Convert to black and white
                for (int x = 0; x < image.Width; x++)
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        Color pixelColor = image.GetPixel(x, y);
                        int grayScale = (int)((pixelColor.R * 0.3) + (pixelColor.G * 0.59) + (pixelColor.B * 0.11));
                        Color newColor = (grayScale > 128) ? Color.White : Color.Black;
                        image.SetPixel(x, y, newColor);
                    }
                }

                // Find both top and bottom limits at 53 pixels from the left
                int startX = 53;
                
                // Find BOTTOM_LIMIT (scanning from bottom up)
                int currentY = image.Height - 1;
                bool foundDarkPixel = false;
                int BOTTOM_LIMIT = -1;

                while (currentY >= 0 && !foundDarkPixel)
                {
                    Color pixelColor = image.GetPixel(startX, currentY);
                    if (pixelColor.R == 0 && pixelColor.G == 0 && pixelColor.B == 0)
                    {
                        foundDarkPixel = true;
                        BOTTOM_LIMIT = currentY;
                        Console.WriteLine($"BOTTOM_LIMIT found at: ({startX}, {BOTTOM_LIMIT})");
                    }
                    else
                    {
                        currentY--;
                    }
                }

                // Find TOP_LIMIT (scanning from top down)
                currentY = 0;
                foundDarkPixel = false;
                int TOP_LIMIT = -1;

                while (currentY < image.Height && !foundDarkPixel)
                {
                    Color pixelColor = image.GetPixel(startX, currentY);
                    if (pixelColor.R == 0 && pixelColor.G == 0 && pixelColor.B == 0)
                    {
                        foundDarkPixel = true;
                        TOP_LIMIT = currentY;
                        Console.WriteLine($"TOP_LIMIT found at: ({startX}, {TOP_LIMIT})");
                    }
                    else
                    {
                        currentY++;
                    }
                }

                // Now draw the continuous red line between the limits
                // Calculate box dimensions and positioning
                int boxHeight = 12;  // 12 pixels high (increased by 2)
                int boxWidth = (int)(image.Width * 0.8);  // 80% of screen width
                int boxLeftMargin = (image.Width - boxWidth) / 2;  // Center horizontally
                int bottomCushion = 4;  // 4 pixel cushion between boxes (increased by 2)
                
                // Calculate spacing to evenly distribute boxes
                int availableHeight = BOTTOM_LIMIT - TOP_LIMIT;
                int boxesCount = (availableHeight + (boxHeight + bottomCushion) - 1) / (boxHeight + bottomCushion);
                
                // Calculate even spacing between boxes
                int totalBoxesHeight = boxesCount * boxHeight;
                int remainingSpace = availableHeight - totalBoxesHeight;
                int evenSpacing = remainingSpace / (boxesCount + 1);
                
                Console.WriteLine($"Drawing {boxesCount} boxes between {TOP_LIMIT} and {BOTTOM_LIMIT} with {evenSpacing}px spacing");

                using (Graphics graphics = Graphics.FromImage(image))
                {
                    // Draw the reference lines in red
                    using (Pen redPen = new Pen(Color.Red, 2))
                    {
                        graphics.DrawLine(redPen, startX, 0, startX, TOP_LIMIT);
                        graphics.DrawLine(redPen, startX, image.Height - 1, startX, BOTTOM_LIMIT);
                    }

                    // Draw the boxes in red
                    using (Pen redPen = new Pen(Color.Red, 1))
                    {
                        int boxY = TOP_LIMIT + evenSpacing;  // Start after first spacing

                        for (int i = 0; i < boxesCount; i++)
                        {
                            graphics.DrawRectangle(
                                redPen,
                                boxLeftMargin,  // X position
                                boxY,          // Y position
                                boxWidth,       // Width
                                boxHeight       // Height
                            );

                            boxY += boxHeight + evenSpacing;
                        }
                    }
                }

                // Save the modified image back to test.png
                image.Save("output.png", ImageFormat.Png);
                
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing image: {ex.Message}");
        }
    }
}
