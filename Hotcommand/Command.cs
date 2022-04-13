using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Hotcommand
{
   public class Command
   {
      public string Tag { get; set; }

      public CommandSpawn[] Spawns { get; set; }
   }
}
