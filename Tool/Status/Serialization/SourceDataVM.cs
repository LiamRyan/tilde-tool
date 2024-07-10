using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public class SourceDataVM : ISourceData
   {
      public string Title { get; set; }
      public string VboxPath { get; set; }
      public string VmName { get; set; }
      public string VmIp { get; set; }

      public Source Spawn(SourceBundle parent)
      {
         return new SourceVM(Title, VboxPath, VmName, VmIp);
      }
   }
}
