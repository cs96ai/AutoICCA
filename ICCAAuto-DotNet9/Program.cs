using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Flann;
using OpenCvSharp.Extensions;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace ICCAAutoDotNet9
{
    class Program
    {
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
                    mrnList = new List<string>(File.ReadAllLines(MrnFilePath));
                    mrnList.RemoveAll(string.IsNullOrWhiteSpace);
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

                    // Move mouse and click
                    SetCursorPos(absoluteX, absoluteY);
                    Thread.Sleep(500); // Small delay to ensure movement
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    LogStep($"Clicked at X={step.XPos}, Y={step.YPos} on leftmost monitor");

                    // Capture screenshot and mark the click location
                    Bitmap screenshot = CaptureScreenshot();
                    using (Graphics g = Graphics.FromImage(screenshot))
                    {
                        using (SolidBrush brush = new SolidBrush(Color.Red))
                        {
                            int dotSize = 20;
                            g.FillEllipse(brush, step.XPos - dotSize / 2, step.YPos - dotSize / 2, dotSize, dotSize);
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

        static System.Drawing.Point? FindTemplateInScreenshot(Bitmap screenshot, Bitmap template, string imagePath = "")
        {
            // Convert Bitmap to Mat for OpenCvSharp processing
            Mat screenshotMat = BitmapToMat(screenshot);
            Mat templateMat = BitmapToMat(template);

            try
            {
                // Convert images to grayscale
                Mat screenshotGray = new Mat();
                Mat templateGray = new Mat();
                Cv2.CvtColor(screenshotMat, screenshotGray, ColorConversionCodes.BGRA2GRAY);
                Cv2.CvtColor(templateMat, templateGray, ColorConversionCodes.BGRA2GRAY);

                Mat searchAreaGray = screenshotGray;

                // If searching for 01-FindPatient.png, restrict to top-left quarter
                if (imagePath.EndsWith("01-FindPatient.png", StringComparison.OrdinalIgnoreCase))
                {
                    int quarterWidth = screenshotGray.Cols / 2;
                    int quarterHeight = screenshotGray.Rows / 2;
                    searchAreaGray = screenshotGray.SubMat(0, quarterHeight, 0, quarterWidth);
                    LogStep("Restricting search to top-left quarter for 01-FindPatient.png");
                    Console.WriteLine("Restricting search to top-left quarter for 01-FindPatient.png");
                }

                // Check if template is inside screenshot
                using (var result = new Mat(searchAreaGray.Rows - templateGray.Rows + 1, searchAreaGray.Cols - templateGray.Cols + 1, MatType.CV_32FC1))
                {
                    //this should really be calibrated instead of hard coded
                    Cv2.MatchTemplate(searchAreaGray, templateGray, result, TemplateMatchModes.CCoeffNormed);

                    // Find maximum value
                    double value;
                    OpenCvSharp.Point maxLoc;
                    Cv2.MinMaxLoc(result, out value, out _, out maxLoc, out _);

                    LogStep($"Template matching value: {value}");
                    Console.WriteLine($"Template matching value: {value}");

                    // Adjust location if we searched in a sub-area
                    if (imagePath.EndsWith("01-FindPatient.png", StringComparison.OrdinalIgnoreCase))
                    {
                        LogStep($"Match location in top-left quarter: ({maxLoc.X}, {maxLoc.Y})");
                    }

                    // if (value > 0.9) // TODO: adjust threshold
                    // {
                    // Return the top left corner of the template in screenshot coordinates
                    return new System.Drawing.Point(maxLoc.X, maxLoc.Y);
                    // }
                    //return null;
                }
            }
            catch (Exception ex)
            {
                LogStep($"Error in template matching: {ex.Message}");
                Console.WriteLine($"Error in template matching: {ex.Message}");
                return null;
            }
            finally
            {
                // Ensure proper disposal of Mat objects
                screenshotMat.Dispose();
                templateMat.Dispose();
            }
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
                // Detect all available screens
                Screen[] screens = Screen.AllScreens;
                Screen selectedScreen;

                if (screens.Length == 1)
                {
                    selectedScreen = screens[0];
                    LogStep("Capturing screenshot from primary (only) monitor");
                }
                else
                {
                    selectedScreen = screens.OrderBy(s => s.Bounds.X).First();
                    LogStep("Capturing screenshot from leftmost monitor");
                }

                int width = selectedScreen.Bounds.Width;
                int height = selectedScreen.Bounds.Height;
                int startX = selectedScreen.Bounds.X;
                int startY = selectedScreen.Bounds.Y;

                Bitmap screenshot = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(screenshot))
                {
                    g.CopyFromScreen(startX, startY, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
                }
                return screenshot;
            }
            catch (Exception ex)
            {
                LogStep($"Error capturing screenshot: {ex.Message}");
                Console.WriteLine($"Error capturing screenshot: {ex.Message}");
                // Fallback mechanism - capture primary screen with default dimensions
                try
                {
                    Bitmap fallbackScreenshot = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(fallbackScreenshot))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(1920, 1080), CopyPixelOperation.SourceCopy);
                    }
                    LogStep("Fallback screenshot capture successful");
                    return fallbackScreenshot;
                }
                catch (Exception fallbackEx)
                {
                    LogStep($"Fallback screenshot capture failed: {fallbackEx.Message}");
                    throw new Exception("Failed to capture screenshot even with fallback", fallbackEx);
                }
            }
        }

        static Mat BitmapToMat(Bitmap bitmap)
        {
            try
            {
                var imageMat = OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap);

                LogStep("Successfully converted Bitmap to Mat using BitmapConverter");
                return imageMat;
            }
            catch (Exception ex)
            {
                LogStep($"Error converting Bitmap to Mat: {ex.Message}");
                Console.WriteLine($"Error converting Bitmap to Mat: {ex.Message}");
                throw new Exception("Failed to convert Bitmap to Mat", ex);
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

    public class MouseStep
    {
        [JsonPropertyName("xpos")]
        public int XPos { get; set; }

        [JsonPropertyName("ypos")]
        public int YPos { get; set; }

        [JsonPropertyName("instruction")]
        public string Instruction { get; set; } = string.Empty;

        // Maintain compatibility with existing code if needed
        public int StepNumber { get; set; } = 0;
    }
}
