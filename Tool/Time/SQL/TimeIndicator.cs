using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Tildetool.Time.SQL
{
   public class TimeIndicator
   {
      public string Category;
      public double Value;
      public DateTime Time;  //utc

      public float Hour => (float)Time.ToLocalTime().TimeOfDay.TotalHours;

      public int Add()
      {
         using SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand();
         command.CommandText = "INSERT INTO time_indicator (category, value, time) VALUES ($category, $value, $time); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$category", Category);
         command.Parameters.AddWithValue("$value", Value);
         command.Parameters.AddWithValue("$time", Time);
         int rowId = Convert.ToInt32(command.ExecuteScalar());

         return rowId;
      }

      public static List<TimeIndicator> Select(DateTime minTimeLocal, DateTime maxTimeLocal)
      {
         using SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand();
         command.CommandText = "SELECT category,value,time FROM time_indicator WHERE time >= $minTime AND time <= $maxTime ORDER BY time;";
         command.Parameters.AddWithValue("$minTime", minTimeLocal);
         command.Parameters.AddWithValue("$maxTime", maxTimeLocal);

         List<TimeIndicator> result = new List<TimeIndicator>();
         using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
               string category = reader.GetString(0);
               double value = reader.GetDouble(1);
               DateTime time = reader.GetDateTime(2);
               result.Add(new TimeIndicator { Category = category, Value = value, Time = time });
            }

         return result;
      }

      public static void SelectMostRecent(out double[] values, out DateTime[] datesUtc)
      {
         Dictionary<int, int> idToIndex = new Dictionary<int, int>();
         using (SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand())
         {
            Dictionary<string, int> indicatorIndex = new Dictionary<string, int>();
            for (int i = 0; i < TimeManager.Instance.Indicators.Length; i++)
               indicatorIndex[TimeManager.Instance.Indicators[i].Name] = i;

            command.CommandText = "SELECT category,MAX(id) FROM time_indicator WHERE time >= (SELECT MAX(subsel.time) FROM time_indicator as subsel WHERE subsel.category = time_indicator.category) GROUP BY category;";
            using (var reader = command.ExecuteReader())
               while (reader.Read())
               {
                  string category = reader.GetString(0);
                  int id = reader.GetInt32(1);
                  if (indicatorIndex.TryGetValue(category, out int index))
                     idToIndex[id] = index;
               }
         }

         values = new double[TimeManager.Instance.Indicators.Length];
         datesUtc = new DateTime[TimeManager.Instance.Indicators.Length];
         for (int i = 0; i < values.Length; i++)
         {
            values[i] = double.MinValue;
            datesUtc[i] = DateTime.MinValue;
         }
         using (SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand())
         {
            int[] ids = idToIndex.Keys.ToArray();
            string idnames = string.Join(",", Enumerable.Range(0, ids.Length).Select(i => $"$id{i}"));
            command.CommandText = $"SELECT id,value,time FROM time_indicator WHERE id IN ({idnames});";
            for (int i = 0; i < ids.Length; i++)
               command.Parameters.AddWithValue($"$id{i}", ids[i]);

            using (var reader = command.ExecuteReader())
               while (reader.Read())
               {
                  int id = reader.GetInt32(0);
                  double value = reader.GetDouble(1);
                  DateTime date = reader.GetDateTime(2);
                  int index = idToIndex[id];
                  values[index] = value;
                  datesUtc[index] = date;
               }
         }
      }

      public static void SelectAdjacent(string category, DateTime minTimeLocal, DateTime maxTimeLocal, out double prevValue, out double nextValue)
      {
         int minId = -1;
         using (SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand())
         {
            command.CommandText = "SELECT MAX(id) FROM time_indicator WHERE category = $category AND time <= $minTime AND time >= (SELECT MAX(subsel.time) FROM time_indicator as subsel WHERE subsel.time <= $minTime AND subsel.category = $category);";
            command.Parameters.AddWithValue("category", category);
            command.Parameters.AddWithValue("minTime", minTimeLocal);
            using (var reader = command.ExecuteReader())
               while (reader.Read())
                  if (!reader.IsDBNull(0))
                     minId = reader.GetInt32(0);
         }

         int maxId = -1;
         using (SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand())
         {
            command.CommandText = "SELECT MIN(id) FROM time_indicator WHERE category = $category AND time >= $maxTime AND time <= (SELECT MIN(subsel.time) FROM time_indicator as subsel WHERE subsel.time >= $maxTime AND subsel.category = $category);";
            command.Parameters.AddWithValue("category", category);
            command.Parameters.AddWithValue("maxTime", maxTimeLocal);
            using (var reader = command.ExecuteReader())
               while (reader.Read())
                  if (!reader.IsDBNull(0))
                     maxId = reader.GetInt32(0);
         }

         prevValue = double.MinValue;
         nextValue = double.MinValue;
         using (SqliteCommand command = TimeManager.Instance.GetConn().CreateCommand())
         {
            command.CommandText = $"SELECT id,value FROM time_indicator WHERE id IN ($minId, $maxId);";
            command.Parameters.AddWithValue($"$minId", minId);
            command.Parameters.AddWithValue($"$maxId", maxId);

            using (var reader = command.ExecuteReader())
               while (reader.Read())
               {
                  int id = reader.GetInt32(0);
                  double value = reader.GetDouble(1);
                  if (id == minId)
                     prevValue = value;
                  else
                     nextValue = value;
               }
         }
      }
   }
}
