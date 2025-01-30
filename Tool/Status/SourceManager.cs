using System;
using System.Collections.Generic;
using System.Data;
using System.Formats.Asn1;
using System.Globalization;
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

      public struct SourceEventArgs
      {
         public int Index;
         public bool CacheChanged;
      }
      public event EventHandler<SourceEventArgs> SourceChanged;
      public event EventHandler<int> SourceQuery;

      Timer? TickTimer = null;
      public void StartTick()
      {
         if (TickTimer != null)
            return;

         // Let them refresh their display right away.
         for (int i = 0; i < Sources.Count; i++)
            Sources[i].Display();

         //
         TickTimer = new Timer();
         TickTimer.Interval = 1000;
         TickTimer.Elapsed += (s, e) =>
         {
            TickTimer.Stop();

            // Make sure none of the sources are already querying.
            bool anyQuery = Sources.Any(src => src.IsQuerying);
            if (!anyQuery)
            {
               HashSet<string> alreadyQuery = new HashSet<string>();
               DateTime now = DateTime.Now;
               for (int i = 0; i < Sources.Count; i++)
               {
                  try
                  {
                     // Make sure it's time for an update.
                     bool shouldUpdate = false;
                     if (NeedRefresh(Sources[i]))
                     {
                        if (Sources[i].Ephemeral)
                           shouldUpdate = true;
                        else if (!alreadyQuery.Contains(Sources[i].Domain))
                           shouldUpdate = true;
                     }

                     // Alright then, start an update.
                     if (shouldUpdate)
                     {
                        Query(i, clearCache: false);
                        alreadyQuery.Add(Sources[i].Domain);
                     }
                     else
                        Sources[i].Display();
                  }
                  catch (Exception ex)
                  {
                     App.WriteLog(ex.ToString());
                  }
               }
            }

            TickTimer.Start();
         };
         TickTimer.Start();
      }

      public DateTime GetUpdateTime(Source src)
      {
         return SourceCache.SourceData[src.Guid].LastUpdate;
      }
      public TimeSpan GetTimeSinceUpdate(Source src)
      {
         return DateTime.Now - GetUpdateTime(src);
      }
      public bool NeedRefresh(Source src)
      {
         return src.NeedsRefresh(GetUpdateTime(src), GetTimeSinceUpdate(src));
      }

      public void Query(int index, bool clearCache)
      {
         // Make sure we're not refreshing in a thread already.
         if (Sources[index].IsQuerying)
            return;

         if (!Sources[index].Ephemeral)
            App.WriteLog("Updating source " + Sources[index].Title + " - " + Sources[index].Subtitle + " (previously " + SourceCache.SourceData[Sources[index].Guid].LastUpdate.ToString() + ", now " + DateTime.Now.ToString() + ")");

         int changeIndex = Sources[index].ChangeIndex;
         Task task = Sources[index].Query(clearCache);

         SourceQuery?.Invoke(this, index);

         // We'll handle when it finishes.
         task.ContinueWith(t =>
         {
            // Always update the data.
            string cache = Sources[index].GetCache();
            SourceCacheData data = SourceCache.SourceData[Sources[index].Guid];
            bool cacheChanged = data.LastCache != cache;
            data.Status = Sources[index].Status;
            data.Article = Sources[index].Article;
            data.State = Sources[index].State;
            data.LastCache = cache;
            data.LastUpdate = DateTime.Now;

            // Refresh the visuals using the new date.
            try
            {
               Sources[index].Display();
            }
            catch (Exception ex2)
            {
               App.WriteLog(ex2.ToString());
            }

            // If something changed, save it.  Even if nothing changed, if we update rarely, save the
            //  new LastUpdate so if we close and open we don't update again.
            if (!Sources[index].Ephemeral || Sources[index].ChangeIndex != changeIndex)
               SaveLater();

            // If something changed, send an event.
            if (Sources[index].ChangeIndex != changeIndex)
               SourceChanged?.Invoke(this, new SourceEventArgs { Index = index, CacheChanged = cacheChanged });

            //
            SourceQuery?.Invoke(this, index);
         });
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
               Source = JsonSerializer.Deserialize<SourceBundle>(jsonString,
                  new JsonSerializerOptions
                  {
                     Converters = { new TimeOnlyJsonConverter() }
                  })!;
               result = true;
            }
            catch (Exception ex)
            {
               MessageBox.Show(ex.ToString());
               App.WriteLog(ex.Message);
               return false;
            }
         }
         // Initial run, make empty.
         else
         {
            Source = new SourceBundle();
            Source.DataVMs = new SourceDataVM[0];
            Source.DataBlogs = new SourceDataBlog[0];
            Source.DataUptimes = new SourceDataUptime[0];
         }

         // Populate into the Sources list.
         Sources = new List<Source>(Source.DataVMs.Length + Source.DataBlogs.Length + Source.DataUptimes.Length);
         foreach (var source in Source.DataUptimes)
            Sources.Add(source.Spawn(Source));
         foreach (var source in Source.DataVMs)
            Sources.Add(source.Spawn(Source));
         foreach (var source in Source.DataBlogs)
            if (source.Enabled)
               Sources.Add(source.Spawn(Source));

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
            App.WriteLog(ex.Message);
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
               App.WriteLog(ex.Message);
               return false;
            }
         }

         // Initial run, make empty.
         if (SourceCache == null)
            SourceCache = new SourceCacheBundle();
         if (SourceCache.SourceData == null)
            SourceCache.SourceData = new Dictionary<string, SourceCacheData>();

         // Make sure each source has a SourceData
         foreach (Source source in Sources)
         {
            SourceCacheData data;
            if (SourceCache.SourceData.TryGetValue(source.Guid, out data))
               source.Initialize(data.Status, data.Article, data.State, data.LastCache);
            else
               SourceCache.SourceData[source.Guid] = new SourceCacheData
               {
                  Status = source.Status,
                  Article = source.Article,
                  State = source.State,
                  LastUpdate = DateTime.UnixEpoch,
                  LastCache = ""
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
            App.WriteLog(ex.Message);
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

   public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
   {
      private readonly string serializationFormat;

      public TimeOnlyJsonConverter() : this(null) { }

      public TimeOnlyJsonConverter(string? serializationFormat)
      {
         this.serializationFormat = serializationFormat ?? "HH:mm:ss.fff";
      }

      public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
      {
         var value = reader.GetString();
         return TimeOnly.Parse(value!);
      }
      public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
      {
         writer.WriteStringValue(value.ToString(serializationFormat));
      }
   }
}
