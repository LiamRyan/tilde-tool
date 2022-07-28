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
using System.Windows.Input;
using Tildetool.Explorer.Serialization;

namespace Tildetool.Explorer
{
    class ExplorerManager
    {
      #region Singleton

      private static ExplorerManager _Instance;
      public static ExplorerManager Instance => _Instance ?? (_Instance = new ExplorerManager());

      #endregion Singleton
      #region Variables

      // Raw data
      ExplorerBundle? Data;

      // Processed results
      public Dictionary<string, List<ExplorerCommand>> CommandByExt = new Dictionary<string, List<ExplorerCommand>>();

      #endregion
      #region File Watcher

      FileSystemWatcher? Watcher = null;
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
            Watcher = WatchFile("Explorer.json", () =>
            {
               return Load();
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
      #region Serialize

      public bool Load()
      {
         // Read from the file.
         bool result = false;
         if (File.Exists("Explorer.json"))
         {
            try
            {
               string jsonString = File.ReadAllText("Explorer.json");
               Data = JsonSerializer.Deserialize<ExplorerBundle>(jsonString)!;
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
            Data = new ExplorerBundle
            {
               Commands = new ExplorerCommand[]
               {
                  new ExplorerCommand { AsFile = false, HotkeyAsKey = Key.X, Title = "Command Prompt", Command = "cmd.exe", InWorkingDir = true },

                  new ExplorerCommand { AsFile = true,  HotkeyAsKey = Key.E, Title = "Edit", Command = "notepad.exe", Extensions = new string[] { "txt", "rtf" } },
                  new ExplorerCommand { AsFile = true,  HotkeyAsKey = Key.C, Title = "Copy Path", Command = "{COPY}" },
               }
            };
            Save();
         }

         // Process it.
         try
         {
            CommandByExt = new Dictionary<string, List<ExplorerCommand>>();
            List<ExplorerCommand> sublist;
            foreach (ExplorerCommand command in Data.Commands)
            {
               Key testKey = command.HotkeyAsKey;

               string[] exts;
               if (command.Extensions == null || command.Extensions.Length == 0)
                  exts = new string[] { "*" };
               else
                  exts = command.Extensions;

               foreach (string ext in exts)
               {
                  if (CommandByExt.TryGetValue(ext, out sublist))
                     sublist.Add(command);
                  else
                     CommandByExt[ext] = new List<ExplorerCommand>(new ExplorerCommand[] { command });
               }
            }
         }
         catch (Exception ex)
         {
            MessageBox.Show(ex.ToString());
            Console.WriteLine(ex.Message);
         }

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
            string jsonString = JsonSerializer.Serialize<ExplorerBundle>(Data, new JsonSerializerOptions { WriteIndented = true });
            //File.WriteAllText("Explorer.json", jsonString);
         }
         catch (Exception ex)
         {
            MessageBox.Show(ex.ToString());
            Console.WriteLine(ex.Message);
         }
         if (Watcher != null)
            Watcher.EnableRaisingEvents = true;
      }

      #endregion Serialize
   }
}
