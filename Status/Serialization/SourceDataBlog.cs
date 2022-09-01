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

      public Source? Spawn(SourceBundle parent)
      {
         if (!parent.Sites.TryGetValue(Site, out SourceBlogSite site))
            return null;
         string sitePath = site.Site.Replace("@REFERENCE@", Reference);
         SourceBlogUrl[] siteUrl = string.IsNullOrEmpty(site.URL) ? site.UrlLookup : new SourceBlogUrl[] { new SourceBlogUrl { URL = site.URL } };
         string siteOpenToUrl = site.OpenToURL.Replace("@REFERENCE@", Reference);
         return new SourceBlog(site.Tag, Title, sitePath, siteUrl, siteOpenToUrl, site.DateLookup, site.DateFormat, site.TitleLookup, Reference);
      }
   }
}
