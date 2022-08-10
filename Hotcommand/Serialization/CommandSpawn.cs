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
      //
      public string ShellOpen { get; set; }
      public string FileName { get; set; }
      public string? WorkingDirectory { get; set; }
      public string? Arguments { get; set; }
      public string[] ArgumentList { get; set; }

      public int? WindowX { get; set; }
      public int? WindowY { get; set; }
      public int? WindowW { get; set; }
      public int? WindowH { get; set; }
   }
}
