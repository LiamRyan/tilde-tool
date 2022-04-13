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
   public class HmUsage
   {
      public string WrittenText;
      public Command Command;
      public float Frequency;
   }
   public class HmContext
   {
      public string Name;
      public Dictionary<string, Command> Commands = new Dictionary<string, Command>();
      public Dictionary<string, Command> QuickTags = new Dictionary<string, Command>();
      public Dictionary<string, List<HmUsage>> UsageByText = new Dictionary<string, List<HmUsage>>();
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
      UsageData UsageData;

      // Processed results
      public Dictionary<string, HmContext> ContextByTag = new Dictionary<string, HmContext>();
      public Dictionary<string, HmContext> ContextTag = new Dictionary<string, HmContext>();

      // State
      public HmContext CurrentContext;

      #endregion
      #region File Watcher

      FileSystemWatcher? Watcher = null;
      FileSystemWatcher? WatcherUsage = null;
      FileSystemWatcher WatchFile(string file, System.Func<bool> callback)
      {
         // Create a new FileSystemWatcher and set its properties.
         FileSystemWatcher watcher = new FileSystemWatcher();
         watcher.Path = ".";
         watcher.Filter = file;
         watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime;
         watcher.IncludeSubdirectories = false;

         // Add event handlers.
         watcher.Changed += (object src, FileSystemEventArgs e) => WatcherEvent(callback);
         watcher.Created += (object src, FileSystemEventArgs e) => WatcherEvent(callback);
         watcher.Deleted += (object src, FileSystemEventArgs e) => WatcherEvent(callback);
         watcher.Renamed += (object src, RenamedEventArgs e) => WatcherEvent(callback);

         // Begin watching.
         watcher.EnableRaisingEvents = true;
         return watcher;
      }
      public void WatchFile()
      {
         if (Watcher == null)
            Watcher = WatchFile("Hotcommand.json", () =>
               {
                  bool result = Load();
                  if (result)
                     LoadUsage();
                  return result;
               });

         if (WatcherUsage == null)
            WatcherUsage = WatchFile("Usage.json", () =>
            {
               return LoadUsage();
            });
      }

      Timer? WatcherTimer = null;
      private void WatcherEvent(System.Func<bool> callback)
      {
         if (WatcherTimer != null)
            return;
         WatcherTimer = new Timer();
         WatcherTimer.Interval = 100;
         WatcherTimer.Elapsed += (s, e) =>
         {
            WatcherTimer.Stop();
            bool result = callback();
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

      #endregion
      #region Usage

      public void IncFrequency(string text, Command command, float decay)
      {
         List<HmUsage>? usages;
         if (!CurrentContext.UsageByText.TryGetValue(text, out usages))
            CurrentContext.UsageByText[text] = usages = new List<HmUsage>();

         HmUsage? foundUsage = null;
         foreach (HmUsage usage in usages)
         {
            if (usage.Command == command)
               foundUsage = usage;
            else
               usage.Frequency *= decay;
         }
         if (foundUsage == null)
         {
            foundUsage = new HmUsage { Command = command };
            usages.Add(foundUsage);
         }
         foundUsage.Frequency += 0.1f;

         usages.Sort((a, b) => -a.Frequency.CompareTo(b.Frequency));
      }

      #endregion
      #region Serialize

      public bool Load()
      {
         // Read from the file.
         bool result = false;
         if (File.Exists("Hotcommand.json"))
         {
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
         }

         // Process it.
         ContextByTag = new Dictionary<string, HmContext>();
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
               ContextByTag[dcontext.Name] = context;
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
               if (ContextByTag.TryGetValue(qtag.Target, out subcontext))
                  ContextTag[qtag.Tag] = subcontext;
               else if (context.Commands.TryGetValue(qtag.Target, out command))
                  context.QuickTags[qtag.Tag] = command;
               else
                  MessageBox.Show("Invalid quicktag " + qtag.Tag);
            }
         ContextByTag["DEFAULT"] = context;
         CurrentContext = context;

         return result;
      }
      public void Save()
      {
         // Pull from the dictionary to data.

         // Write to file.
         if (Watcher != null)
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
         if (Watcher != null)
            Watcher.EnableRaisingEvents = true;
      }

      public bool LoadUsage()
      {
         // Read from the file.
         bool result = false;
         if (File.Exists("Usage.json"))
         {
            try
            {
               string jsonString = File.ReadAllText("Usage.json");
               UsageData = JsonSerializer.Deserialize<UsageData>(jsonString)!;
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
            UsageData = new UsageData();
            SaveUsage();
         }

         // Process it.
         if (UsageData.Usage != null)
            foreach (var dusage in UsageData.Usage)
            {
               if (dusage.WrittenText == null)
                  dusage.WrittenText = "";
               if (dusage.Context == null || dusage.Tag == null)
                  continue;
               HmContext? context;
               if (!ContextByTag.TryGetValue(dusage.Context, out context))
                  continue;
               Command? command;
               if (!context.Commands.TryGetValue(dusage.Tag, out command))
                  continue;

               List<HmUsage>? list;
               if (!context.UsageByText.TryGetValue(dusage.WrittenText, out list))
                  context.UsageByText[dusage.WrittenText] = list = new List<HmUsage>();
               list.Add(new HmUsage { WrittenText = dusage.WrittenText, Frequency = dusage.Frequency, Command = command });
            }
         foreach (HmContext context in ContextByTag.Values)
            foreach (List<HmUsage> usages in context.UsageByText.Values)
               usages.Sort((a, b) => -a.Frequency.CompareTo(b.Frequency));

         return result;
      }
      public bool SaveUsage()
      {
         // Pull from the dictionary to data.
         List<Usage> dusages = new List<Usage>();
         foreach (HmContext context in ContextByTag.Values)
            foreach (List<HmUsage> usages in context.UsageByText.Values)
               foreach (HmUsage usage in usages)
                  dusages.Add(new Usage
                  {
                     WrittenText = usage.WrittenText,
                     Frequency = usage.Frequency,
                     Context = context.Name,
                     Tag = usage.Command.Tag
                  });
         UsageData = new UsageData { Usage = dusages.ToArray() };

         // Write to file.
         bool result = false;
         if (WatcherUsage != null)
            WatcherUsage.EnableRaisingEvents = false;
         try
         {
            string jsonString = JsonSerializer.Serialize<UsageData>(UsageData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("Usage.json", jsonString);
            result = true;
         }
         catch (Exception ex)
         {
            MessageBox.Show(ex.ToString());
            Console.WriteLine(ex.Message);
         }
         if (WatcherUsage != null)
            WatcherUsage.EnableRaisingEvents = true;
         return result;
      }

      Timer? SaveTimer = null;
      public void SaveUsageLater()
      {
         if (SaveTimer != null)
            return;
         SaveTimer = new Timer();
         SaveTimer.Interval = 2000;
         SaveTimer.Elapsed += (s, e) =>
         {
            SaveTimer.Stop();
            bool result = SaveUsage();
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

      #endregion Serialize
   }
}
