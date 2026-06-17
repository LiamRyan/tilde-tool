using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Time.Serialization
{
   public class Command
   {
      public string FileName { get; set; }
      public string[] ArgumentList { get; set; }
      public string? WorkingDirectory { get; set; }
   }

   public class TimeDataBundle
   {
      public Command OpenDatabase { get; set; }
      public Project[] Project { get; set; }
      public Indicator[] Indicator { get; set; }
      public WeeklyDay WeeklyDay { get; set; }
   }
}
