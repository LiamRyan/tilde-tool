using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Hotcommand
{
   public class Hotcommand
   {
      public string Tag { get; set; }
      public string FileName { get; set; }
      public string? WorkingDirectory { get; set; }
      public string? Arguments { get; set; }
   }
}
