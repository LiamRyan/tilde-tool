using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceBundle
   {
      public Dictionary<string, SourceBlogSite> Sites { get; set; }
      public SourceDataBlog[] DataBlogs { get; set; }
      public SourceDataVM[] DataVMs { get; set; }
   }
}
