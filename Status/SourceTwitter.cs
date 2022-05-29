using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Tildetool.Status
{
   internal class SourceTwitter : Source
   {
      public SourceTwitter(string account)
         : base("TWEET", "@" + account)
      {
         Status = "tbd";
      }

      protected override void _Refresh() { }
      public override bool NeedsRefresh(DateTime lastUpdate) { return false; }
   }
}
