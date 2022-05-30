using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceCacheData
   {
      public string Status { get; set; }
      public Source.StateType State { get; set; }
      public DateTime LastUpdate { get; set; }
      public string LastCache { get; set; }
   }
}
