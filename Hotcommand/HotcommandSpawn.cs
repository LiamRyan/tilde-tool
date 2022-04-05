using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Hotcommand
{
   public class HotcommandSpawn
   {
      //
      public string FileName { get; set; }
      public string? WorkingDirectory { get; set; }
      public string? Arguments { get; set; }

      public int? WindowX { get; set; }
      public int? WindowY { get; set; }
   }
}
