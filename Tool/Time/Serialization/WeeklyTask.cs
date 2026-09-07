using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Time.Serialization
{
   public class WeeklyTask
   {
      public DayTask[] Sun { get; set; }
      public DayTask[] Mon { get; set; }
      public DayTask[] Tue { get; set; }
      public DayTask[] Wed { get; set; }
      public DayTask[] Thu { get; set; }
      public DayTask[] Fri { get; set; }
      public DayTask[] Sat { get; set; }
   }
}
