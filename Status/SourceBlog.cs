using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Tildetool.Status
{
   internal class SourceBlog : Source
   {
      protected record CacheStruct : IEquatable<CacheStruct>
      {
         public DateTime Date { get; set; }
         public string Title { get; set; }
         public string? Etag { get; set; }
         public DateTimeOffset? LastModified { get; set; }
      }

      protected string Site;
      protected string Url;
      protected string OpenToUrl;
      protected string[] DateLookup;
      protected string DateFormat;
      protected string[] TitleLookup;
      public SourceBlog(string title, string name, string site, string url, string openToUrl, string[] dateLookup, string dateFormat, string[] titleLookup)
         : base(title, name, typeof(CacheStruct))
      {
         Site = site;
         Url = url;
         OpenToUrl = openToUrl;
         DateLookup = dateLookup;
         DateFormat = dateFormat;
         TitleLookup = titleLookup;
      }

      protected override void _Query()
      {
         CacheStruct cache = Cache as CacheStruct;
         if (cache == null)
            cache = new CacheStruct();

         string responseBody;
         if (SourceBlogTest.sUseTest)
            responseBody = SourceBlogTest.sTestResponseBody;
         else
         {
            // Set up an HTTP GET request.
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Site);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, Url);
            if (cache.Etag != null)
               request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(cache.Etag));
            else if (cache.LastModified != null)
               request.Headers.IfModifiedSince = cache.LastModified;

            // Send it, make sure we get a result.
            Task<HttpResponseMessage> taskGet = httpClient.SendAsync(request);
            taskGet.Wait();

            // Not Modified, we're good!
            if (taskGet.Result.StatusCode == System.Net.HttpStatusCode.NotModified)
               return;

            // Anything besides success, fail out.
            if (!taskGet.Result.IsSuccessStatusCode)
            {
               Status = taskGet.Result.StatusCode.ToString();
               State = StateType.Error;
               Cache = null;
               return;
            }

            //
            cache.Etag = taskGet.Result.Headers.ETag?.ToString();
            cache.LastModified = taskGet.Result.Headers.Date;

            // Read the data.
            Task<string> taskRead = taskGet.Result.Content.ReadAsStringAsync();
            taskRead.Wait();
            responseBody = taskRead.Result;
         }

         //
         string lookupData(string[] lookup)
         {
            try
            {
               int index = responseBody.IndexOf(lookup[0]);
               while (index != -1)
               {
                  // Find the date.
                  index += lookup[0].Length;
                  int lastIndex = index;
                  for (int i = 1; i < lookup.Length; i++)
                  {
                     lastIndex = index;
                     index = responseBody.IndexOf(lookup[i], index);
                     if (index == -1)
                        break;
                     if (i + 1 < lookup.Length)
                        index += lookup[i].Length;
                  }
                  if (index == -1)
                     break;

                  // Parse the date.
                  string infoTimeStr = responseBody.Substring(lastIndex, index - lastIndex);
                  return infoTimeStr;

                  //
                  index = responseBody.IndexOf(lookup[0], index);
               }
            }
            catch (Exception ex)
            {
               Console.WriteLine(ex.Message);
            }
            return null;
         }

         bool isValid = true;

         // Figure out the date
         try
         {
            string infoTimeStr = lookupData(DateLookup);

            // Parse it.
            if (!string.IsNullOrEmpty(infoTimeStr))
            {
               // do some preprocessing to standardize some dates
               infoTimeStr = infoTimeStr.Replace("PDT", "-7").Replace("PST", "-8");
               infoTimeStr = infoTimeStr.Trim('\t', '\n', '\r');

               // parse
               if (DateFormat == "unix")
                  cache.Date = DateTime.UnixEpoch.AddSeconds(int.Parse(infoTimeStr));
               else
                  cache.Date = DateTime.ParseExact(infoTimeStr, DateFormat, CultureInfo.CreateSpecificCulture("en-us"));
            }
            else
               isValid = false;
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex.Message);
            isValid = false;
         }

         // Figure out the title.
         try
         {
            // Pull it out and store it.
            string titleStr = lookupData(TitleLookup);
            if (!string.IsNullOrEmpty(titleStr))
               cache.Title = titleStr;
            else
               isValid = false;
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex.Message);
            isValid = false;
         }

         // Store it for future use.
         if (isValid)
            Cache = cache;
         else
         {
            Cache = null;
            Status = "error";
            State = StateType.Error;
         }
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
         {
            Status = infoDate.Year.ToString() + "/" + infoDate.Month.ToString() + "/" + infoDate.Day.ToString();
            State = StateType.Inactive;
         }
         else if (delta.Days >= 14)
         {
            int weeks = delta.Days / 7;
            Status = weeks.ToString() + " week" + (weeks > 1 ? "s" : "") + " ago";
            State = StateType.Inactive;
         }
         else if (delta.Days >= 1)
         {
            Status = delta.Days.ToString() + " day" + (delta.Days > 1 ? "s" : "") + " ago";
            State = StateType.Alert;
         }
         else
         {
            if (delta.Hours >= 1)
               Status = delta.Hours.ToString() + " hour" + (delta.Hours > 1 ? "s" : "") + " ago";
            else
               Status = "new";
            State = StateType.Success;
         }

         Article = cache.Title;
      }

      public override bool Ephemeral { get { return false; } }
      public override string Domain { get { return Site; } }
      public override bool NeedsRefresh(TimeSpan interval) { return interval.TotalHours >= 2.0f; }

      public override void HandleClick()
      {
         if (OpenToUrl.StartsWith("http://") || OpenToUrl.StartsWith("https://"))
            Process.Start(new ProcessStartInfo(OpenToUrl) { UseShellExecute = true });
      }
   }
}
