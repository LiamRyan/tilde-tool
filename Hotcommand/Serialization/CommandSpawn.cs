using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Hotcommand.Serialization
{
   public class CommandSpawn
   {
      // Spawn Arguments
      public string ShellOpen { get; set; }
      public string FileName { get; set; }
      public string? WorkingDirectory { get; set; }
      public string? Arguments { get; set; }
      public string[] ArgumentList { get; set; }
      public bool AsAdmin { get; set; }

      // Order
      public float PauseSec { get; set; }

      // Window Parameters
      public int? WindowX { get; set; }
      public int? WindowY { get; set; }
      public int? WindowW { get; set; }
      public int? WindowH { get; set; }
      public int? Monitor { get; set; }
      public string? VirtualDesktop { get; set; }
      public bool Maximize { get; set; } = false;
      public bool Minimize { get; set; } = false;
      public bool HasWindowParameter => (WindowX != null && WindowY != null) || (WindowW != null && WindowH != null) || Monitor != null || !string.IsNullOrEmpty(VirtualDesktop) || Maximize || Minimize;
   }
}
