using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceDataUptime : ISourceData
   {
      public string Title { get; set; }
      public string Site { get; set; }
      public string URL { get; set; }
      public string ParseMethod { get; set; }

      public Source Spawn(SourceBundle parent)
      {
         return new SourceUptime(Title, Site, URL, ParseMethod);
      }
   }
}
