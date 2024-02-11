using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceDataBlog : ISourceData
   {
      public string Title { get; set; }
      public string Site { get; set; }
      public string Reference { get; set; }
      public int UpdateTimeMin { get; set; } = 120;
      public TimeOnly[] UpdateTimes { get; set; }
      public bool Enabled { get; set; } = true;

      public Source? Spawn(SourceBundle parent)
      {
         if (!parent.Sites.TryGetValue(Site, out SourceBlogSite site))
            return null;
         SourceBlogUrl[] siteUrl = string.IsNullOrEmpty(site.URL) ? site.UrlLookup : new SourceBlogUrl[] { new SourceBlogUrl { URL = site.URL } };
         return new SourceBlog(site, Title, siteUrl, Reference, UpdateTimeMin, UpdateTimes);
      }
   }
}
