using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace MousePosition
{
    internal class Program
    {
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Point lpPoint);
        
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        private static Label _coordLabel;
        private static Label _colorLabel;
        private static PictureBox _colorSample;
        private static List<MouseStep> _steps = new List<MouseStep>();
        private static int _stepCount = 0;
        private static readonly string _jsonFilePath = "MouseSteps.json";

        static void Main(string[] args)
        {
            Console.WriteLine("Mouse Position Tracker - Press Ctrl+C to exit, Press 'C' to capture position");

            // Create a form to display the mouse position
            Form form = new Form();
            form.Text = "Mouse Position";
            form.FormBorderStyle = FormBorderStyle.None;
            form.BackColor = Color.Black;
            form.TransparencyKey = Color.Black;
            form.TopMost = true;

            // Get the leftmost monitor
            Screen leftmostScreen = Screen.AllScreens.OrderBy(s => s.Bounds.X).FirstOrDefault() ?? Screen.PrimaryScreen;
            
            // Set form position to top right of leftmost monitor
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(leftmostScreen.Bounds.Right - 300, leftmostScreen.Bounds.Top + 10);
            form.Size = new Size(290, 150);

            // Create label for displaying coordinates
            _coordLabel = new Label();
            _coordLabel.AutoSize = true;
            _coordLabel.Font = new Font("Arial", 24, FontStyle.Bold);
            _coordLabel.ForeColor = Color.Red;
            _coordLabel.Location = new Point(10, 10);
            form.Controls.Add(_coordLabel);
            
            // Create label for displaying color
            _colorLabel = new Label();
            _colorLabel.AutoSize = true;
            _colorLabel.Font = new Font("Arial", 18, FontStyle.Bold);
            _colorLabel.ForeColor = Color.Red;
            _colorLabel.Location = new Point(10, 50);
            form.Controls.Add(_colorLabel);
            
            // Create picture box for color sample
            _colorSample = new PictureBox();
            _colorSample.Size = new Size(50, 50);
            _colorSample.Location = new Point(220, 10);
            _colorSample.BorderStyle = BorderStyle.FixedSingle;
            form.Controls.Add(_colorSample);

            // Set up key press event
            form.KeyPress += Form_KeyPress;
            form.KeyPreview = true;

            // Timer to update mouse position
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 100; // Update every 100ms
            timer.Tick += (sender, e) =>
            {
                Point cursorPos;
                if (GetCursorPos(out cursorPos))
                {
                    // Find which screen the cursor is on
                    Screen currentScreen = Screen.AllScreens.FirstOrDefault(s => s.Bounds.Contains(cursorPos)) ?? Screen.PrimaryScreen;
                    
                    // Calculate relative coordinates
                    int relativeX = cursorPos.X - currentScreen.Bounds.X;
                    int relativeY = cursorPos.Y - currentScreen.Bounds.Y;
                    
                    // Get color at cursor position
                    Color pixelColor = GetColorAt(cursorPos);
                    string hexColor = $"#{pixelColor.R:X2}{pixelColor.G:X2}{pixelColor.B:X2}";
                    
                    _coordLabel.Text = $"X: {relativeX}, Y: {relativeY}";
                    _colorLabel.Text = $"Color: {hexColor}";
                    _colorSample.BackColor = pixelColor;
                    
                    _coordLabel.Refresh();
                    _colorLabel.Refresh();
                    _colorSample.Refresh();
                }
            };
            timer.Start();

            // Load existing steps if file exists
            LoadSteps();

            Application.Run(form);
        }
        
        private static Color GetColorAt(Point point)
        {
            using (Bitmap screenPixel = new Bitmap(1, 1))
            using (Graphics gdest = Graphics.FromImage(screenPixel))
            {
                using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                {
                    IntPtr hSrcDC = gsrc.GetHdc();
                    IntPtr hDC = gdest.GetHdc();
                    int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, point.X, point.Y, (int)CopyPixelOperation.SourceCopy);
                    gdest.ReleaseHdc();
                    gsrc.ReleaseHdc();
                }
                
                return screenPixel.GetPixel(0, 0);
            }
        }

        private static void Form_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 'c' || e.KeyChar == 'C')
            {
                Point cursorPos;
                if (GetCursorPos(out cursorPos))
                {
                    Screen currentScreen = Screen.AllScreens.FirstOrDefault(s => s.Bounds.Contains(cursorPos)) ?? Screen.PrimaryScreen;
                    int relativeX = cursorPos.X - currentScreen.Bounds.X;
                    int relativeY = cursorPos.Y - currentScreen.Bounds.Y;
                    
                    _stepCount++;
                    var step = new MouseStep { StepNumber = _stepCount, X = relativeX, Y = relativeY, MonitorIndex = Array.IndexOf(Screen.AllScreens.OrderBy(s => s.Bounds.X).ToArray(), currentScreen) };
                    _steps.Add(step);
                    
                    // Save to JSON file
                    SaveSteps();
                    
                    Console.WriteLine($"Captured Step {_stepCount}: X={relativeX}, Y={relativeY} on Monitor {step.MonitorIndex}");
                    MessageBox.Show($"Captured Step {_stepCount}: X={relativeX}, Y={relativeY}", "Position Captured", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                e.Handled = true;
            }
        }

        private static void LoadSteps()
        {
            if (File.Exists(_jsonFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_jsonFilePath);
                    _steps = JsonSerializer.Deserialize<List<MouseStep>>(json) ?? new List<MouseStep>();
                    _stepCount = _steps.Count > 0 ? _steps.Max(s => s.StepNumber) : 0;
                    Console.WriteLine($"Loaded {_steps.Count} existing steps from {_jsonFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading steps: {ex.Message}");
                    _steps = new List<MouseStep>();
                    _stepCount = 0;
                }
            }
        }

        private static void SaveSteps()
        {
            try
            {
                string json = JsonSerializer.Serialize(_steps, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_jsonFilePath, json);
                Console.WriteLine($"Saved {_steps.Count} steps to {_jsonFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving steps: {ex.Message}");
            }
        }
    }

    public class MouseStep
    {
        public int StepNumber { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int MonitorIndex { get; set; }
    }
}
