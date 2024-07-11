using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tildetool.Time.Serialization
{
   public class IndicatorValue
   {
      public string Icon { get; set; }
      public string Name { get; set; }
   }
   public class Indicator
   {
      public string Name { get; set; }
      public string Hotkey { get; set; }
      public IndicatorValue[] Values { get; set; }

      public int Offset => Values.Length / 2;
   }
}
