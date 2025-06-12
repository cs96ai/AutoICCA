using System;
using System.IO;

namespace ICCAAutoDotNet9
{
    class Step
    {
        public string ImagePath { get; set; }
        public string Name { get; set; }

        public Step(string imagePath)
        {
            ImagePath = imagePath;
            Name = Path.GetFileNameWithoutExtension(imagePath);
        }
    }
}
