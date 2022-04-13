using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Timers;

namespace Tildetool.Hotcommand
{
   public class HotcommandManager
   {
      #region Singleton

      private static HotcommandManager _Instance;
      public static HotcommandManager Instance => _Instance ?? (_Instance = new HotcommandManager());

      #endregion Singleton
      #region Variables

      // Raw data
      HotcommandData Data;

      // Processed results
      public Dictionary<string, Hotcommand> Commands = new Dictionary<string, Hotcommand>();
      public Dictionary<string, Hotcommand> QuickTags = new Dictionary<string, Hotcommand>();

      #endregion
      #region File Watcher

      FileSystemWatcher? Watcher = null;
      void WatchFile()
      {
         if (Watcher != null)
            return;
         if (!File.Exists("Hotcommand.json"))
            return;

         // Create a new FileSystemWatcher and set its properties.
         Watcher = new FileSystemWatcher();
         Watcher.Path = ".";
         Watcher.Filter = "Hotcommand.json";
         Watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime;
         Watcher.IncludeSubdirectories = false;

         // Add event handlers.
         Watcher.Changed += new FileSystemEventHandler(Watcher_Changed);
         Watcher.Created += new FileSystemEventHandler(Watcher_Changed);
         Watcher.Deleted += new FileSystemEventHandler(Watcher_Changed);
         Watcher.Renamed += new RenamedEventHandler(Watcher_Renamed);

         // Begin watching.
         Watcher.EnableRaisingEvents = true;
      }

      Timer? WatcherTimer = null;
      private void WatcherEvent()
      {
         if (WatcherTimer != null)
            return;
         WatcherTimer = new Timer();
         WatcherTimer.Interval = 100;
         WatcherTimer.Elapsed += (s, e) =>
         {
            WatcherTimer.Stop();
            bool result = Load();
            if (!result)
            {
               WatcherTimer.Interval = 1000;
               WatcherTimer.Start();
            }
            else
            {
               WatcherTimer.Dispose();
               WatcherTimer = null;
            }
         };
         WatcherTimer.Start();
      }
      private void Watcher_Renamed(object sender, RenamedEventArgs e)
      {
         WatcherEvent();
      }
      private void Watcher_Changed(object sender, FileSystemEventArgs e)
      {
         WatcherEvent();
      }

      #endregion
      #region Serialize

      public bool Load()
      {
         // Read from the file.
         bool result = false;
         if (File.Exists("Hotcommand.json"))
         {
            WatchFile();
            try
            {
               string jsonString = File.ReadAllText("Hotcommand.json");
               Data = JsonSerializer.Deserialize<HotcommandData>(jsonString)!;
               result = true;
            }
            catch (Exception ex)
            {
               MessageBox.Show(ex.ToString());
               Console.WriteLine(ex.Message);
            }
         }
         else
         {
            // Initial run, populate some basic data.
            Data = new HotcommandData();
            Data.Hotcommand = new Hotcommand[]
               {
                  new Hotcommand {
                     Tag = "DOCUMENT",
                     Spawns = new HotcommandSpawn[] { new HotcommandSpawn {
                        FileName = "explorer.exe",
                        Arguments = "D:\\Documents" } } },
               };
            Data.QuickTag = new HotcommandQuickTag[]
               {
                  new HotcommandQuickTag { Tag = "DOC", Target = "DOCUMENT" },
               };
            Save();
            WatchFile();
         }

         // Process it.
         Commands = new Dictionary<string, Hotcommand>();
         if (Data.Hotcommand != null)
            foreach (Hotcommand command in Data.Hotcommand)
               Commands[command.Tag] = command;
         QuickTags = new Dictionary<string, Hotcommand>();
         if (Data.QuickTag != null)
            foreach (HotcommandQuickTag qtag in Data.QuickTag)
            {
               Hotcommand command;
               if (Commands.TryGetValue(qtag.Target, out command))
                  QuickTags[qtag.Tag] = command;
            }

         return result;
      }
      public void Save()
      {
         // Pull from the dictionary to data.

         // Write to file.
         Watcher.EnableRaisingEvents = false;
         try
         {
            string jsonString = JsonSerializer.Serialize<HotcommandData>(Data, new JsonSerializerOptions { WriteIndented = true });
            //File.WriteAllText("Hotcommand.json", jsonString);
         }
         catch (Exception ex)
         {
            MessageBox.Show(ex.ToString());
            Console.WriteLine(ex.Message);
         }
         Watcher.EnableRaisingEvents = true;
      }

      #endregion Serialize
   }
}
