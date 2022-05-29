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
      public SourceBlog(string name, string site, string url, string[] searchPattern, string datePattern)
         : base("BLOG", name)
      {
         Site = site;
         Url = url;
         SearchPattern = searchPattern;
         DatePattern = datePattern;
      }

      protected override void _Refresh()
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
               int lastIndex = 0;
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
               DateTime infoDate = DateTime.ParseExact(infoTimeStr, DatePattern, CultureInfo.CreateSpecificCulture("en-us"));

               // Figure out which bucket it belongs in.
               DateTime now = DateTime.Now;
               TimeSpan delta = now - infoDate;
               if (delta.Days >= 14)
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
                  Status = "Today";
                  State = StateType.Success;
               }
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
         }
      }

      public override bool NeedsRefresh(DateTime lastUpdate) { return (DateTime.Now - lastUpdate).TotalHours >= 18.0f; }
   }
}
