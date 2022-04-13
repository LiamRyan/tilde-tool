using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Hotcommand
{
   public class Context
   {
      public string Name { get; set; }

      public Command[] Hotcommand { get; set; }

      public QuickTag[] QuickTag { get; set; }
   }
}
