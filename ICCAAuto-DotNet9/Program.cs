using System;
using System.Collections.Generic;
using System;
using System.Drawing;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;

using System.Text.Json.Serialization;
using System.Text.Json;
using System.Windows.Forms;
using CountCheckBox;

namespace ICCAAutoDotNet9
{
    class Program
    {
        private static int reportsGenerated = 0; // Counter for number of reports generated

        // Log file paths
        private static readonly string StepLogPath = "StepLog.txt";
        private static readonly string PatientLogPath = "PatientLog.txt";
        private static readonly string LastPatientFile = "LastPatient.txt";
        private static readonly string MrnFilePath = "MRN.txt";
        private static readonly string StepsFolder = "steps";

        // List to store MRNs
        private static List<string> mrnList = new List<string>();
        private static int currentMrnIndex = 0;

        // Import user32.dll for mouse control
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting ICCA Automation...");
            Console.WriteLine("Waiting 5 seconds before beginning processing...");
            Console.WriteLine("Press 'Q' at any time to quit the application.");
            Thread.Sleep(5000);
            LogStep("Application started");

            // Start a background thread to listen for 'Q' key press
            Thread keyListenerThread = new Thread(KeyListener);
            keyListenerThread.IsBackground = true;
            keyListenerThread.Start();

            // Read MRN list from file
            ReadMrnList();

            // Get starting index based on last processed patient
            GetStartingIndex();

            // Process each patient
            while (currentMrnIndex < mrnList.Count)
            {
                string currentMrn = mrnList[currentMrnIndex];
                Console.WriteLine($"Processing MRN: {currentMrn}");
                LogPatient($"Started processing MRN: {currentMrn}");
                LogStep($"Started processing MRN: {currentMrn}");

                // Execute all steps for current patient
                if (!ProcessPatientViaXYPosition(currentMrn))
                {
                    LogStep($"Failed to process MRN: {currentMrn}. Stopping application.");
                    LogPatient($"Failed to process MRN: {currentMrn}");
                    Console.WriteLine($"Failed to process MRN: {currentMrn}. Stopping application.");
                    break;
                }

                // Save the last successfully processed MRN
                File.WriteAllText(LastPatientFile, currentMrn);
                LogPatient($"Completed processing MRN: {currentMrn}");
                currentMrnIndex++;
            }

            Console.WriteLine("Automation completed.");
            LogStep("Application completed");
        }

        static void ReadMrnList()
        {
            try
            {
                if (File.Exists(MrnFilePath))
                {
                    mrnList = File.ReadAllLines(MrnFilePath)
                        .Where(mrn => !string.IsNullOrWhiteSpace(mrn))
                        .Distinct()
                        .ToList();
                    LogStep("Successfully read MRN file");
                    Console.WriteLine($"Read {mrnList.Count} MRNs from file.");
                }
                else
                {
                    LogStep("MRN file not found");
                    Console.WriteLine("MRN file not found. Exiting.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                LogStep($"Error reading MRN file: {ex.Message}");
                Console.WriteLine($"Error reading MRN file: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static void GetStartingIndex()
        {
            try
            {
                if (File.Exists(LastPatientFile))
                {
                    string lastMrn = File.ReadAllText(LastPatientFile).Trim();
                    currentMrnIndex = mrnList.FindIndex(m => m == lastMrn);
                    if (currentMrnIndex >= 0)
                    {
                        currentMrnIndex++; // Start with next patient
                        LogStep($"Resuming from after MRN: {lastMrn}");
                        Console.WriteLine($"Resuming from after MRN: {lastMrn}");
                    }
                    else
                    {
                        currentMrnIndex = 0;
                        LogStep("Last processed MRN not found in list, starting from beginning");
                    }
                }
                else
                {
                    currentMrnIndex = 0;
                    LogStep("No last patient record found, starting from beginning");
                }
            }
            catch (Exception ex)
            {
                LogStep($"Error getting starting index: {ex.Message}");
                currentMrnIndex = 0;
            }
        }

        static bool ProcessPatientViaImages(string mrn)
        {
            // Get list of step image files from Steps directory, sorted by filename
            string[] stepImagePaths = Directory.GetFiles(StepsFolder, "*.png")
                                         .OrderBy(f => f)
                                         .ToArray();

            // Create list of Step objects
            List<Step> steps = stepImagePaths.Select(path => new Step(path)).ToList();

            int stepNumber = 1;

            foreach (Step step in steps)
            {
                LogStep($"Starting step {stepNumber} ({step.Name}) for MRN: {mrn}");
                Console.WriteLine($"Executing step {stepNumber} ({step.Name}) for MRN: {mrn}");

                if (!ExecuteStep(step.ImagePath, mrn, step.Name))
                {
                    LogStep($"Failed to complete step {stepNumber} ({step.Name}) for MRN: {mrn} after 5 attempts");
                    Console.WriteLine($"Failed to complete step {stepNumber} ({step.Name}) for MRN: {mrn} after 5 attempts");
                    return false;
                }

                LogStep($"Completed step {stepNumber} ({step.Name}) for MRN: {mrn}");
                stepNumber++;
            }

            return true;
        }

        static bool ProcessPatientViaXYPosition(string mrn)
        {
            
            string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locations.json");
            if (!File.Exists(jsonFilePath))
            {
                LogStep($"JSON file not found: {jsonFilePath}");
                Console.WriteLine($"JSON file not found: {jsonFilePath}");
                return false;
            }

            string jsonContent = File.ReadAllText(jsonFilePath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                LogStep("JSON file is empty");
                Console.WriteLine("JSON file is empty");
                return false;
            }

            try
            {
                var steps = JsonSerializer.Deserialize<List<MouseStep>>(jsonContent);
                if (steps == null || steps.Count == 0)
                {
                    LogStep("No steps found in JSON file");
                    Console.WriteLine("No steps found in JSON file");
                    return false;
                }

                int stepNumber = 1;
                foreach (var step in steps.OrderBy(s => s.StepNumber))
                {
                    LogStep($"Executing step {stepNumber} at X={step.XPos}, Y={step.YPos} for MRN: {mrn}");
                    Console.WriteLine($"Executing step {stepNumber} at X={step.XPos}, Y={step.YPos} for MRN: {mrn}");

                    // Always use the leftmost monitor
                    Screen targetScreen = Screen.AllScreens.OrderBy(s => s.Bounds.X).FirstOrDefault() ?? Screen.PrimaryScreen;
                    LogStep("Using leftmost monitor for click operation");

                    // Calculate absolute position
                    int absoluteX = targetScreen.Bounds.X + step.XPos;
                    int absoluteY = targetScreen.Bounds.Y + step.YPos;

                    // Move mouse and click with configurable delays
                    SetCursorPos(absoluteX, absoluteY);
                    Thread.Sleep(step.PreClickDelay); // Configurable delay before clicking
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                    // Handle different input steps
                    if (step.Instruction?.ToLower() == "click mrn")
                    {
                        Thread.Sleep(step.InputDelay); // Wait for click to register
                        SendKeys.SendWait(mrn);
                        Thread.Sleep(step.InputDelay); // Wait for input
                        SendKeys.SendWait("{ENTER}");
                        Thread.Sleep(step.PostClickDelay); // Wait for processing
                    }
                    else if (step.Instruction?.ToLower() == "click auth reason")
                    {
                        Thread.Sleep(step.InputDelay); // Wait for click to register
                        SendKeys.SendWait("Export Report");
                        Thread.Sleep(step.InputDelay); // Wait for input
                        SendKeys.SendWait("{ENTER}");
                        Thread.Sleep(step.PostClickDelay); // Wait for processing
                    }
                    else if (step.Instruction?.ToLower() == "click next for all reports")
                    {
                        reportsGenerated=1;
                        const int maxAttempts = 10;
                        int attempts = 0;
                        bool exitLoop = false;

                        // loop through all the reports by clicking next
                        while (!exitLoop && attempts < maxAttempts)
                        {
                            attempts++;
                            LogStep($"Report navigation attempt {attempts} of {maxAttempts} for MRN: {mrn}");

                            // Step 1: Check check to see if the default reporting period is selected
                            
                            
                            var timeCheckResult = TimeOptions.CheckForBlueBarInTimeSelection(CaptureScreenshot());

                            if (!timeCheckResult.ColorFound)
                            {
                                LogStep($"Time slot color not found, clicking at ({timeCheckResult.XCoordinate}, {timeCheckResult.YCoordinate})");
                                SetCursorPos(timeCheckResult.XCoordinate , timeCheckResult.YCoordinate);
                                Thread.Sleep(step.InputDelay);
                                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                                Thread.Sleep(step.PostClickDelay);
                            }
                            else
                            {
                                LogStep("Time slot color found, proceeding to next step.");
                            }

                            // Step 2: Check and click second coordinate (next button) if not light gray (#F0F0F0)
                            Color color2 = GetPixelColor(1242, 785);
                            bool isF0F0F0 = color2.R == 0xF0 && color2.G == 0xF0 && color2.B == 0xF0; // #F0F0F0
                            bool isDDDDDD = color2.R == 0xDD && color2.G == 0xDD && color2.B == 0xDD; // #DDDDDD
                            bool isDEDCDC = color2.R == 0xDE && color2.G == 0xDC && color2.B == 0xDC; // #DEDCDC
                            bool isBEE6FD = color2.R == 0xBE && color2.G == 0xE6 && color2.B == 0xFD; // #BEE6FD

                            string colorHex = $"#{color2.R:X2}{color2.G:X2}{color2.B:X2}";
                            LogStep($"Detected color at next button (1242, 785): {colorHex}");
                            
                            if ((isDDDDDD || isDEDCDC || isBEE6FD) && !isF0F0F0)
                            {
                                SetCursorPos(targetScreen.Bounds.X + 1242, targetScreen.Bounds.Y + 785);
                                Thread.Sleep(step.InputDelay);
                                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                                Thread.Sleep(step.PostClickDelay);
                                reportsGenerated++; // Increment counter when we successfully click next
                                LogStep($"Successfully generated report {reportsGenerated} for MRN: {mrn}");
                            }

                            // Step 3: Check exit condition  (if the print button is dark gray or the next button is (f4f4f4-light gray) we're done)
                            Color color3 = GetPixelColor(1326, 786);
                            if (color3.R == 0xDD && color3.G == 0xDD && color3.B == 0xDD) // Is #DDDDDD
                            {
                                reportsGenerated++;
                                SetCursorPos(targetScreen.Bounds.X + 1326, targetScreen.Bounds.Y + 786);
                                Thread.Sleep(step.InputDelay);
                                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                                Thread.Sleep(step.PostClickDelay);
                                exitLoop = true; // Exit condition met
                            }

                            if (!exitLoop)
                            {
                                Thread.Sleep(500); // Wait a second before next attempt
                            }
                        }

                        if (!exitLoop)
                        {
                            string errorMsg = $"Failed to complete report navigation after {maxAttempts} attempts for MRN: {mrn}. Stopping application.";
                            LogStep(errorMsg);
                            Console.WriteLine(errorMsg);
                            Environment.Exit(1);
                        }
                    }
                    else if (step.Instruction?.ToLower() == "box count screen shot")
                    {
                        // Always use the leftmost monitor
                        Screen targetScreen2 = Screen.AllScreens.OrderBy(s => s.Bounds.X).FirstOrDefault() ?? Screen.PrimaryScreen;

                        // Define screenshot region relative to leftmost monitor
                        int x = targetScreen2.Bounds.X + 724;  // Add monitor offset to x coordinate
                        int y = 407;  // Y coordinate remains the same
                        int width = 1193 - 724;
                        int height = 763 - 407;

                        LogStep($"Taking screenshot from leftmost monitor at coordinates: ({x}, {y}) with size {width}x{height}");

                        // Take screenshot of the specified region
                        string screenshotPath = Path.Combine(Path.GetTempPath(), "temp_screenshot.png");
                        using (Bitmap bitmap = new Bitmap(width, height))
                        {
                            using (Graphics g = Graphics.FromImage(bitmap))
                            {
                                g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
                            }
                            bitmap.Save(screenshotPath);
                        }

                        LogStep($"Screenshot saved to: {screenshotPath}");

                        // Process the screenshot and count boxes
                        var boxCounter = new BoxCounter();
                        string outputPath = Path.Combine(Path.GetTempPath(), "processed_screenshot.png");
                        reportsGenerated = boxCounter.ProcessImageAndCountBoxes(screenshotPath, outputPath);

                        LogStep($"Box count completed. Found {reportsGenerated} boxes.");
                    }

                    else if (step.Instruction?.ToLower() == "click filename for all reports")
                    {
                        // Create export directory if it doesn't exist
                        string exportDir = @"C:\ICCAExports\";
                        if (!Directory.Exists(exportDir))
                        {
                            LogStep($"Creating export directory: {exportDir}");
                            Directory.CreateDirectory(exportDir);
                        }

                        // Process each report
                        for (int reportNum = 0; reportNum <= reportsGenerated; reportNum++)
                        {
                            LogStep($"Processing file {reportNum} of {reportsGenerated} for MRN: {mrn}");

                            // Wait for the white background (color #FFFFFF)
                            const int maxColorChecks = 20;
                            int colorChecks = 0;
                            bool isWhite = false;

                            while (!isWhite && colorChecks < maxColorChecks)
                            {
                                colorChecks++;
                                Color bgColor = GetPixelColor(183, 436);
                                isWhite = bgColor.R == 0xFF && bgColor.G == 0xFF && bgColor.B == 0xFF; // #FFFFFF

                                if (!isWhite)
                                {
                                    LogStep($"Waiting for white background, attempt {colorChecks} of {maxColorChecks}");
                                    Thread.Sleep(2000); // Wait 2 seconds between checks
                                }
                            }

                            if (!isWhite)
                            {
                                string errorMsg = $"Failed to detect white background after {maxColorChecks} attempts for MRN: {mrn}, report {reportNum}. Stopping application.";
                                LogStep(errorMsg);
                                Console.WriteLine(errorMsg);
                                Environment.Exit(1);
                            }

                            // Background is white, proceed with file save
                            //string fileName = Path.Combine(exportDir, $"{mrn}_{reportNum}");
                            string fileName = mrn + "_" + reportNum;
                            LogStep($"Saving report {reportNum} to: {fileName}");

                            Thread.Sleep(step.InputDelay);
                            SendKeys.SendWait(fileName);
                            Thread.Sleep(step.InputDelay);
                            SendKeys.SendWait("{ENTER}");
                            Thread.Sleep(step.PostClickDelay);
                        }
                    }

                  
                    else if (step.Instruction?.ToLower() == "done")
                    {
                        // Click at the specified coordinates
                        SetCursorPos(targetScreen.Bounds.X + step.XPos, targetScreen.Bounds.Y + step.YPos);
                        Thread.Sleep(step.InputDelay);
                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        Thread.Sleep(step.PostClickDelay);

                        // Log the total reports generated and reset counter
                        LogStep($"Completed processing {reportsGenerated} reports for MRN: {mrn}");
                        reportsGenerated = 0;
                    }
                   
                    // Capture screenshot and mark the click location
                    Bitmap screenshot = CaptureScreenshot();
                    using (Graphics g = Graphics.FromImage(screenshot))
                    {
                        // Draw click location dot
                        using (SolidBrush redBrush = new SolidBrush(Color.Red))
                        {
                            int dotSize = 20;
                            g.FillEllipse(redBrush, step.XPos - dotSize / 2, step.YPos - dotSize / 2, dotSize, dotSize);
                        }

                        // Draw step name in top right corner with outline
                        using (Font boldFont = new Font("Arial", 16, FontStyle.Bold))
                        {
                            string stepText = $"Step {stepNumber}: {step.Instruction}";
                            SizeF textSize = g.MeasureString(stepText, boldFont);
                            float x = screenshot.Width - textSize.Width - 20; // 20px padding from right
                            float y = 20; // 20px from top

                            // Draw black outline
                            using (SolidBrush blackBrush = new SolidBrush(Color.Black))
                            {
                                // Draw text offset in 8 directions
                                float offset = 1.0f;
                                g.DrawString(stepText, boldFont, blackBrush, x - offset, y);
                                g.DrawString(stepText, boldFont, blackBrush, x + offset, y);
                                g.DrawString(stepText, boldFont, blackBrush, x, y - offset);
                                g.DrawString(stepText, boldFont, blackBrush, x, y + offset);
                                g.DrawString(stepText, boldFont, blackBrush, x - offset, y - offset);
                                g.DrawString(stepText, boldFont, blackBrush, x + offset, y - offset);
                                g.DrawString(stepText, boldFont, blackBrush, x - offset, y + offset);
                                g.DrawString(stepText, boldFont, blackBrush, x + offset, y + offset);
                            }

                            // Draw red text on top
                            using (SolidBrush redBrush = new SolidBrush(Color.Red))
                            {
                                g.DrawString(stepText, boldFont, redBrush, x, y);
                            }
                        }
                    }
                    SaveImage(screenshot, $"Step_{stepNumber}_Click", "screenshot_with_click");
                    screenshot.Dispose();

                    LogStep($"Completed step {stepNumber} for MRN: {mrn}");
                    stepNumber++;
                    Thread.Sleep(2000); // Delay between steps to mimic human interaction
                }

                return true;
            }
            catch (Exception ex)
            {
                LogStep($"Error processing JSON file: {ex.Message}");
                Console.WriteLine($"Error processing JSON file: {ex.Message}");
                return false;
            }
        }

        static bool ExecuteStep(string imagePath, string mrn, string stepName)
        {
            if (!File.Exists(imagePath))
            {
                LogStep($"Image not found for step {stepName}: {imagePath}");
                Console.WriteLine($"Image not found for step {stepName}");
                return false;
            }

            Bitmap template = (Bitmap)Image.FromFile(imagePath);
            int attempts = 0;
            int maxAttempts = 5;

            // Save template image for reference
            SaveImage(template, stepName, "template");

            while (attempts < maxAttempts)
            {
                attempts++;
                LogStep($"Attempt {attempts} to find image for step {stepName}");
                Console.WriteLine($"Attempt {attempts} for step {stepName}");

                // Capture screenshot
                Bitmap screenshot = CaptureScreenshot();

                // Save screenshot for reference
                SaveImage(screenshot, stepName, "screenshot");

                // Test all template matching modes
                //TestTemplateMatchingModes();

                // Find template in screenshot
                System.Drawing.Point? location = null; // Temporarily set to null as we're not using template matching

                if (location.HasValue)
                {
                    // Draw a red box around the identified template location
                    using (Graphics g = Graphics.FromImage(screenshot))
                    {
                        using (Pen redPen = new Pen(Color.Red, 3))
                        {
                            g.DrawRectangle(redPen, location.Value.X, location.Value.Y, template.Width, template.Height);
                        }
                    }

                    // Calculate center point
                    int centerX = location.Value.X + template.Width / 2;
                    int centerY = location.Value.Y + template.Height / 2;

                    // Move mouse and click
                    SetCursorPos(centerX, centerY);
                    // mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                    //Thread.Sleep(300);
                    //mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);

                    LogStep($"Successfully clicked at ({centerX}, {centerY}) for step {stepName}");
                    Console.WriteLine($"Successfully completed step {stepName}");

                    // Save screenshot with red box after successful match
                    SaveImage(screenshot, stepName + "_match", "screenshot", location.Value.X, location.Value.Y, template.Width, template.Height);
                    screenshot.Dispose();
                    template.Dispose();
                    Thread.Sleep(1000); // Wait after click
                    return true;
                }

                screenshot.Dispose();
                LogStep($"Image not found in attempt {attempts} for step {stepName}");
                Thread.Sleep(1000); // Wait 1 second before next attempt
            }

            template.Dispose();
            return false;
        }

        

        static void SaveImage(Bitmap image, string stepName, string imageType, int boxX = -1, int boxY = -1, int boxWidth = 0, int boxHeight = 0)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string filename = $"_{imageType}_{timestamp}_{stepName.Replace(" ", "_")}.png";
                string logDir = ".\\Log";
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                string filepath = Path.Combine(logDir, filename);

                // If coordinates are provided, draw a box
                if (boxX >= 0 && boxY >= 0 && boxWidth > 0 && boxHeight > 0)
                {
                    using (Bitmap imageCopy = new Bitmap(image))
                    {
                        using (Graphics g = Graphics.FromImage(imageCopy))
                        {
                            using (Pen redPen = new Pen(Color.Red, 3))
                            {
                                g.DrawRectangle(redPen, boxX, boxY, boxWidth, boxHeight);
                            }
                        }
                        imageCopy.Save(filepath, ImageFormat.Png);
                    }
                }
                else
                {
                    image.Save(filepath, ImageFormat.Png);
                }
                LogStep($"Saved {imageType} image: {filepath}");
            }
            catch (Exception ex)
            {
                LogStep($"Error saving {imageType} image for {stepName}: {ex.Message}");
                Console.WriteLine($"Error saving {imageType} image: {ex.Message}");
            }
        }

        static Bitmap CaptureScreenshot()
        {
            try
            {
                // Always use the leftmost monitor
                var targetScreen = Screen.AllScreens.OrderBy(s => s.Bounds.X).First();
                LogStep($"Capturing screenshot from leftmost monitor: {targetScreen.DeviceName}");

                int width = targetScreen.Bounds.Width;
                int height = targetScreen.Bounds.Height;
                Bitmap screenshot = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(screenshot))
                {
                    g.CopyFromScreen(targetScreen.Bounds.X, targetScreen.Bounds.Y, 0, 0, new Size(width, height));
                }
                string screenshotPath = SaveScreenshot(screenshot, "capture");
                LogStep($"Screenshot saved to: {screenshotPath}");
                return screenshot;
            }
            catch (Exception ex)
            {
                LogStep($"Error capturing screenshot: {ex.Message}");
                throw;
            }
        }

        static string SaveScreenshot(Bitmap screenshot, string screenshotType)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string filename = $"{screenshotType}_{timestamp}.png";
                string logDir = ".\\Log";
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                string filepath = Path.Combine(logDir, filename);
                screenshot.Save(filepath, ImageFormat.Png);
                return filepath;
            }
            catch (Exception ex)
            {
                LogStep($"Error saving screenshot: {ex.Message}");
                throw;
            }
        }

        

        static void LogStep(string message)
        {
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
            File.AppendAllText(StepLogPath, logMessage);
        }

        static void LogPatient(string message)
        {
            string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n";
            File.AppendAllText(PatientLogPath, logMessage);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        static Color GetPixelColor(int x, int y)
        {
            // Always use the leftmost monitor
            Screen targetScreen = Screen.AllScreens.OrderBy(s => s.Bounds.X).FirstOrDefault() ?? Screen.PrimaryScreen;
            
            // Adjust coordinates for leftmost monitor
            int absoluteX = targetScreen.Bounds.X + x;
            int absoluteY = targetScreen.Bounds.Y + y;
            
            LogStep($"Getting color from leftmost monitor at ({x}, {y}) -> ({absoluteX}, {absoluteY})");
            
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                uint pixel = GetPixel(hdc, absoluteX, absoluteY);
                Color color = Color.FromArgb(
                    (int)(pixel & 0x000000FF),         // Red
                    (int)(pixel & 0x0000FF00) >> 8,   // Green
                    (int)(pixel & 0x00FF0000) >> 16); // Blue
                return color;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }

        static void KeyListener()
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine("'Q' key pressed. Terminating application.");
                        LogStep("Application terminated by user pressing 'Q' key");
                        Environment.Exit(0);
                    }
                }
                Thread.Sleep(100); // Prevent high CPU usage
            }
        }
    }

   
}
