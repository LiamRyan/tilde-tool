using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Hotcommand.Serialization
{
   public class HotcommandData
   {
      public string DictionaryURL { get; set; }

      public Context[] Context { get; set; }

      public Command[] Hotcommand { get; set; }

      public QuickTag[] QuickTag { get; set; }
   }
}
