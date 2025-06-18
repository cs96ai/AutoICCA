using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace CountCheckBox
{
    public class TimeOptions
    {
       

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

      
        private List<(int x, int y)> FindLengthOfStay(Bitmap bw)
        {
            // Save the original image for comparison
            bw.Save(".\\Debug\\debug_original_input.png", ImageFormat.Png);
            Directory.CreateDirectory(".\\Debug");

            // Define the area of interest
            int startX = 835;
            int width = 558;
            int startY = 348;
            int height = 141;

            // Ensure the crop area is within the bounds of the original image
            if (startX + width > bw.Width || startY + height > bw.Height)
            {
                // Adjust dimensions if they exceed image bounds
                width = Math.Min(width, bw.Width - startX);
                height = Math.Min(height, bw.Height - startY);
            }

            // Crop the image to the area of interest
            Bitmap croppedImage = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(croppedImage))
            {
                g.DrawImage(bw, 0, 0, new Rectangle(startX, startY, width, height), GraphicsUnit.Pixel);
            }

            // Save the cropped image to verify
            croppedImage.Save(".\\Debug\\debug_test_bw.png", ImageFormat.Png);

            // Convert to black and white if not already
            Bitmap bwCropped = BinarizeImage(croppedImage);
            bwCropped.Save(".\\Debug\\debug_binarized_cropped.png", ImageFormat.Png);

            // Detect text lines by scanning from bottom to top
            List<(int top, int bottom)> lines = new List<(int, int)>();
            bool inText = false;
            int lineTop = 0;
            for (int y = height - 1; y >= 0; y--)
            {
                bool hasBlack = false;
                for (int x = 0; x < width; x++)
                {
                    if (bwCropped.GetPixel(x, y).R == 0)
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
                    int lineBottom = y + 1;
                    // Add some vertical cushion
                    lines.Add((Math.Max(0, lineBottom - 2), Math.Min(height - 1, lineTop + 2)));
                }
            }
            if (inText)
            {
                lines.Add((0, Math.Min(height - 1, lineTop + 2)));
            }

            // Create debug image with annotations, using original cropped image to avoid unwanted lines
            using (Bitmap debugImage = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(debugImage))
                {
                    g.DrawImage(croppedImage, 0, 0);

                    Pen linePen = new Pen(Color.Blue, 1);
                    Pen targetPen = new Pen(Color.Magenta, 2);
                    List<(int x, int y)> clickCoordinates = new List<(int x, int y)>();

                    foreach (var bounds in lines)
                    {
                        int adjustedTop = bounds.top;
                        int adjustedBottom = bounds.bottom;

                        // Draw horizontal lines for top and bottom of text line
                        g.DrawLine(linePen, 0, adjustedTop, width - 1, adjustedTop);
                        g.DrawLine(linePen, 0, adjustedBottom, width - 1, adjustedBottom);

                        // Find min and max X for the line to calculate width
                        int minX = width, maxX = -1;
                        for (int y = bounds.top; y <= bounds.bottom; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                if (bwCropped.GetPixel(x, y).R == 0)
                                {
                                    if (x < minX) minX = x;
                                    if (x > maxX) maxX = x;
                                }
                            }
                        }

                        if (maxX >= minX)
                        {
                            int lineWidth = maxX - minX + 1;
                            int lineHeight = adjustedBottom - adjustedTop + 1;

                            // Draw bounding box around the text line
                            g.DrawRectangle(linePen, minX, adjustedTop, lineWidth, lineHeight);

                            // Annotate width of the line
                            using (Font widthFont = new Font("Arial", 12, FontStyle.Bold))
                            using (Brush widthBrush = new SolidBrush(Color.Red))
                            {
                                int textX = maxX + 10;
                                int textY = adjustedTop + (lineHeight) / 2 - 8;
                                g.DrawString(lineWidth.ToString(), widthFont, widthBrush, textX, textY);
                            }

                            // Check if width matches target (67 or 68 pixels)
                            if (lineWidth == 67 || lineWidth == 68)
                            {
                                g.DrawRectangle(targetPen, minX, adjustedTop, lineWidth, lineHeight);
                                int clickX = startX + (minX + maxX) / 2;
                                int clickY = startY + (adjustedTop + adjustedBottom) / 2;
                                clickCoordinates.Add((clickX, clickY));
                                g.DrawEllipse(targetPen, (minX + maxX) / 2 - 5, (adjustedTop + adjustedBottom) / 2 - 5, 10, 10);
                                g.DrawString($"Target Click ({clickX},{clickY})", new Font("Arial", 12), new SolidBrush(Color.Magenta), (minX + maxX) / 2 + 15, (adjustedTop + adjustedBottom) / 2 - 10);
                            }
                        }
                    }

                    // Save debug image with annotations
                    debugImage.Save(".\\Debug\\debug_findlengthofstay_annotated.png", ImageFormat.Png);
                    return clickCoordinates;
                }
            }
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


       

        public class TimeCheckResult
        {
            public bool ColorFound { get; set; }
            public int XCoordinate { get; set; }
            public int YCoordinate { get; set; }
        }

        public TimeCheckResult CheckForBlueBarInTimeSelection(Bitmap ss)
        {
            // Make a deep copy of the input bitmap
            Bitmap screenShot = new Bitmap(ss.Width, ss.Height);
            using (Graphics g = Graphics.FromImage(screenShot))
            {
                g.DrawImage(ss, 0, 0);
            }

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

            using (Graphics g = Graphics.FromImage(screenShot))
            {
                // Copy the original screenshot
                g.DrawImage(screenShot, 0, 0);

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
                    Color pixelColor = screenShot.GetPixel(middleX, y);
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
                        screenShot.Save(debugPath, ImageFormat.Png);
                        screenShot.Dispose();

                        // Color found, return true with no coordinates
                        return new TimeCheckResult { ColorFound = true, XCoordinate = 0, YCoordinate = 0 };
                    }
                }

                // If blue bar color is not found, look for line that has "Length of Stay" and return x y
              
                var lengthOfStay = new TimeOptions().FindLengthOfStay(ss);
                int clickX = middleX;
                int clickY = endY - 1;

                if (lengthOfStay.Count > 0)
                {
                    // Assuming the first result is the target for now. Adjust logic if needed.
                    clickX = lengthOfStay[0].x;
                    clickY = lengthOfStay[0].y;
                }

                // Save the debug image
                string debugPathNotFound = $".\\Debug\\debug_bluebar_notfound_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                Directory.CreateDirectory(".\\Debug");
                screenShot.Save(debugPathNotFound, ImageFormat.Png);
                screenShot.Dispose();
//                searchArea.Dispose();
                //bwImage.Dispose();

                // Return coordinates if blue bar is not found
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