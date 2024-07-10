using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Time.Serialization
{
    public class WeeklyDay
    {
      public WeeklySchedule[] Sun { get; set; }
      public WeeklySchedule[] Mon { get; set; }
      public WeeklySchedule[] Tue { get; set; }
      public WeeklySchedule[] Wed { get; set; }
      public WeeklySchedule[] Thu { get; set; }
      public WeeklySchedule[] Fri { get; set; }
      public WeeklySchedule[] Sat { get; set; }
   }
}
