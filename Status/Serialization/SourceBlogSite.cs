using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceBlogSite
   {
      public string Tag { get; set; }
      public string Site { get; set; }
      public string URL { get; set; }
      public string OpenToURL { get; set; }
      public string[] DateLookup { get; set; }
      public string DateFormat { get; set; }
      public string[] TitleLookup { get; set; }
   }
}
