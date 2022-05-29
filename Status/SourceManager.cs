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
using Tildetool.Status.Serialization;

namespace Tildetool.Status
{
   public class SourceManager
   {
      #region Singleton

      private static SourceManager _Instance;
      public static SourceManager Instance => _Instance ?? (_Instance = new SourceManager());

      #endregion Singleton
      #region Variables

      // Raw data
      SourceCacheBundle SourceCache;
      SourceBundle Source;

      // Processed data
      internal List<Source> Sources;

      #endregion
      #region Source Management

      public event EventHandler<int> SourceChanged;

      Timer? TickTimer = null;
      public void StartTick()
      {
         if (TickTimer != null)
            return;
         TickTimer = new Timer();
         TickTimer.Interval = 1000;
         TickTimer.Elapsed += (s, e) =>
         {
            TickTimer.Stop();
            for (int i = 0; i < Sources.Count; i++)
            {
               try
               {
                  // Make sure we're not refreshing in a thread already.
                  if (Sources[i].RefreshTask != null && !Sources[i].RefreshTask.IsCompleted)
                     continue;
                  if (!Sources[i].NeedsRefresh(SourceBundle.SourceData[Sources[i].Guid].LastUpdate))
                     continue;

                  Console.WriteLine("Updating source " + Sources[i].Title + " - " + Sources[i].Subtitle + " (previously " + SourceBundle.SourceData[Sources[i].Guid].LastUpdate.ToString() + ", now " + DateTime.Now.ToString() + ")");
                  int index = i;
                  int changeIndex = Sources[index].ChangeIndex;
                  Task task = Sources[index].Refresh();

                  // We'll handle when it finishes.
                  task.ContinueWith(t =>
                  {
                     if (Sources[index].ChangeIndex != changeIndex)
                     {
                        UpdateSource(Sources[index]);
                        SourceChanged?.Invoke(this, index);
                     }
                  });
               }
               catch (Exception ex)
               {
                  Console.WriteLine(ex.ToString());
               }
            }
            TickTimer.Start();
         };
         TickTimer.Start();
      }

      public void UpdateSource(Source source)
      {
         SourceData data = SourceBundle.SourceData[source.Guid];
         data.Status = source.Status;
         data.State = source.State;
         data.LastUpdate = DateTime.Now;
         SaveLater();
      }

      #endregion
      #region Serialize

      public bool Load()
      {
         // Read from the file.
         bool result = false;
         if (File.Exists("Source.json"))
         {
            try
            {
               string jsonString = File.ReadAllText("Source.json");
               Source = JsonSerializer.Deserialize<SourceBundle>(jsonString)!;
               result = true;
            }
            catch (Exception ex)
            {
               MessageBox.Show(ex.ToString());
               Console.WriteLine(ex.Message);
               return false;
            }
         }
         // Initial run, make empty.
         else
         {
            Source = new SourceBundle();
            Source.DataVMs = new SourceDataVM[0];
            Source.DataBlogs = new SourceDataBlog[0];
         }

         // Populate into the Sources list.
         Sources = new List<Source>(Source.DataVMs.Length + Source.DataBlogs.Length);
         foreach (var source in Source.DataVMs)
            Sources.Add(source.Spawn());
         foreach (var source in Source.DataBlogs)
            Sources.Add(source.Spawn());

         // Save if we didn't have any previously.
         if (!result)
            Save();

         return result;
      }
      public bool Save()
      {
         // Pull from the dictionary to data.

         // Write to file.
         try
         {
            string jsonString = JsonSerializer.Serialize<SourceBundle>(Source, new JsonSerializerOptions { WriteIndented = true });
            //File.WriteAllText("Source.json", jsonString);
         }
         catch (Exception ex)
         {
            MessageBox.Show(ex.ToString());
            Console.WriteLine(ex.Message);
            return false;
         }

         return true;
      }

      public bool LoadCache()
      {
         // Read from the file.
         bool result = false;
         if (File.Exists("SourceCache.json"))
         {
            try
            {
               string jsonString = File.ReadAllText("SourceCache.json");
               SourceCache = JsonSerializer.Deserialize<SourceCacheBundle>(jsonString)!;
               result = true;
            }
            catch (Exception ex)
            {
               MessageBox.Show(ex.ToString());
               Console.WriteLine(ex.Message);
               return false;
            }
         }
         // Initial run, make empty.
         else
         {
            SourceCache = new SourceCacheBundle();
            SourceCache.SourceData = new Dictionary<string, SourceCacheData>();
         }

         // Make sure each source has a SourceData
         foreach (Source source in Sources)
         {
            SourceCacheData data;
            if (SourceCache.SourceData.TryGetValue(source.Guid, out data))
               source.Initialize(data.Status, data.State);
            else
               SourceCache.SourceData[source.Guid] = new SourceCacheData
               {
                  Status = source.Status,
                  State = source.State,
                  LastUpdate = DateTime.UnixEpoch
               };
         }

         // Save if we didn't have any previously.
         if (!result)
            SaveCache();

         return result;
      }
      public bool SaveCache()
      {
         // Pull from the dictionary to data.

         // Write to file.
         try
         {
            string jsonString = JsonSerializer.Serialize<SourceCacheBundle>(SourceCache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("SourceCache.json", jsonString);
         }
         catch (Exception ex)
         {
            MessageBox.Show(ex.ToString());
            Console.WriteLine(ex.Message);
            return false;
         }

         return true;
      }

      Timer? SaveTimer = null;
      public void SaveLater()
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

      #endregion Serialize
   }
}
