using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tildetool.Time.Serialization
{
   public class Project
   {
      public string Name { get; set; }
      public string Hotkey { get; set; }
      public string Ident { get; set; }
      public bool ShowOnIndicator { get; set; }
      public string[] DesktopPrevent { get; set; }

      [JsonIgnore]
      public int TimeTodaySec { get; set; }
   }
}
