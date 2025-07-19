using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Tildetool.Time.Serialization;

namespace Tildetool.Time
{
   public interface ISchedule
   {
      public string Name { get; }
      public float HourBegin { get; }
      public float HourEnd { get; }
   }
   public class TimeEvent : ISchedule
   {
      public string Description;
      public DateTime StartTime;  //local
      public DateTime EndTime;  //local

      public string Name => Description;
      public float HourBegin => (float)StartTime.ToLocalTime().TimeOfDay.TotalHours;
      public float HourEnd => (float)EndTime.ToLocalTime().TimeOfDay.TotalHours;
   }
   public class TimeIndicator
   {
      public string Category;
      public double Value;
      public DateTime Time;  //utc
      public float Hour => (float)Time.ToLocalTime().TimeOfDay.TotalHours;
   }

   public class TimeManager
   {
      #region Singleton

      private static TimeManager _Instance;
      public static TimeManager Instance => _Instance ?? (_Instance = new TimeManager());

      #endregion Singleton
      #region Variables

      public Indicator[] Indicators;
      public Dictionary<string, Indicator> IndicatorByCategory;
      public Dictionary<string, Indicator> IndicatorByHotkey;

      public Indicator GetIndicator(string category)
      {
         return IndicatorByCategory.GetValueOrDefault(category);
      }

      public IndicatorValue GetIndicatorValue(string category, double value)
      {
         if (IndicatorByCategory.TryGetValue(category, out Indicator indicator))
            return indicator.GetValue(value);
         return null;
      }

      public bool TryGetIndicatorValue(string category, double value, out Indicator indicator, out IndicatorValue valueCls)
      {
         if (!IndicatorByCategory.TryGetValue(category, out indicator))
         {
            valueCls = null;
            return false;
         }
         valueCls = indicator.GetValue(value);
         return valueCls != null;
      }

      // Raw data
      public static Project IdleProject = new Project { Hotkey = "0", Name = "Idle", Ident = "Idle" };
      public Project[] Data;

      // Processed results
      public Dictionary<string, Project> HotkeyToProject;
      public Dictionary<string, Project> IdentToProject;
      public WeeklySchedule[][] ScheduleByDayOfWeek = Enumerable.Range(0, 7).Select(s => new WeeklySchedule[0]).ToArray();

      // State
      public Project? CurrentProject;
      public int CurrentTimePeriod = -1;
      public DateTime CurrentStartTime;
      public Project PausedProject;

      #endregion
      #region Active Project

      Timer _Timer;
      public void StartTick()
      {
         if (_Timer != null)
            return;
         _Timer = new Timer { Interval = 60000 };
         _Timer.Elapsed += (o, e) => { UpdateCurrentTimePeriod(); };
         _Timer.Start();

         SetProject(IdleProject);

         SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
      }

      public void SetProject(Project? project)
      {
         if (project == CurrentProject)
            return;

         // Increment the time of the old project by our value.
         bool result = SaveCache();
         if (!result)
            SaveCacheLater();

         UpdateCurrentTimePeriod();
         if (CurrentProject != null)
            CurrentProject.TimeTodaySec += (int)(DateTime.UtcNow - CurrentStartTime).TotalSeconds;

         // Switch to the new.
         CurrentProject = project;
         CurrentStartTime = DateTime.UtcNow;
         CurrentTimePeriod = -1;
      }

      public void AlterProject(Project project)
      {
         if (project == CurrentProject)
            return;

         // Increment the time of the old project by our value.
         bool result = SaveCache();
         if (!result)
            SaveCacheLater();

         // Force-switch to the new project and update the database accordingly.
         CurrentProject = project;
         UpdateCurrentTimePeriod();
      }

      private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
      {
         bool result;
         switch (e.Reason)
         {
            case SessionSwitchReason.SessionLock:
               App.WriteLog("Screen locked at " + DateTime.UtcNow.ToString() + (CurrentProject != null ? (", pausing " + CurrentProject.Name) : ""));
               PausedProject = CurrentProject;
               SetProject(null);
               break;

            case SessionSwitchReason.SessionUnlock:
               App.WriteLog("Screen unlocked at " + DateTime.UtcNow.ToString() + (PausedProject != null ? (", resuming " + PausedProject.Name) : ""));
               SetProject(PausedProject);
               break;
         }
      }

      #endregion
      #region Serialize

      public bool LoadCache()
      {
         Data = new Project[0];

         // Read from the file.
         bool result = false;
         TimeDataBundle cacheData;
         if (File.Exists("TimekeepCache.json"))
         {
            try
            {
               string jsonString = File.ReadAllText("TimekeepCache.json");
               cacheData = JsonSerializer.Deserialize<TimeDataBundle>(jsonString)!;
               result = true;
            }
            catch (Exception ex)
            {
               MessageBox.Show(ex.ToString());
               App.WriteLog(ex.Message);
               cacheData = new TimeDataBundle();
            }
         }
         else
         {
            // Initial run, populate some basic data.
            cacheData = new TimeDataBundle();
            cacheData.Project = new Project[1] { new Project { Ident = "Sample", Name = "Sample Project", Hotkey = "S", DesktopPrevent = new string[] { "Open" } } };
            cacheData.Indicator = new Indicator[1] { new Indicator { Hotkey = "P", Name = "Progress", Values = new IndicatorValue[] { new IndicatorValue() { Icon = "|", Name = "Average" } } } };
            SaveCache();
         }

         // Process it.
         if (cacheData.Project != null)
            Data = cacheData.Project.Append(IdleProject).ToArray();
         HotkeyToProject = Data.ToDictionary(p => p.Hotkey);
         IdentToProject = Data.ToDictionary(p => p.Ident);

         Indicators = (cacheData.Indicator ?? new Indicator[0]).ToArray();
         IndicatorByCategory = Indicators.ToDictionary(k => k.Name);
         IndicatorByHotkey = Indicators.ToDictionary(k => k.Hotkey);

         if (cacheData.WeeklyDay != null)
         {
            ScheduleByDayOfWeek[(int)DayOfWeek.Sunday] = cacheData.WeeklyDay.Sun ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Monday] = cacheData.WeeklyDay.Mon ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Tuesday] = cacheData.WeeklyDay.Tue ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Wednesday] = cacheData.WeeklyDay.Wed ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Thursday] = cacheData.WeeklyDay.Thu ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Friday] = cacheData.WeeklyDay.Fri ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Saturday] = cacheData.WeeklyDay.Sat ?? new WeeklySchedule[0];
         }

         RefreshTodayTime();

         return result;
      }
      public bool SaveCache()
      {
         return true;

         // Pull from the dictionary to data.
         TimeDataBundle cacheData = new TimeDataBundle();
         cacheData.Project = Data.Where(d => d != IdleProject).ToArray();
         cacheData.WeeklyDay = new WeeklyDay
         {
            Sun = ScheduleByDayOfWeek[(int)DayOfWeek.Sunday],
            Mon = ScheduleByDayOfWeek[(int)DayOfWeek.Monday],
            Tue = ScheduleByDayOfWeek[(int)DayOfWeek.Tuesday],
            Wed = ScheduleByDayOfWeek[(int)DayOfWeek.Wednesday],
            Thu = ScheduleByDayOfWeek[(int)DayOfWeek.Thursday],
            Fri = ScheduleByDayOfWeek[(int)DayOfWeek.Friday],
            Sat = ScheduleByDayOfWeek[(int)DayOfWeek.Saturday]
         };

         // Write to file.
         bool result = false;
         try
         {
            string jsonString = JsonSerializer.Serialize<TimeDataBundle>(cacheData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("TimekeepCache.json", jsonString);
            result = true;
         }
         catch (Exception ex)
         {
            MessageBox.Show(ex.ToString());
            App.WriteLog(ex.Message);
         }
         return result;
      }

      Timer? SaveTimer = null;
      public void SaveCacheLater()
      {
         if (SaveTimer != null)
            return;
         SaveTimer = new Timer();
         SaveTimer.Interval = 2000;
         SaveTimer.Elapsed += (s, e) =>
         {
            SaveTimer.Stop();
            bool result = SaveCache();
            if (!result)
            {
               SaveTimer.Interval = 1000;
               SaveTimer.Start();
            }
            else
            {
               SaveTimer.Dispose();
               SaveTimer = null;
            }
         };
         SaveTimer.Start();
      }

      #endregion
      #region SQLite

      void Query(string commandText)
      {
         using (SqliteCommand command = _Sqlite.CreateCommand())
         {
            command.CommandText = commandText;
            command.ExecuteNonQuery();
         }
      }
      SqliteConnection _Sqlite;
      public Dictionary<string, int> ProjectIdentToId = new();
      public Dictionary<string, string> ProjectIdentToCategory = new();
      public Dictionary<string, int> ProjectIdentToOrder = new();
      public List<string> ProjectIdentAutoSuggest = new();
      public void ConnectSqlite()
      {
         if (_Sqlite != null)
            return;
         _Sqlite = new SqliteConnection(new SqliteConnectionStringBuilder() { Mode = SqliteOpenMode.ReadWriteCreate, DataSource = "TimekeepHistory.db" }.ToString());
         _Sqlite.Open();

         // Create and populate the project table.
         SqliteCommand command;
         command = _Sqlite.CreateCommand();
         command.CommandText = "CREATE TABLE IF NOT EXISTS \"project\" ( \"id\"\tINTEGER, \"ident\" TEXT UNIQUE, \"category\" INTEGER, \"sort_order\" INTEGER NOT NULL DEFAULT 0, \"auto_suggest\" INTEGER NOT NULL DEFAULT 1, PRIMARY KEY(\"id\" AUTOINCREMENT) );";
         command.ExecuteNonQuery();
         command.Dispose();

         command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT ident FROM project;";
         HashSet<string> idents = new HashSet<string>();
         using (var reader = command.ExecuteReader())
            while (reader.Read())
               idents.Add(reader.GetString(0));
         command.Dispose();

         foreach (Project data in Data)
            if (!idents.Contains(data.Ident))
            {
               command = _Sqlite.CreateCommand();
               command.CommandText = "INSERT OR IGNORE INTO project (ident) VALUES ($ident);";
               command.Parameters.AddWithValue("$ident", data.Ident);
               command.ExecuteNonQuery();
               command.Dispose();
            }

         // Read an ident to id mapping.
         UpdateProjectData();

         // Create the tables.
         Query("CREATE TABLE IF NOT EXISTS \"time_period\" ( \"id\" INTEGER, \"project_id\" INTEGER, \"start_time\" TEXT, \"end_time\" TEXT, \"on_computer\" INTEGER DEFAULT 0, \"notes\" TEXT, PRIMARY KEY(\"id\" AUTOINCREMENT) );");
         Query("CREATE TABLE IF NOT EXISTS \"time_event\" ( \"id\" INTEGER, \"description\" TEXT, \"start_time\" TEXT, \"end_time\" TEXT, \"notes\" TEXT, PRIMARY KEY(\"id\" AUTOINCREMENT) );");
         Query("CREATE TABLE IF NOT EXISTS \"time_indicator\" ( \"id\" INTEGER, \"category\" TEXT, \"value\" REAL, \"time\" TEXT, \"notes\" TEXT, PRIMARY KEY(\"id\" AUTOINCREMENT) );");

         // Create the indices.
         Query("CREATE INDEX IF NOT EXISTS \"time_event_start_time\" ON \"time_event\" ( \"start_time\" ASC );");
         Query("CREATE INDEX IF NOT EXISTS \"time_event_end_time\" ON \"time_event\" ( \"end_time\" ASC );");
         Query("CREATE INDEX IF NOT EXISTS \"time_period_start_time\" ON \"time_period\" ( \"start_time\" ASC );");
         Query("CREATE INDEX IF NOT EXISTS \"time_period_end_time\" ON \"time_period\" ( \"end_time\" ASC );");
         Query("CREATE INDEX IF NOT EXISTS \"time_indicator_time\" ON \"time_indicator\" ( \"time\" ASC );");

         //
         RefreshTodayTime();
      }

      public void UpdateProjectData()
      {
         ProjectIdentToId.Clear();
         ProjectIdentToOrder.Clear();
         ProjectIdentToCategory.Clear();
         ProjectIdentAutoSuggest.Clear();

         var command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT ident, id, category, sort_order, auto_suggest FROM project;";
         Dictionary<int, string> idToIdent = new();
         Dictionary<string, int> identToCategoryId = new();
         using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
               string ident = reader.GetString(0);
               int id = reader.GetInt32(1);
               ProjectIdentToId[ident] = id;
               idToIdent[id] = ident;
               if (!reader.IsDBNull(2))
                  identToCategoryId[ident] = reader.GetInt32(2);
               ProjectIdentToOrder[ident] = reader.GetInt32(3);
               if (reader.GetInt32(4) > 0)
                  ProjectIdentAutoSuggest.Add(ident);
            }
         foreach (var kv in identToCategoryId)
            ProjectIdentToCategory[kv.Key] = idToIdent.GetValueOrDefault(kv.Value, null);
         command.Dispose();
      }

      public int AddProject(string ident)
      {
         if (ProjectIdentToId.TryGetValue(ident, out int projectId))
            return projectId;

         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "INSERT INTO project (ident) VALUES ($ident); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$ident", ident);
         var result = command.ExecuteScalar();
         command.Dispose();

         int value = Convert.ToInt32(result);
         ProjectIdentToId[ident] = value;
         return value;
      }

      public int AddHistoryLine(TimePeriod period)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "INSERT INTO time_period (project_id, start_time, end_time, on_computer) VALUES ($project_id, $start_time, $end_time, $on_computer); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$project_id", ProjectIdentToId[period.Ident]);
         command.Parameters.AddWithValue("$start_time", period.StartTime);
         command.Parameters.AddWithValue("$end_time", period.EndTime);
         command.Parameters.AddWithValue("$on_computer", period.OnComputer);
         int rowId = Convert.ToInt32(command.ExecuteScalar());
         command.Dispose();

         return rowId;
      }

      void UpdateHistoryLine(int id, TimePeriod period)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "UPDATE time_period SET project_id = $project_id, start_time = $start, end_time = $end WHERE id = $id;";
         command.Parameters.AddWithValue("$id", id);
         command.Parameters.AddWithValue("$project_id", ProjectIdentToId[period.Ident]);
         command.Parameters.AddWithValue("$start", period.StartTime);
         command.Parameters.AddWithValue("$end", period.EndTime);
         command.ExecuteNonQuery();
         command.Dispose();
      }

      void RemoveHistoryLine(int id)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "DELETE FROM time_period WHERE id = $id;";
         command.Parameters.AddWithValue("$id", id);
         command.ExecuteNonQuery();
         command.Dispose();
      }

      void UpdateCurrentTimePeriod()
      {
         // If we have no project, nothing to do.
         if (CurrentProject == null)
         {
            CurrentTimePeriod = -1;
            return;
         }
         // If it is too short right now, don't add (or remove if necessary)
         if ((DateTime.UtcNow - CurrentStartTime).TotalMinutes < 1.0f)
         {
            if (CurrentTimePeriod != -1)
               RemoveHistoryLine(CurrentTimePeriod);
            CurrentTimePeriod = -1;
            return;
         }

         // Either add or update.
         if (CurrentTimePeriod == -1)
            CurrentTimePeriod = AddHistoryLine(new TimePeriod { Ident = CurrentProject.Ident, StartTime = CurrentStartTime, EndTime = DateTime.UtcNow, OnComputer = true });
         else
            UpdateHistoryLine(CurrentTimePeriod, new TimePeriod { Ident = CurrentProject.Ident, StartTime = CurrentStartTime, EndTime = DateTime.UtcNow });
      }

      void RefreshTodayTime()
      {
         if (_Sqlite == null || Data == null)
            return;

         SqliteCommand command = _Sqlite.CreateCommand();
         DateTime today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day).ToUniversalTime();
         command.CommandText = "SELECT ident,SUM(julianday(end_time) - julianday(start_time)) FROM time_period INNER JOIN project ON project.id = project_id WHERE start_time >= $today GROUP BY project_id;";
         command.Parameters.AddWithValue("$today", today);
         using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
               string ident = reader.GetString(0);
               int seconds = (int)(reader.GetFloat(1) * 24 * 60 * 60);
               if (IdentToProject.TryGetValue(ident, out Project project))
                  project.TimeTodaySec = seconds;
            }
         command.Dispose();
      }

      public List<TimePeriod> QueryTimePeriod(DateTime minTimeUtc, DateTime maxTimeUtc)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT time_period.id,project.ident,start_time,end_time,on_computer FROM time_period INNER JOIN project ON project.id = project_id WHERE end_time >= $minTime AND start_time <= $maxTime;";
         command.Parameters.AddWithValue("$minTime", minTimeUtc);
         command.Parameters.AddWithValue("$maxTime", maxTimeUtc);

         List<TimePeriod> result = new List<TimePeriod>();
         using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
               long dbid = reader.GetInt64(0);
               string projectIdent = reader.GetString(1);
               DateTime startTime = reader.GetDateTime(2);
               DateTime endTime = reader.GetDateTime(3);
               bool onComputer = reader.GetBoolean(4);
               result.Add(new TimePeriod { DbId = dbid, Ident = projectIdent, StartTime = startTime, EndTime = endTime, OnComputer = onComputer });
            }
         command.Dispose();

         return result;
      }

      public List<TimePeriod> QueryTimePeriod(Project project, DateTime minTimeUtc, DateTime maxTimeUtc)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT time_period.id,start_time,end_time,on_computer FROM time_period INNER JOIN project ON project.id = project_id WHERE end_time >= $minTime AND start_time <= $maxTime AND project.ident = $ident;";
         command.Parameters.AddWithValue("$ident", project.Ident);
         command.Parameters.AddWithValue("$minTime", minTimeUtc);
         command.Parameters.AddWithValue("$maxTime", maxTimeUtc);

         List<TimePeriod> result = new List<TimePeriod>();
         using (var reader = command.ExecuteReader())
            while (reader.Read())
            {
               long dbid = reader.GetInt64(0);
               DateTime startTime = reader.GetDateTime(1);
               DateTime endTime = reader.GetDateTime(2);
               bool onComputer = reader.GetBoolean(3);
               result.Add(new TimePeriod { DbId = dbid, Ident = project.Ident, StartTime = startTime, EndTime = endTime, OnComputer = onComputer });
            }
         command.Dispose();

         return result;
      }

      public double QueryNightLength(DateTime beforeDay)
      {
         DateTime cutoff = new DateTime(beforeDay.Year, beforeDay.Month, beforeDay.Day, 3, 0, 0);
         DateTime cutoffUtc = cutoff.ToUniversalTime();
         //DateTime? earliest = projectPeriods.SelectMany(p => p).Select<TimeBlock, DateTime?>(p => p.StartTime)
         //   .Where(p => p?.ToLocalTime().TimeOfDay >= cutoff).DefaultIfEmpty(null).Min();
         //
         //if (earliest == null)
         //   return -1.0;
         //
         //List<TimeBlock> preperiods = TimeManager.Instance.QueryTimePeriod(earliest.Value.AddHours(-24), earliest.Value).Select(p => TimeBlock.FromTimePeriod(p)).ToList();
         //DateTime? latest = preperiods.Select<TimeBlock, DateTime?>(p => p.EndTime).Where(p => p < earliest).DefaultIfEmpty(null).Max();
         //if (latest == null)
         //   return -1.0;

         DateTime earliest;
         using (SqliteCommand command = _Sqlite.CreateCommand())
         {
            command.CommandText = "SELECT MIN(start_time) FROM time_period WHERE start_time >= $cutoff;";
            command.Parameters.AddWithValue("$cutoff", cutoffUtc);
            using (var reader = command.ExecuteReader())
            {
               if (!reader.Read())
                  return -1.0;
               if (reader.IsDBNull(0))
                  return -1.0;
               earliest = reader.GetDateTime(0);
            }
         }

         DateTime latest;
         using (SqliteCommand command = _Sqlite.CreateCommand())
         {
            command.CommandText = "SELECT MAX(end_time) FROM time_period WHERE end_time < $cutoff;";
            command.Parameters.AddWithValue("$cutoff", earliest);
            using (var reader = command.ExecuteReader())
            {
               if (!reader.Read())
                  return -1.0;
               if (reader.IsDBNull(0))
                  return -1.0;
               latest = reader.GetDateTime(0);
            }
         }

         return (earliest - latest).TotalHours;
      }

      public int AddTimeEvent(TimeEvent evt)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "INSERT INTO time_event (description, start_time, end_time) VALUES ($description, $startTime, $endTime); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$description", evt.Description);
         command.Parameters.AddWithValue("$startTime", evt.StartTime);
         command.Parameters.AddWithValue("endTime", evt.EndTime);
         int rowId = Convert.ToInt32(command.ExecuteScalar());
         command.Dispose();

         return rowId;
      }

      public List<TimeEvent> QueryTimeEvent(DateTime minTimeLocal, DateTime maxTimeLocal)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
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
         command.Dispose();

         return result;
      }

      public int AddTimeIndicator(TimeIndicator indicator)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "INSERT INTO time_indicator (category, value, time) VALUES ($category, $value, $time); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$category", indicator.Category);
         command.Parameters.AddWithValue("$value", indicator.Value);
         command.Parameters.AddWithValue("$time", indicator.Time);
         int rowId = Convert.ToInt32(command.ExecuteScalar());
         command.Dispose();

         return rowId;
      }

      public List<TimeIndicator> QueryTimeIndicator(DateTime minTimeLocal, DateTime maxTimeLocal)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
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
         command.Dispose();

         return result;
      }

      public void QueryLastTimeIndicators(out double[] values, out DateTime[] datesUtc)
      {
         Dictionary<int, int> idToIndex = new Dictionary<int, int>();
         using (SqliteCommand command = _Sqlite.CreateCommand())
         {
            Dictionary<string, int> indicatorIndex = new Dictionary<string, int>();
            for (int i = 0; i < Indicators.Length; i++)
               indicatorIndex[Indicators[i].Name] = i;

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

         values = new double[Indicators.Length];
         datesUtc = new DateTime[Indicators.Length];
         for (int i = 0; i < values.Length; i++)
         {
            values[i] = double.MinValue;
            datesUtc[i] = DateTime.MinValue;
         }
         using (SqliteCommand command = _Sqlite.CreateCommand())
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

      public void QueryAdjacentTimeIndicators(string category, DateTime minTimeLocal, DateTime maxTimeLocal, out double prevValue, out double nextValue)
      {
         int minId = -1;
         using (SqliteCommand command = _Sqlite.CreateCommand())
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
         using (SqliteCommand command = _Sqlite.CreateCommand())
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
         using (SqliteCommand command = _Sqlite.CreateCommand())
         {
            command.CommandText = $"SELECT id,value FROM time_indicator WHERE id IN ($minId, $maxId);";
            command.Parameters.AddWithValue($"$minId", minId);
            command.Parameters.AddWithValue($"$maxId", maxId);

            using (var reader = command.ExecuteReader())
               while (reader.Read())
               {
                  int id = reader.GetInt32(0);
                  double value = reader.GetInt32(1);
                  if (id == minId)
                     prevValue = value;
                  else
                     nextValue = value;
               }
         }
      }

      #endregion
   }
}
