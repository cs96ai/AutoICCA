using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ICCAAutoDotNet9
{
    public class MouseStep
    {
        [JsonPropertyName("xpos")]
        public int XPos { get; set; }

        [JsonPropertyName("ypos")]
        public int YPos { get; set; }

        [JsonPropertyName("instruction")]
        public string Instruction { get; set; } = string.Empty;

        [JsonPropertyName("pre_click_delay")]
        public int PreClickDelay { get; set; } = 500;  // Default 500ms delay before clicking

        [JsonPropertyName("post_click_delay")]
        public int PostClickDelay { get; set; } = 2000;  // Default 2000ms delay after clicking

        [JsonPropertyName("input_delay")]
        public int InputDelay { get; set; } = 500;  // Default 500ms delay between input actions

        // Maintain compatibility with existing code if needed
        public int StepNumber { get; set; } = 0;
    }
}
