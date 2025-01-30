using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Tildetool.Status.Serialization;

namespace Tildetool.Status
{
   internal class SourceBlog : Source
   {
      #region Cached Data

      protected record CacheStruct : IEquatable<CacheStruct>
      {
         public DateTime Date { get; set; }
         public string Title { get; set; }
         public string OpenTo { get; set; }
         public string? Etag { get; set; }
         public DateTimeOffset? LastModified { get; set; }
      }

      #endregion
      #region Json Data

      protected string Site;
      protected SourceBlogUrl[] Url;
      protected string? OpenToUrl;
      protected string? OpenCommand;
      protected string[] OpenArgumentList;
      protected SourceBlogLookup DateLookup;
      protected string DateFormat;
      protected TimeZoneInfo? DateTimeZone;
      protected SourceBlogLookup TitleLookup;
      protected SourceBlogLookup OpenToLookup;
      protected string Reference;
      protected float UpdateTimeMin;
      protected TimeOnly[] UpdateTimes;
      public SourceBlog(SourceBlogSite site, string name, SourceBlogUrl[] url, string reference, float updateIntervalMin, TimeOnly[] updateTimes)
         : base(site.Tag, name, typeof(CacheStruct))
      {
         Reference = reference;
         Site = site.Site.Replace("@REFERENCE@", Reference);
         Url = url;
         OpenToUrl = site.OpenToURL?.Replace("@REFERENCE@", Reference);
         OpenCommand = site.OpenCommand;
         OpenArgumentList = site.OpenArgumentList?.Select(arg => arg.Replace("@REFERENCE@", Reference))?.ToArray();
         DateLookup = site.DateLookup;
         DateFormat = site.DateFormat;
         DateTimeZone = !string.IsNullOrEmpty(site.DateTimeZone) ? TimeZoneInfo.FindSystemTimeZoneById(site.DateTimeZone) : null;
         TitleLookup = site.TitleLookup;
         OpenToLookup = site.OpenToLookup;
         UpdateTimeMin = updateIntervalMin;
         UpdateTimes = updateTimes;
      }

      #endregion
      #region Temporary Data

      bool IsOffline = false;

      #endregion
      #region Implementation

      protected override void _Query()
      {
         //
         string responseBody = null;
         string lookupData(SourceBlogLookup lookup)
         {
            try
            {
               int index;
               if (lookup.Forward)
                  index = responseBody.IndexOf(lookup.Path[0]);
               else
                  index = responseBody.LastIndexOf(lookup.Path[0]);
               if (index == -1)
                  App.WriteLog($"lookupData unable to find initial value \"{lookup.Path[0]}\"");
               while (index != -1)
               {
                  // Find the date.
                  index += lookup.Path[0].Length;
                  int lastIndex = index;
                  for (int i = 1; i < lookup.Path.Length; i++)
                  {
                     lastIndex = index;
                     index = responseBody.IndexOf(lookup.Path[i], index);
                     if (index == -1)
                     {
                        App.WriteLog($"lookupData unable to find value {i} \"{lookup.Path[i]}\" from {lastIndex}");
                        break;
                     }
                     if (i + 1 < lookup.Path.Length)
                        index += lookup.Path[i].Length;
                  }
                  if (index == -1)
                     break;

                  // Parse the date.
                  string infoTimeStr = responseBody.Substring(lastIndex, index - lastIndex);
                  return infoTimeStr;
               }
            }
            catch (Exception ex)
            {
               App.WriteLog(ex.Message);
            }
            return null;
         }

         CacheStruct cache = Cache as CacheStruct;
         if (cache == null)
            cache = new CacheStruct { Date = DateTime.Now };
         string oldTitle = cache.Title;
         IsOffline = false;

         bool isValid = true;

         if (SourceBlogTest.sUseTest)
            responseBody = SourceBlogTest.sTestResponseBody;
         else
         {
            bool isFirst = true;
            string curLookup = Reference;
            foreach (SourceBlogUrl urlLink in Url)
            {
               string url = urlLink.URL.Replace("@REFERENCE@", Reference).Replace("@LOOKUP@", curLookup);

               // Set up an HTTP GET request.
               HttpClient httpClient = new HttpClient();
               httpClient.BaseAddress = new Uri(Site);
               httpClient.Timeout = TimeSpan.FromSeconds(15.0f);
               HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
               if (isFirst)
               {
                  if (cache.Etag != null)
                     request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(cache.Etag));
                  else if (cache.LastModified != null)
                     request.Headers.IfModifiedSince = cache.LastModified;
               }

               // Send it, make sure we get a result.
               Task<HttpResponseMessage> taskGet = httpClient.SendAsync(request);
               try
               {
                  taskGet.Wait();
               }
               catch (SocketException socketEx)
               {
                  App.WriteLog(socketEx.Message);

                  IsOffline = true;
                  Status = "no connection";
                  State = StateType.Error;
                  return;
               }
               catch (HttpRequestException httpRequestEx)
               {
                  App.WriteLog(httpRequestEx.Message);

                  IsOffline = true;
                  Status = "no connection";
                  State = StateType.Error;
                  return;
               }
               catch (Exception ex)
               {
                  if (ex.InnerException is HttpRequestException httpEx)
                  {
                     if (httpEx.StatusCode == null)
                     {
                        IsOffline = true;
                        Status = "no connection";
                        State = StateType.Error;
                        return;
                     }
                     ex = httpEx;
                  }
                  if (ex.InnerException is SocketException socketEx)
                  {
                     IsOffline = true;
                     Status = "no connection";
                     State = StateType.Error;
                     return;
                  }

                  App.WriteLog(ex.Message);

                  Status = "offline";
                  State = StateType.Error;
                  Cache = null;
                  return;
               }

               // Not Modified, we're good!
               if (taskGet.Result.StatusCode == System.Net.HttpStatusCode.NotModified)
                  return;

               // Anything besides success, fail out.
               if (!taskGet.Result.IsSuccessStatusCode)
               {
                  App.WriteLog(taskGet.Result.ToString());
                  Status = taskGet.Result.StatusCode.ToString();
                  State = StateType.Error;
                  Cache = null;
                  return;
               }

               //
               if (isFirst)
               {
                  cache.Etag = taskGet.Result.Headers.ETag?.ToString();
                  cache.LastModified = taskGet.Result.Headers.Date;
                  isFirst = false;
               }

               // Read the data.
               Task<string> taskRead = taskGet.Result.Content.ReadAsStringAsync();
               taskRead.Wait();
               responseBody = taskRead.Result;

               // If we have a lookup for the next url, deal with it.
               if (urlLink.Lookup != null && urlLink.Lookup.Path.Length > 0)
               {
                  try
                  {
                     curLookup = lookupData(urlLink.Lookup);
                  }
                  catch (Exception ex)
                  {
                     App.WriteLog(ex.Message);
                     isValid = false;
                  }
               }
            }
         }

         // Figure out the date
         bool hasDate = DateLookup != null && DateLookup.Path.Length > 0;
         if (hasDate)
         {
            string infoTimeStr = null;
            try
            {
               infoTimeStr = lookupData(DateLookup);

               // Parse it.
               if (!string.IsNullOrEmpty(infoTimeStr))
               {
                  // do some preprocessing to standardize some dates
                  infoTimeStr = infoTimeStr.Replace("PDT", "-7").Replace("PST", "-8").Replace("GMT", "+0");
                  infoTimeStr = infoTimeStr.Trim('\t', '\n', '\r');

                  // parse
                  if (DateFormat == "unix")
                     cache.Date = DateTime.UnixEpoch.AddSeconds(int.Parse(infoTimeStr)).ToLocalTime();
                  else
                     cache.Date = DateTime.ParseExact(infoTimeStr, DateFormat, CultureInfo.CreateSpecificCulture("en-us"));

                  // convert to local time
                  if (DateTimeZone != null)
                     cache.Date = TimeZoneInfo.ConvertTimeToUtc(cache.Date, DateTimeZone).ToLocalTime();
               }
               else
                  isValid = false;
            }
            catch (Exception ex)
            {
               App.WriteLog(ex.Message);
               App.WriteLog($"  string was {infoTimeStr} vs {DateFormat}");
               isValid = false;
            }
         }

         // Figure out the title.
         bool hasTitle = TitleLookup != null && TitleLookup.Path.Length > 0;
         if (hasTitle)
         {
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
               App.WriteLog(ex.Message);
               isValid = false;
            }
         }

         // Figure out the link.
         bool hasOpenTo = OpenToLookup != null && OpenToLookup.Path.Length > 0;
         if (hasOpenTo)
         {
            try
            {
               // Pull it out and store it.
               string openToStr = lookupData(OpenToLookup);
               if (!string.IsNullOrEmpty(openToStr))
               {
                  if (!string.IsNullOrEmpty(OpenToUrl))
                     cache.OpenTo = OpenToUrl.Replace("@OPENTO@", openToStr);
                  else
                     cache.OpenTo = openToStr;
               }
               else
                  isValid = false;
            }
            catch (Exception ex)
            {
               App.WriteLog(ex.Message);
               isValid = false;
            }
         }

         if (isValid)
         {
            // If we don't have a date lookup and the title changed, set the date to now.
            if (!hasDate)
               if (Cache == null || string.IsNullOrEmpty(oldTitle) || string.Compare(oldTitle, cache.Title) != 0)
                  cache.Date = DateTime.Now;

            // If we don't have a title lookup, use a default
            if (!hasTitle)
               cache.Title = "Updated " + cache.Date.ToString();

            // If we don't have a link lookup, use default.
            if (!hasOpenTo)
               cache.OpenTo = OpenToUrl;

            // Store it for future use.
            Cache = cache;
         }
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
         if (IsOffline)
            return;

         DateTime infoDate = cache.Date;

         // Figure out which bucket it belongs in.
         DateTime now = DateTime.Now;
         TimeSpan delta = now - infoDate;
         if (delta.TotalDays >= 70)
         {
            Status = infoDate.Year.ToString() + "/" + infoDate.Month.ToString() + "/" + infoDate.Day.ToString() + (DateLookup != null && DateLookup.Path.Length > 0 ? "" : "?");
            State = StateType.Inactive;
         }
         else if (delta.Days >= 14)
         {
            int weeks = delta.Days / 7;
            Status = weeks.ToString() + " week" + (weeks > 1 ? "s" : "") + " ago" + (DateLookup != null && DateLookup.Path.Length > 0 ? "" : "?");
            State = StateType.Inactive;
         }
         else if (delta.Days >= 1)
         {
            Status = delta.Days.ToString() + " day" + (delta.Days > 1 ? "s" : "") + " ago" + (DateLookup != null && DateLookup.Path.Length > 0 ? "" : "?");
            State = StateType.Alert;
         }
         else
         {
            if (delta.Hours >= 1)
               Status = delta.Hours.ToString() + " hour" + (delta.Hours > 1 ? "s" : "") + " ago" + (DateLookup != null && DateLookup.Path.Length > 0 ? "" : "?");
            else
               Status = "new";
            State = StateType.Success;
         }

         Article = cache.Title;
      }

      public override bool IsFeed { get { return true; } }
      public override bool Ephemeral { get { return false; } }
      public override bool Important { get { return State != StateType.Inactive; } }
      public override int Order { get { if (Cache == null) return 0; return (int)(DateTime.Now - (Cache as CacheStruct).Date).TotalSeconds; } }
      public override string Domain { get { return Site; } }
      public override bool NeedsRefresh(DateTime lastUpdate, TimeSpan interval)
      {
         if (IsOffline)
            return interval.TotalSeconds >= 60.0f;

         if (UpdateTimes == null || UpdateTimes.Length == 0)
         {
            // check if we're in a different bucket
            int lastUpdateIndex = (int)Math.Floor(lastUpdate.TimeOfDay.TotalMinutes / UpdateTimeMin);
            int nowIndex = (int)Math.Floor(DateTime.Now.TimeOfDay.TotalMinutes / UpdateTimeMin);

            if (lastUpdateIndex != nowIndex)
               return true;

            // check if we're in the same bucket for a different day.
            DateTime lastUpdateBucket = lastUpdate.Date.AddMinutes(lastUpdateIndex * UpdateTimeMin);
            if ((DateTime.Now - lastUpdateBucket).TotalHours >= 24.0f)
               return true;
         }
         else
         {
            // check if we're in a different bucket
            TimeOnly lastUpdateTime = TimeOnly.FromDateTime(lastUpdate);
            int lastUpdateIndex = Enumerable.Range(0, UpdateTimes.Length).FirstOrDefault(i => lastUpdateTime < UpdateTimes[i], -1);
            TimeOnly nowTime = TimeOnly.FromDateTime(DateTime.Now);
            int nowIndex = Enumerable.Range(0, UpdateTimes.Length).FirstOrDefault(i => nowTime < UpdateTimes[i], -1);

            if (lastUpdateIndex != nowIndex)
               return true;

            // check if we're in the same bucket for a different day.
            TimeOnly lastUpdateBucketTime = UpdateTimes[(lastUpdateIndex + UpdateTimes.Length) % UpdateTimes.Length];
            DateTime lastUpdateBucket = lastUpdate.Date + lastUpdateBucketTime.ToTimeSpan();
            if ((DateTime.Now - lastUpdateBucket).TotalHours >= 24.0f)
               return true;
         }

         return false;
      }

      public override void HandleClick()
      {
         string? openTo = "";
         CacheStruct? cache = Cache as CacheStruct;
         if (cache != null)
            openTo = cache.OpenTo ?? "";

         if (!string.IsNullOrEmpty(OpenCommand))
         {
            Thread trd = new Thread(new ThreadStart(() =>
            {
               try
               {
                  Process process = new Process();
                  ProcessStartInfo startInfo = new ProcessStartInfo();
                  startInfo.FileName = OpenCommand.Replace("@OPENTO@", openTo);
                  if (OpenArgumentList != null)
                  {
                     foreach (string argument in OpenArgumentList)
                        startInfo.ArgumentList.Add(argument.Replace("@OPENTO@", openTo));
                  }
                  process.StartInfo = startInfo;
                  process.Start();
                  process.Dispose();
               }
               catch (Exception ex)
               {
                  MessageBox.Show(ex.ToString());
                  App.WriteLog(ex.ToString());
               }
            }));
            trd.IsBackground = true;
            trd.Start();
         }
         else if (!string.IsNullOrEmpty(openTo) && (openTo.StartsWith("http://") || openTo.StartsWith("https://")))
            Process.Start(new ProcessStartInfo(openTo) { UseShellExecute = true });
      }

      #endregion
   }
}
