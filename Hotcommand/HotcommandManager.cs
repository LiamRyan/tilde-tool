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

namespace Tildetool.Hotcommand
{
   public class HmContext
   {
      public string Name;
      public Dictionary<string, Command> Commands = new Dictionary<string, Command>();
      public Dictionary<string, Command> QuickTags = new Dictionary<string, Command>();
   }
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
      public Dictionary<string, HmContext> Context = new Dictionary<string, HmContext>();
      public Dictionary<string, HmContext> ContextTag = new Dictionary<string, HmContext>();

      // State
      public HmContext CurrentContext;

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
            Data.Hotcommand = new Command[]
               {
                  new Command {
                     Tag = "DOCUMENT",
                     Spawns = new CommandSpawn[] { new CommandSpawn {
                        FileName = "explorer.exe",
                        Arguments = "D:\\Documents" } } },
               };
            Data.QuickTag = new QuickTag[]
               {
                  new QuickTag { Tag = "DOC", Target = "DOCUMENT" },
               };
            Save();
            WatchFile();
         }

         // Process it.
         Context = new Dictionary<string, HmContext>();
         HmContext context;

         if (Data.Context != null)
            foreach (var dcontext in Data.Context)
            {
               context = new HmContext { Name = dcontext.Name };
               if (dcontext.Hotcommand != null)
                  foreach (Command command in dcontext.Hotcommand)
                     context.Commands[command.Tag] = command;
               if (dcontext.QuickTag != null)
                  foreach (QuickTag qtag in dcontext.QuickTag)
                  {
                     Command command;
                     if (context.Commands.TryGetValue(qtag.Target, out command))
                        context.QuickTags[qtag.Tag] = command;
                     else
                        MessageBox.Show("Invalid quicktag " + qtag.Tag + " in context " + dcontext.Name);
                  }
               Context[dcontext.Name] = context;
            }

         context = new HmContext { Name = "DEFAULT" };
         if (Data.Hotcommand != null)
            foreach (Command command in Data.Hotcommand)
               context.Commands[command.Tag] = command;
         if (Data.QuickTag != null)
            foreach (QuickTag qtag in Data.QuickTag)
            {
               HmContext subcontext;
               Command command;
               if (Context.TryGetValue(qtag.Target, out subcontext))
                  ContextTag[qtag.Tag] = subcontext;
               else if (context.Commands.TryGetValue(qtag.Target, out command))
                  context.QuickTags[qtag.Tag] = command;
               else
                  MessageBox.Show("Invalid quicktag " + qtag.Tag);
            }
         Context["DEFAULT"] = context;
         CurrentContext = context;

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
