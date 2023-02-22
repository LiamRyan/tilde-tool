using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Hotcommand.Serialization
{
   public enum ColorIndex
   {
      Background = 0,
      TextFore,
      TextBack,
      Glow
   }
   public class Context
   {
      public string Name { get; set; }
      public uint[] Colors { get; set; } = new uint[4] { 0x002720, 0x8ff8e0, 0x009d7f, 0x4fffdf };

      public Command[] Hotcommand { get; set; }

      public QuickTag[] QuickTag { get; set; }
   }
}
