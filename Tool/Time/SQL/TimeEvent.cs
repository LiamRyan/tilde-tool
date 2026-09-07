using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Tildetool.Time.SQL
{
   public class TimeEvent : ISchedule
   {
      public string Description;
      public DateTime StartTime;  //local
      public DateTime EndTime;  //local

      public string Name => Description;
      public float HourBegin => (float)StartTime.ToLocalTime().TimeOfDay.TotalHours;
      public float HourEnd => (float)EndTime.ToLocalTime().TimeOfDay.TotalHours;

      public int Add()
      {
         using SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand();
         command.CommandText = "INSERT INTO time_event (description, start_time, end_time) VALUES ($description, $startTime, $endTime); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$description", Description);
         command.Parameters.AddWithValue("$startTime", StartTime);
         command.Parameters.AddWithValue("endTime", EndTime);
         int rowId = Convert.ToInt32(command.ExecuteScalar());

         return rowId;
      }

      public static List<TimeEvent> Select(DateTime minTimeLocal, DateTime maxTimeLocal)
      {
         using SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand();
         command.CommandText = "SELECT description,start_time,end_time FROM time_event WHERE end_time >= $minTime AND start_time <= $maxTime;";
         command.Parameters.AddWithValue("$minTime", minTimeLocal);
         command.Parameters.AddWithValue("$maxTime", maxTimeLocal);

         List<TimeEvent> result = new List<TimeEvent>();
         using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
               string desc = reader.GetString(0);
               DateTime startTime = reader.GetDateTime(1).ToUniversalTime();
               DateTime endTime = reader.GetDateTime(2).ToUniversalTime();
               result.Add(new TimeEvent { Description = desc, StartTime = startTime, EndTime = endTime });
            }

         return result;
      }
   }
}
