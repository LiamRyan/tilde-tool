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
      public SourceBlog(string name, string site, string url)
         : base("BLOG", name)
      {
         Site = site;
         Url = url;
      }

      protected override void _Refresh()
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
         string responseBody = taskRead.Result;

         // TODO: Find some format that lets us detect the format and identify links and their dates
         Status = "online";
         State = StateType.Success;
      }

      public override bool NeedsRefresh(DateTime lastUpdate) { return (DateTime.Now - lastUpdate).TotalHours >= 18.0f; }
   }
}
