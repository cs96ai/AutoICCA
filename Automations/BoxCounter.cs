using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CountCheckBox
{
    public class BoxCounter
    {
        private const int BOX_HEIGHT = 12;
        private const int BOTTOM_CUSHION = 4;
        private const int SCAN_X_POSITION = 53;

        /// <summary>
        /// Processes an image to count the number of boxes that can fit between detected boundaries.
        /// Converts the image to black and white, finds top and bottom boundaries, and draws boxes.
        /// </summary>
        /// <param name="imagePath">Path to the source image</param>
        /// <param name="outputPath">Path where the processed image will be saved</param>
        /// <param name="drawDebugLines">Whether to draw reference lines showing the detected boundaries</param>
        /// <returns>Number of boxes that can fit in the detected space</returns>
        public int ProcessImageAndCountBoxes(string imagePath, string outputPath, bool drawDebugLines = true)
        {
            using (Bitmap image = new Bitmap(imagePath))
            {
                Console.WriteLine($"Processing image: {image.Width}x{image.Height} pixels");

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
                int startX = SCAN_X_POSITION;
                
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

                // Calculate box dimensions and positioning
                int boxHeight = BOX_HEIGHT;
                int boxWidth = (int)(image.Width * 0.8);  // 80% of screen width
                int boxLeftMargin = (image.Width - boxWidth) / 2;  // Center horizontally
                int bottomCushion = BOTTOM_CUSHION;
                
                // Calculate how many boxes we can fit (no top buffer)
                int availableHeight = BOTTOM_LIMIT - TOP_LIMIT;
                int boxesCount = (availableHeight + (boxHeight + bottomCushion) - 1) / (boxHeight + bottomCushion);  // Ceiling division to include last box
                
                Console.WriteLine($"Drawing {boxesCount} boxes between {TOP_LIMIT} and {BOTTOM_LIMIT}");

                if (drawDebugLines)
                {
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
                            int boxY = TOP_LIMIT;  // Start directly at TOP_LIMIT

                            for (int i = 0; i < boxesCount; i++)
                            {
                                graphics.DrawRectangle(
                                    redPen,
                                    boxLeftMargin,  // X position
                                    boxY,          // Y position
                                    boxWidth,       // Width
                                    boxHeight       // Height
                                );

                                boxY += boxHeight + bottomCushion;
                            }
                        }
                    }
                }

                // Save the processed image
                image.Save(outputPath, ImageFormat.Png);
                
                return boxesCount;
            }
        }
    }
}
