using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BertUI
{
    class Launcher
    {
        public string Name { get; set; }
        public string LaunchPath { get; set; }
        public string ImagePath { get; set; }
        public string Args { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }

        public Launcher(string Name = "Launcher", string LaunchPath = null, string Args = null, string ImagePath = null) 
        {
            this.Name = Name;
            this.LaunchPath = LaunchPath;
            this.ImagePath = ImagePath;
            this.Args = Args;
        }
    }
}
