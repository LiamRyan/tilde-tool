using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

   public class TimeManager
   {
      #region Singleton

      private static TimeManager _Instance;
      public static TimeManager Instance => _Instance ?? (_Instance = new TimeManager());

      #endregion Singleton
      #region Variables

      public Command OpenDatabase;

      public double DayBeginHour;
      public double DayEndHour;

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

      public bool DoesIndicatorExist(string category)
         => IndicatorByCategory.ContainsKey(category);

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
      public static Project? TimetrackProject;
      public Project[] Data;

      // Processed results
      public Dictionary<string, Project> HotkeyToProject;
      public Dictionary<string, Project> IdentToProject;
      public WeeklySchedule[][] ScheduleByDayOfWeek = Enumerable.Range(0, 7).Select(s => new WeeklySchedule[0]).ToArray();
      public DayTask[][] TaskByDayOfWeek = Enumerable.Range(0, 7).Select(s => Array.Empty<DayTask>()).ToArray();

      // State
      public Project? CurrentProject;
      public long CurrentTimePeriod = -1;
      public DateTime CurrentStartTime;
      public string CurrentNotes;

      public Project PausedProject;
      public string PausedNotes;

      #endregion
      #region Active Project

      Timer _Timer;
      public void StartTick()
      {
         if (_Timer != null)
            return;
         _Timer = new Timer { Interval = 60000 };
         _Timer.Elapsed += (o, e) => { UpdateCurrentTimePeriod(DateTime.UtcNow); };
         _Timer.Start();

         SetProject(IdleProject, null);

         SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
      }

      public void Dispose()
      {
         SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
      }

      public void SetProject(Project? project, string notes)
      {
         if (project == CurrentProject)
            return;

         // Increment the time of the old project by our value.
         bool result = SaveCache();
         if (!result)
            SaveCacheLater();

         UpdateCurrentTimePeriod(DateTime.UtcNow, force: true);
         if (CurrentProject != null)
            CurrentProject.TimeTodaySec += (int)(DateTime.UtcNow - CurrentStartTime).TotalSeconds;

         // Switch to the new.
         CurrentProject = project;
         CurrentStartTime = DateTime.UtcNow;
         CurrentTimePeriod = -1;
         CurrentNotes = notes;
      }

      public void AlterProject(Project project, string notes)
      {
         if (project == CurrentProject)
            return;

         // Increment the time of the old project by our value.
         bool result = SaveCache();
         if (!result)
            SaveCacheLater();

         // Force-switch to the new project and update the database accordingly.
         CurrentProject = project;
         CurrentNotes = notes;
         UpdateCurrentTimePeriod(DateTime.UtcNow);
      }

      public void RetroapplyProject(Project project, DateTime from, DateTime to)
      {
         Project? originalProject = CurrentProject;
         string originalNotes = CurrentNotes;

         bool result = SaveCache();
         if (!result)
            SaveCacheLater();

         // Write our PREVIOUS project up until from.
         UpdateCurrentTimePeriod(from, force: true);
         if (CurrentProject != null)
            CurrentProject.TimeTodaySec += (int)(from - CurrentStartTime).TotalSeconds;

         // Write the RETRO project from from until to.
         CurrentProject = project;
         CurrentStartTime = from;
         CurrentTimePeriod = -1;
         CurrentNotes = null;
         UpdateCurrentTimePeriod(to, force: true);
         if (CurrentProject != null)
            CurrentProject.TimeTodaySec += (int)(to - CurrentStartTime).TotalSeconds;

         // Restart with the previous project going forward.
         CurrentProject = originalProject;
         CurrentStartTime = to;
         CurrentTimePeriod = -1;
         CurrentNotes = originalNotes;
         UpdateCurrentTimePeriod(DateTime.UtcNow);
         if (CurrentProject != null)
            CurrentProject.TimeTodaySec += (int)(DateTime.UtcNow - CurrentStartTime).TotalSeconds;
      }

      private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
      {
         bool result;
         switch (e.Reason)
         {
            case SessionSwitchReason.SessionLock:
               App.WriteLog("Screen locked at " + DateTime.UtcNow.ToString() + (CurrentProject != null ? (", pausing " + CurrentProject.Name) : ""));
               PausedProject = CurrentProject;
               PausedNotes = CurrentNotes;
               SetProject(null, null);
               break;

            case SessionSwitchReason.SessionUnlock:
               App.WriteLog("Screen unlocked at " + DateTime.UtcNow.ToString() + (PausedProject != null ? (", resuming " + PausedProject.Name) : ""));
               SetProject(PausedProject, PausedNotes);
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
         {
            Data = cacheData.Project.Append(IdleProject).ToArray();
            TimetrackProject = Data.FirstOrDefault(p => string.Compare(p.Name, "Time tracking") == 0);
         }
         HotkeyToProject = Data.ToDictionary(p => p.Hotkey);
         IdentToProject = Data.ToDictionary(p => p.Ident);

         OpenDatabase = cacheData.OpenDatabase;
         DayBeginHour = cacheData.DayBeginHour;
         DayEndHour = cacheData.DayEndHour;

         Indicators = (cacheData.Indicator ?? new Indicator[0]).ToArray();
         IndicatorByCategory = Indicators.ToDictionary(k => k.Name);
         IndicatorByHotkey = Indicators.ToDictionary(k => k.Hotkey);

         cacheData.WeeklyDay ??= new();
         {
            ScheduleByDayOfWeek[(int)DayOfWeek.Sunday] = cacheData.WeeklyDay.Sun ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Monday] = cacheData.WeeklyDay.Mon ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Tuesday] = cacheData.WeeklyDay.Tue ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Wednesday] = cacheData.WeeklyDay.Wed ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Thursday] = cacheData.WeeklyDay.Thu ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Friday] = cacheData.WeeklyDay.Fri ?? new WeeklySchedule[0];
            ScheduleByDayOfWeek[(int)DayOfWeek.Saturday] = cacheData.WeeklyDay.Sat ?? new WeeklySchedule[0];
         }

         cacheData.WeeklyTask ??= new();
         {
            TaskByDayOfWeek[(int)DayOfWeek.Sunday] = cacheData.WeeklyTask.Sun ?? Array.Empty<DayTask>();
            TaskByDayOfWeek[(int)DayOfWeek.Monday] = cacheData.WeeklyTask.Mon ?? Array.Empty<DayTask>();
            TaskByDayOfWeek[(int)DayOfWeek.Tuesday] = cacheData.WeeklyTask.Tue ?? Array.Empty<DayTask>();
            TaskByDayOfWeek[(int)DayOfWeek.Wednesday] = cacheData.WeeklyTask.Wed ?? Array.Empty<DayTask>();
            TaskByDayOfWeek[(int)DayOfWeek.Thursday] = cacheData.WeeklyTask.Thu ?? Array.Empty<DayTask>();
            TaskByDayOfWeek[(int)DayOfWeek.Friday] = cacheData.WeeklyTask.Fri ?? Array.Empty<DayTask>();
            TaskByDayOfWeek[(int)DayOfWeek.Saturday] = cacheData.WeeklyTask.Sat ?? Array.Empty<DayTask>();
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

      SqliteConnection _Sqlite;

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public SqliteConnection GetConn()
         => _Sqlite;

      void Query(string commandText)
      {
         using (SqliteCommand command = _Sqlite.CreateCommand())
         {
            command.CommandText = commandText;
            command.ExecuteNonQuery();
         }
      }
      public Dictionary<string, int> ProjectIdentToId = new();
      public Dictionary<string, string> ProjectIdentToCategory = new();
      public Dictionary<string, int> ProjectIdentToOrder = new();
      public List<string> ProjectIdentAutoSuggest = new();
      public Dictionary<string, double> ProjectIdentTargetTime = new();
      public void ConnectSqlite()
      {
         if (_Sqlite != null)
            return;
         _Sqlite = new SqliteConnection(new SqliteConnectionStringBuilder() { Mode = SqliteOpenMode.ReadWriteCreate, DataSource = "TimekeepHistory.db" }.ToString());
         _Sqlite.Open();

         // Create and populate the project table.
         SqliteCommand command;
         command = _Sqlite.CreateCommand();
         command.CommandText = "CREATE TABLE IF NOT EXISTS \"project\" ( \"id\"\tINTEGER, \"ident\" TEXT UNIQUE, \"category\" INTEGER, \"sort_order\" INTEGER NOT NULL DEFAULT 0, \"auto_suggest\" INTEGER NOT NULL DEFAULT 1, \"target_time\" REAL, PRIMARY KEY(\"id\" AUTOINCREMENT) );";
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
         ProjectIdentTargetTime.Clear();

         var command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT ident, id, category, sort_order, auto_suggest, target_time FROM project;";
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
               if (!reader.IsDBNull(5))
                  ProjectIdentTargetTime[ident] = reader.GetDouble(5);
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

      public long AddHistoryLine(TimePeriod period)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         if (!string.IsNullOrEmpty(period.Notes))
            command.CommandText = "INSERT INTO time_period (project_id, start_time, end_time, on_computer, notes) VALUES ($project_id, $start_time, $end_time, $on_computer, $notes); SELECT last_insert_rowid();";
         else
            command.CommandText = "INSERT INTO time_period (project_id, start_time, end_time, on_computer) VALUES ($project_id, $start_time, $end_time, $on_computer); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$project_id", ProjectIdentToId[period.Ident]);
         command.Parameters.AddWithValue("$start_time", period.StartTime);
         command.Parameters.AddWithValue("$end_time", period.EndTime);
         command.Parameters.AddWithValue("$on_computer", period.OnComputer);
         if (!string.IsNullOrEmpty(period.Notes))
            command.Parameters.AddWithValue("$notes", period.Notes);
         int rowId = Convert.ToInt32(command.ExecuteScalar());
         command.Dispose();

         return rowId;
      }

      public void UpdateHistoryLine(long id, TimePeriod period)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         if (!string.IsNullOrEmpty(period.Notes))
            command.CommandText = "UPDATE time_period SET project_id = $project_id, start_time = $start, end_time = $end, notes = $notes WHERE id = $id;";
         else
            command.CommandText = "UPDATE time_period SET project_id = $project_id, start_time = $start, end_time = $end, notes = null WHERE id = $id;";
         command.Parameters.AddWithValue("$id", id);
         command.Parameters.AddWithValue("$project_id", ProjectIdentToId[period.Ident]);
         command.Parameters.AddWithValue("$start", period.StartTime);
         command.Parameters.AddWithValue("$end", period.EndTime);
         if (!string.IsNullOrEmpty(period.Notes))
            command.Parameters.AddWithValue("notes", period.Notes);
         command.ExecuteNonQuery();
         command.Dispose();
      }

      public void RemoveHistoryLine(long id)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "DELETE FROM time_period WHERE id = $id;";
         command.Parameters.AddWithValue("$id", id);
         command.ExecuteNonQuery();
         command.Dispose();
      }

      public void UpdateCurrentTimePeriod(DateTime timeUntil, bool force = false)
      {
         // If we have no project, nothing to do.
         if (CurrentProject == null)
         {
            CurrentTimePeriod = -1;
            return;
         }
         // If it is too short right now, don't add (or remove if necessary)
         if ((timeUntil - CurrentStartTime).TotalMinutes < 1.0f && !force)
         {
            if (CurrentTimePeriod != -1)
               RemoveHistoryLine(CurrentTimePeriod);
            CurrentTimePeriod = -1;
            return;
         }

         // If we overlap with previous time periods, respect them.
         //List<TimePeriod> periods = QueryTimePeriod(CurrentStartTime, timeUntil);
         //foreach (TimePeriod period in periods)
         //{
         //   if (period.DbId == CurrentTimePeriod)
         //      continue;
         //   if (period.EndTime > CurrentStartTime)
         //      CurrentStartTime = period.EndTime;
         //}

         // If we overlap with previous time periods, carve them out.
         CarveHistory(CurrentStartTime, timeUntil, excludeDbid: CurrentTimePeriod);

         // Either add or update.
         if (CurrentTimePeriod == -1)
            CurrentTimePeriod = AddHistoryLine(new TimePeriod { Ident = CurrentProject.Ident, StartTime = CurrentStartTime, EndTime = timeUntil, OnComputer = true, Notes = CurrentNotes });
         else
            UpdateHistoryLine(CurrentTimePeriod, new TimePeriod { Ident = CurrentProject.Ident, StartTime = CurrentStartTime, EndTime = timeUntil, Notes = CurrentNotes });
      }

      public void CarveHistory(DateTime minTimeUtc, DateTime maxTimeUtc, long excludeDbid = -1)
      {
         List<TimePeriod> periods = QueryTimePeriod(minTimeUtc, maxTimeUtc);
         foreach (TimePeriod period in periods)
         {
            if (period.DbId == excludeDbid)
               continue;

            // if the carve period (expanded one second each way) totally covers us, then we delete altogether.
            if (minTimeUtc.AddSeconds(-1) <= period.StartTime && period.EndTime <= maxTimeUtc.AddSeconds(1))
               RemoveHistoryLine((int)period.DbId);
            else
            {
               // if this period (shrunk one second each way) totally covers the carve period, we split in two
               //  on each end.
               if (period.StartTime.AddSeconds(1) < minTimeUtc && maxTimeUtc < period.EndTime.AddSeconds(-1))
               {
                  TimePeriod period2 = period.Clone();
                  period2.StartTime = maxTimeUtc;
                  AddHistoryLine(period2);

                  period.EndTime = minTimeUtc;
               }
               // otherwise we clip at either one end or the other
               else if (period.StartTime.AddSeconds(1) <= minTimeUtc)
                  period.EndTime = minTimeUtc;
               else
                  period.StartTime = maxTimeUtc;

               // done
               UpdateHistoryLine((int)period.DbId, period);
            }
         }
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
         command.CommandText = "SELECT time_period.id,project.ident,start_time,end_time,on_computer,notes FROM time_period INNER JOIN project ON project.id = project_id WHERE end_time >= $minTime AND start_time <= $maxTime;";
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
               string notes = reader.IsDBNull(5) ? null : reader.GetString(5);
               result.Add(new TimePeriod { DbId = dbid, Ident = projectIdent, StartTime = startTime, EndTime = endTime, OnComputer = onComputer, Notes = notes });
            }
         command.Dispose();

         return result;
      }

      public List<TimePeriod> QueryTimePeriod(Project project, DateTime minTimeUtc, DateTime maxTimeUtc)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT time_period.id,start_time,end_time,on_computer,notes FROM time_period INNER JOIN project ON project.id = project_id WHERE end_time >= $minTime AND start_time <= $maxTime AND project.ident = $ident;";
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
               string notes = reader.IsDBNull(4) ? null : reader.GetString(4);
               result.Add(new TimePeriod { DbId = dbid, Ident = project.Ident, StartTime = startTime, EndTime = endTime, OnComputer = onComputer, Notes = notes });
            }
         command.Dispose();

         return result;
      }

      public void QueryDayPeriod(DateTime day, out DateTime dayBegin, out DateTime dayEnd)
      {
         DateTime dayStrip = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0);
         DateTime dayStripUtc = dayStrip.ToUniversalTime();

         // TODO: implement;
         dayBegin = dayStrip;
         dayEnd = dayStrip.AddDays(1);
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

      #endregion
   }
}
