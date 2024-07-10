using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Markup;
using Tildetool.Hotcommand.Serialization;
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
      public DateTime StartTime;
      public DateTime EndTime;

      public string Name => Description;
      public float HourBegin => (float)StartTime.ToLocalTime().TimeOfDay.TotalHours;
      public float HourEnd => (float)EndTime.ToLocalTime().TimeOfDay.TotalHours;
   }

   public class TimeManager
   {
      #region Singleton

      private static TimeManager _Instance;
      public static TimeManager Instance => _Instance ?? (_Instance = new TimeManager());

      #endregion Singleton
      #region Variables

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
               (App.Current as App).ShowTimekeep(true);
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
            SaveCache();
         }

         // Process it.
         if (cacheData.Project != null)
            Data = cacheData.Project.Append(IdleProject).ToArray();
         HotkeyToProject = Data.ToDictionary(p => p.Hotkey);
         IdentToProject = Data.ToDictionary(p => p.Ident);

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

      SqliteConnection _Sqlite;
      Dictionary<string, int> ProjectIdentToId = new Dictionary<string, int>();
      public void ConnectSqlite()
      {
         if (_Sqlite != null)
            return;
         _Sqlite = new SqliteConnection(new SqliteConnectionStringBuilder() { Mode = SqliteOpenMode.ReadWriteCreate, DataSource = "TimekeepHistory.db" }.ToString());
         _Sqlite.Open();

         // Create and populate the project table.
         SqliteCommand command;
         command = _Sqlite.CreateCommand();
         command.CommandText = "CREATE TABLE IF NOT EXISTS \"project\" ( \"id\"\tINTEGER, \"ident\" TEXT UNIQUE, PRIMARY KEY(\"id\" AUTOINCREMENT) );";
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
         command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT ident,id FROM project;";
         using (var reader = command.ExecuteReader())
            while (reader.Read())
               ProjectIdentToId[reader.GetString(0)] = reader.GetInt32(1);
         command.Dispose();

         // Create the time period table
         command = _Sqlite.CreateCommand();
         command.CommandText = "CREATE TABLE IF NOT EXISTS \"time_period\" ( \"id\" INTEGER, \"project_id\" INTEGER, \"start_time\" TEXT, \"end_time\" TEXT, PRIMARY KEY(\"id\" AUTOINCREMENT) );";
         command.ExecuteNonQuery();
         command.Dispose();

         // Create the event table.
         command = _Sqlite.CreateCommand();
         command.CommandText = "CREATE TABLE IF NOT EXISTS \"time_event\" ( \"id\" INTEGER, \"description\" TEXT, \"start_time\" TEXT, \"end_time\" TEXT, PRIMARY KEY(\"id\" AUTOINCREMENT) );";
         command.ExecuteNonQuery();
         command.Dispose();

         //
         RefreshTodayTime();
      }

      int AddHistoryLine(TimePeriod period)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "INSERT INTO time_period (project_id, start_time, end_time) VALUES ($project_id, $start_time, $end_time); SELECT last_insert_rowid();";
         command.Parameters.AddWithValue("$project_id", ProjectIdentToId[period.Ident]);
         command.Parameters.AddWithValue("$start_time", period.StartTime);
         command.Parameters.AddWithValue("$end_time", period.EndTime);
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
            CurrentTimePeriod = AddHistoryLine(new TimePeriod { Ident = CurrentProject.Ident, StartTime = CurrentStartTime, EndTime = DateTime.UtcNow });
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
               IdentToProject[ident].TimeTodaySec = seconds;
            }
         command.Dispose();
      }

      public List<TimePeriod> QueryTimePeriod(DateTime minTimeUtc, DateTime maxTimeUtc)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT time_period.id,project.ident,start_time,end_time FROM time_period INNER JOIN project ON project.id = project_id WHERE end_time >= $minTime AND start_time <= $maxTime;";
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
               result.Add(new TimePeriod { DbId = dbid, Ident = projectIdent, StartTime = startTime, EndTime = endTime });
            }
         command.Dispose();

         return result;
      }

      public List<TimePeriod> QueryTimePeriod(Project project, DateTime minTimeUtc, DateTime maxTimeUtc)
      {
         SqliteCommand command = _Sqlite.CreateCommand();
         command.CommandText = "SELECT time_period.id,start_time,end_time FROM time_period INNER JOIN project ON project.id = project_id WHERE end_time >= $minTime AND start_time <= $maxTime AND project.ident = $ident;";
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
               result.Add(new TimePeriod { DbId = dbid, Ident = project.Ident, StartTime = startTime, EndTime = endTime });
            }
         command.Dispose();

         return result;
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

      #endregion
   }
}
