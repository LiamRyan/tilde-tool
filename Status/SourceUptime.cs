using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tildetool.Status
{
   internal class SourceUptime : Source
   {
      protected record CacheStruct : IEquatable<CacheStruct>
      {
         public string Status { get; set; }
         public bool Online { get; set; }
         public DateTime Date { get; set; }
      }

      protected string Site;
      protected string URL;
      protected string ParseMethod;
      public SourceUptime(string name, string site, string url, string parseMethod)
         : base("Uptime", name, typeof(CacheStruct))
      {
         Site = site;
         URL = url;
         ParseMethod = parseMethod;
      }

      protected override void _Query()
      {
         CacheStruct cache = Cache as CacheStruct;
         if (cache == null)
            cache = new CacheStruct { Date = DateTime.Now };

         string responseBody = null;
         {
            // Set up an HTTP GET request.
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Site);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, URL);

            // Send it, make sure we get a result.
            Task<HttpResponseMessage> taskGet = httpClient.SendAsync(request);
            taskGet.Wait();

            // Not Modified, we're good!
            if (taskGet.Result.StatusCode == System.Net.HttpStatusCode.NotModified)
               return;

            // Anything besides success, fail out.
            if (!taskGet.Result.IsSuccessStatusCode)
            {
               if (cache.Online)
                  cache.Date = DateTime.Now;
               cache.Online = false;
               cache.Status = taskGet.Result.StatusCode.ToString();
               Cache = cache;
               return;
            }

            // Read the data.
            Task<string> taskRead = taskGet.Result.Content.ReadAsStringAsync();
            taskRead.Wait();
            responseBody = taskRead.Result;
         }

         // Parse the result
         if (ParseMethod.CompareTo("JSON") == 0)
         {
            try
            {
            }
            catch (Exception ex)
            {
               if (cache.Online)
                  cache.Date = DateTime.Now;
               cache.Online = false;
               cache.Status = ex.ToString();
               Cache = cache;
               return;
            }
         }

         // Handle 
         cache.Date = DateTime.Now;
         cache.Online = true;
         cache.Status = "online";
         Cache = cache;
      }
      public override void Display()
      {
         CacheStruct? cache = Cache as CacheStruct;
         if (cache == null)
            return;

         DateTime infoDate = cache.Date;

         // Figure out which bucket it belongs in.
         DateTime now = DateTime.Now;
         TimeSpan delta = now - infoDate;
         if (delta.TotalDays >= 70)
            Status = infoDate.Year.ToString() + "/" + infoDate.Month.ToString() + "/" + infoDate.Day.ToString();
         else if (delta.Days >= 14)
         {
            int weeks = delta.Days / 7;
            Status = weeks.ToString() + " week" + (weeks > 1 ? "s" : "") + " ago";
         }
         else if (delta.Days >= 1)
            Status = delta.Days.ToString() + " day" + (delta.Days > 1 ? "s" : "") + " ago";
         else
         {
            if (delta.Hours >= 1)
               Status = delta.Hours.ToString() + " hour" + (delta.Hours > 1 ? "s" : "") + " ago";
            else
               Status = delta.Minutes.ToString() + " minute" + (delta.Minutes != 1 ? "s" : "") + " ago";
         }

         //
         Article = cache.Status;
         if (cache.Online)
            State = StateType.Success;
         else if (delta.Days < 7)
            State = StateType.Error;
         else
            State = StateType.Inactive;
      }

      public override bool Ephemeral { get { return false; } }
      public override bool Important { get { return State == StateType.Error; } }
      public override string Domain { get { return URL; } }
      public override bool NeedsRefresh(TimeSpan interval) { return interval.TotalHours >= 4.0f; }

      public override void HandleClick()
      {
         Process.Start(new ProcessStartInfo(Site + URL) { UseShellExecute = true });
      }
   }
}
