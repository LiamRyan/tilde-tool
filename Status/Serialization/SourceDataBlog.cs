using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceDataBlog
   {
      public string Title { get; set; }
      public string Site { get; set; }
      public string URL { get; set; }
      public string[] DateLookup { get; set; }
      public string DateFormat { get; set; }

      public Source Spawn()
      {
         return new SourceBlog(Title, Site, URL, DateLookup, DateFormat);
      }
   }
}
