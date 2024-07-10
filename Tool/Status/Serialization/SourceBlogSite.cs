using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceBlogLookup
   {
      public bool Forward { get; set; } = true;
      public string[] Path { get; set; }
   }
   public class SourceBlogUrl
   {
      public string URL { get; set; }
      public SourceBlogLookup Lookup { get; set; }
   }
   public class SourceBlogSite
   {
      public string Tag { get; set; }
      public string Site { get; set; }
      public string URL { get; set; }
      public SourceBlogUrl[] UrlLookup { get; set; }
      public string? OpenToURL { get; set; }
      public string? OpenCommand { get; set; }
      public string[] OpenArgumentList { get; set; }
      public SourceBlogLookup DateLookup { get; set; }
      public string DateFormat { get; set; }
      public string? DateTimeZone { get; set; }
      public SourceBlogLookup TitleLookup { get; set; }
      public SourceBlogLookup OpenToLookup { get; set; }
   }
}
