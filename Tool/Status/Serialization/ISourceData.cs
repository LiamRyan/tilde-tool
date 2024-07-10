using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool.Status.Serialization
{
   public interface ISourceData
   {
      Source Spawn(SourceBundle parent);
   }
}
