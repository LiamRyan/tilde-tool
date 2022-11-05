using System;

namespace Tildetool.Time.Serialization
{
   public class TimePeriod
   {
      [NonSerialized]
      public long DbId;
      public string Ident { get; set; }
      public DateTime StartTime { get; set; }
      public DateTime EndTime { get; set; }
   }
}
