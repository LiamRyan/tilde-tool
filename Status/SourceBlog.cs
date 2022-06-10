using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Tildetool.Status
{
   internal class SourceBlog : Source
   {
      protected string Site;
      protected string Url;
      protected string[] SearchPattern;
      protected string DatePattern;
      public SourceBlog(string title, string name, string site, string url, string[] searchPattern, string datePattern)
         : base(title, name)
      {
         Site = site;
         Url = url;
         SearchPattern = searchPattern;
         DatePattern = datePattern;
      }

      protected override void _Query()
      {
         string responseBody;
         {
            // Set up an HTTP GET request.
            HttpClient httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Site);

            // Send it, make sure we get a result.
            Task<HttpResponseMessage> taskGet = httpClient.GetAsync(Url);
            taskGet.Wait();
            if (!taskGet.Result.IsSuccessStatusCode)
            {
               Status = taskGet.Result.StatusCode.ToString();
               State = StateType.Error;
               Cache = "";
               return;
            }

            // Read the data.
            Task<string> taskRead = taskGet.Result.Content.ReadAsStringAsync();
            taskRead.Wait();
            responseBody = taskRead.Result;
         }

         // TODO: Find some format that lets us detect the format and identify links and their dates
         // Parse the data
         try
         {
            int index = responseBody.IndexOf(SearchPattern[0]);
            while (index != -1)
            {
               // Find the date.
               index += SearchPattern[0].Length;
               int lastIndex = index;
               for (int i = 1; i < SearchPattern.Length; i++)
               {
                  lastIndex = index;
                  index = responseBody.IndexOf(SearchPattern[i], index);
                  if (index == -1)
                     break;
                  if (i + 1 < SearchPattern.Length)
                     index += SearchPattern[i].Length;
               }
               if (index == -1)
                  break;

               // Parse the date.
               string infoTimeStr = responseBody.Substring(lastIndex, index - lastIndex);
               infoTimeStr = infoTimeStr.Replace("PDT", "-7").Replace("PST", "-8");
               infoTimeStr = infoTimeStr.Trim('\t', '\n', '\r');

               DateTime infoDate;
               if (DatePattern == "unix")
                  infoDate = DateTime.UnixEpoch.AddSeconds(int.Parse(infoTimeStr));
               else
                  infoDate = DateTime.ParseExact(infoTimeStr, DatePattern, CultureInfo.CreateSpecificCulture("en-us"));

               // Store it for future use.
               Cache = infoTimeStr;
               break;

               //
               index = responseBody.IndexOf(SearchPattern[0], index);
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex.Message);
            Status = "error";
            State = StateType.Error;
            Cache = "";
         }
      }
      public override void Display()
      {
         if (string.IsNullOrEmpty(Cache))
            return;

         DateTime infoDate;
         if (DatePattern == "unix")
            infoDate = DateTime.UnixEpoch.AddSeconds(int.Parse(Cache));
         else
            infoDate = DateTime.ParseExact(Cache, DatePattern, CultureInfo.CreateSpecificCulture("en-us"));

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
      }

      public override bool Ephemeral { get { return false; } }
      public override bool NeedsRefresh(TimeSpan interval) { return interval.TotalHours >= 18.0f; }
   }
}
