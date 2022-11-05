using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Tildetool.Time.Serialization
{
   public class WeeklySchedule : ISchedule
   {
      public string Name { get; set; }
      public float HourBegin { get; set; }
      public float HourEnd { get; set; }
   }
}
