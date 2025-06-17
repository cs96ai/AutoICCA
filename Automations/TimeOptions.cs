using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CountCheckBox
{
    public class TimeOptions
    {
        /// <summary>
        /// Scans an image for horizontal text lines, OCRs each line, and finds the line most closely matching the target text.
        /// Returns the (x, y) coordinates (center of the matched line) or (-1, -1) if not found.
        /// </summary>
        /// <param name="imagePath">Path to the image</param>
        /// <param name="targetText">Text to match (e.g. "length of stay")</param>
        /// <returns>Tuple of (x, y) coordinates or (-1, -1) if not found</returns>
        public (int x, int y) FindTextLineCoordinates(string imagePath)
        {
            using (Bitmap image = new Bitmap(imagePath))
            {
                // Convert to grayscale and binarize (black/white)
                Bitmap bwImage = this.BinarizeImage(image);

                // Find line bounds (top, bottom) for each line
                var lineBounds = this.FindTextLines(bwImage);

                // Prepare to draw debug image
                using (Bitmap debugImage = new Bitmap(image.Width, image.Height))
                {
                    using (Graphics g = Graphics.FromImage(debugImage))
                    {
                        g.DrawImage(image, 0, 0);
                        Pen linePen = new Pen(Color.Blue, 1);
                        Pen boxPen = new Pen(Color.Lime, 2); // Special color for 63px box

                        (int x, int y) found = (-1, -1);
                        foreach (var bounds in lineBounds)
                        {
                            // Draw horizontal bounds
                            g.DrawLine(linePen, 0, bounds.top, image.Width - 1, bounds.top);
                            g.DrawLine(linePen, 0, bounds.bottom, image.Width - 1, bounds.bottom);

                            // Find bounding box of black pixels in this line
                            int minX = bwImage.Width, maxX = -1;
                            for (int y = bounds.top; y <= bounds.bottom; y++)
                            {
                                for (int x = 0; x < bwImage.Width; x++)
                                {
                                    if (bwImage.GetPixel(x, y).R == 0)
                                    {
                                        if (x < minX) minX = x;
                                        if (x > maxX) maxX = x;
                                    }
                                }
                            }
                            if (maxX >= minX)
                            {
                                int width = maxX - minX + 1;
                                // Draw bounding box for this line
                                g.DrawRectangle(linePen, minX, bounds.top, width, bounds.bottom - bounds.top + 1);
                                // Draw the width as text to the right of the bounding box (30px spacer)
                                using (Font font = new Font("Arial", 12, FontStyle.Bold))
                                using (Brush brush = new SolidBrush(Color.Red))
                                {
                                    int textX = minX + width + 30;
                                    int textY = bounds.top + (bounds.bottom - bounds.top) / 2 - 8; // vertical center
                                    g.DrawString(width.ToString(), font, brush, textX, textY);
                                }
                                if (width == 68)
                                {
                                    // Highlight this box
                                    g.DrawRectangle(boxPen, minX, bounds.top, width, bounds.bottom - bounds.top + 1);
                                    int centerY = (bounds.top + bounds.bottom) / 2;
                                    int centerX = (minX + maxX) / 2;
                                    found = (centerX, centerY);
                                }
                            }
                        }
                        // Save debug image
                        string debugPath = Path.Combine(Path.GetDirectoryName(imagePath) ?? ".", "debug_lines.png");
                        debugImage.Save(debugPath, ImageFormat.Png);
                        return found;
                    }
                }
            }
        }

        // Helper: Binarize image (black/white)
        private Bitmap BinarizeImage(Bitmap src)
        {
            Bitmap bw = new Bitmap(src.Width, src.Height);
            for (int x = 0; x < src.Width; x++)
            {
                for (int y = 0; y < src.Height; y++)
                {
                    Color pixel = src.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.3 + pixel.G * 0.59 + pixel.B * 0.11);
                    bw.SetPixel(x, y, gray > 128 ? Color.White : Color.Black);
                }
            }
            return bw;
        }

        // Helper: Find text line bounds by scanning horizontally
        private List<(int top, int bottom)> FindTextLines(Bitmap bw)
        {
            List<(int top, int bottom)> lines = new List<(int, int)>();
            bool inText = false;
            int lineTop = 0;
            for (int y = 0; y < bw.Height; y++)
            {
                bool hasBlack = false;
                for (int x = 0; x < bw.Width; x++)
                {
                    if (bw.GetPixel(x, y).R == 0)
                    {
                        hasBlack = true;
                        break;
                    }
                }
                if (hasBlack && !inText)
                {
                    inText = true;
                    lineTop = y;
                }
                else if (!hasBlack && inText)
                {
                    inText = false;
                    int lineBottom = y - 1;
                    // Add some border/cushion
                    lines.Add((Math.Max(0, lineTop - 2), Math.Min(bw.Height - 1, lineBottom + 2)));
                }
            }
            // If image ends on a line
            if (inText)
                lines.Add((Math.Max(0, lineTop - 2), bw.Height - 1));
            return lines;
        }


        //top left x=833 y=364
        //width = 562 height 149

        /*
        i will pass in an image, 
        the image will have multiple lines of black text on a white background
        design an algoright that uses horizontal line scanning to seperate each line of text in to seperate images
        try and have the same top and bottom border in each sub image
        pass each image to micorsoft office document imaging (MODI) 
        oCR the text 
        lower case all the text
        use a fuzzy match algorithm to see which line of text most closely resemblems "length of stay"
        (i don't really care how incorrect the text is, but position of correct text matter, and overall score matters, and perhaps uniqueness matters as well)
        (I added some sample code at the bottom)

        and return the x y coordinates of where I should click the mouse if I want to click on image
        if no text was found return -1

        */
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

        public class TimeCheckResult
        {
            public bool ColorFound { get; set; }
            public int XCoordinate { get; set; }
            public int YCoordinate { get; set; }
        }

        public static TimeCheckResult CheckForBlueBarInTimeSelection(Bitmap screenshot)
        {
            // Define the area to check (top-left corner x=833, y=345, width=562, height=145)
            int startX = 833;
            int startY = 345;
            int width = 562;
            int height = 145;
            int endX = startX + width;
            int endY = startY + height;

            // Target color to find: #3399FF (RGB: 51, 153, 255)
            Color targetColor = Color.FromArgb(51, 153, 255);

            // Calculate the middle X coordinate of the box
            int middleX = startX + (width / 2);

            using (Graphics g = Graphics.FromImage(screenshot))
            {
                // Copy the original screenshot
                g.DrawImage(screenshot, 0, 0);

                // Draw the search area rectangle in cyan
                using (Pen cyanPen = new Pen(Color.Orange, 2))
                {
                    g.DrawRectangle(cyanPen, startX, startY, width, height);
                }

                // Draw text for search area coordinates
                using (Font font = new Font("Arial", 12))
                using (SolidBrush brush = new SolidBrush(Color.Orange))
                {
                    g.DrawString($"Search Area ({startX},{startY}) to ({endX},{endY})", font, brush, startX, startY - 20);
                    g.DrawString($"Width: {width}, Height: {height}", font, brush, startX, startY - 40);
                }

                // Start from the bottom of the box and move up checking each pixel for blue bar
                for (int y = endY - 1; y >= startY; y--)
                {
                    Color pixelColor = screenshot.GetPixel(middleX, y);
                    // Draw a small red dot for each pixel checked (search path)
                    g.FillRectangle(Brushes.Red, middleX - 1, y - 1, 3, 3);

                    if (pixelColor.R == targetColor.R && pixelColor.G == targetColor.G && pixelColor.B == targetColor.B)
                    {
                        // Draw a larger green circle around the found blue pixel
                        using (Pen greenPen = new Pen(Color.LimeGreen, 2))
                        {
                            g.DrawEllipse(greenPen, middleX - 10, y - 10, 20, 20);
                            g.DrawString("Blue Bar Found!", new Font("Arial", 12), new SolidBrush(Color.LimeGreen), middleX + 15, y - 10);
                        }

                        // Save the debug image
                        string debugPath = $".\\Debug\\debug_bluebar_found_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                        Directory.CreateDirectory(".\\Debug");
                        screenshot.Save(debugPath, ImageFormat.Png);
                        screenshot.Dispose();

                        // Color found, return true with no coordinates
                        return new TimeCheckResult { ColorFound = true, XCoordinate = 0, YCoordinate = 0 };
                    }
                }

                // If blue bar color is not found, look for text lines in the search area
                // Convert search area to grayscale and binarize (black/white)
                Bitmap searchArea = new Bitmap(width, height);
                using (Graphics areaG = Graphics.FromImage(searchArea))
                {
                    areaG.DrawImage(screenshot, 0, 0, new Rectangle(startX, startY, width, height), GraphicsUnit.Pixel);
                }
                Bitmap bwImage = new TimeOptions().BinarizeImage(searchArea);

                // Find text lines within the search area
                var lineBounds = new TimeOptions().FindTextLines(bwImage);
                int clickX = middleX;
                int clickY = endY - 1; // Default to bottom if no suitable line is found

                Pen linePen = new Pen(Color.Blue, 1);
                Pen targetPen = new Pen(Color.Magenta, 2);

                // Process each detected line
                foreach (var bounds in lineBounds)
                {
                    // Adjust bounds to original image coordinates
                    int adjustedTop = startY + bounds.top;
                    int adjustedBottom = startY + bounds.bottom;

                    // Draw horizontal bounds in debug image
                    g.DrawLine(linePen, startX, adjustedTop, endX - 1, adjustedTop);
                    g.DrawLine(linePen, startX, adjustedBottom, endX - 1, adjustedBottom);

                    // Find bounding box of black pixels in this line within search area
                    int minX = bwImage.Width, maxX = -1;
                    for (int y = bounds.top; y <= bounds.bottom; y++)
                    {
                        for (int x = 0; x < bwImage.Width; x++)
                        {
                            if (bwImage.GetPixel(x, y).R == 0)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                            }
                        }
                    }

                    if (maxX >= minX)
                    {
                        int lineWidth = maxX - minX + 1;
                        // Draw bounding box for this line
                        int adjustedMinX = startX + minX;
                        int adjustedMaxX = startX + maxX;
                        g.DrawRectangle(linePen, adjustedMinX, adjustedTop, lineWidth, bounds.bottom - bounds.top + 1);

                        // Draw the width as text to the right of the bounding box
                        using (Font widthFont = new Font("Arial", 12, FontStyle.Bold))
                        using (Brush widthBrush = new SolidBrush(Color.Red))
                        {
                            int textX = adjustedMaxX + 10;
                            int textY = adjustedTop + (adjustedBottom - adjustedTop) / 2 - 8; // vertical center
                            g.DrawString(lineWidth.ToString(), widthFont, widthBrush, textX, textY);
                        }

                        if (lineWidth == 68 || lineWidth == 67)
                        {
                            // Highlight this box as the target to click
                            g.DrawRectangle(targetPen, adjustedMinX, adjustedTop, lineWidth, bounds.bottom - bounds.top + 1);
                            clickY = (adjustedTop + adjustedBottom) / 2; // Center vertically
                            clickX = (adjustedMinX + adjustedMaxX) / 2; // Center horizontally
                            g.DrawEllipse(targetPen, clickX - 2, clickY - 2, 5, 5);
                            g.DrawString($"Target Click ({clickX},{clickY})", new Font("Arial", 12), new SolidBrush(Color.Magenta), clickX + 15, clickY - 10);
                        }
                    }
                }

                // Save the debug image
                string debugPathNotFound = $".\\Debug\\debug_bluebar_notfound_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                Directory.CreateDirectory(".\\Debug");
                screenshot.Save(debugPathNotFound, ImageFormat.Png);
                screenshot.Dispose();
                searchArea.Dispose();
                bwImage.Dispose();

                // If color is not found, return false with coordinates to click
                return new TimeCheckResult 
                { 
                    ColorFound = false, 
                    XCoordinate = clickX, 
                    YCoordinate = clickY 
                };
            }
        }
    }
}