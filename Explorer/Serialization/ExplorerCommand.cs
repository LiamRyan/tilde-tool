using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Tildetool.Explorer.Serialization
{
   public class ExplorerCommand
   {
      public bool AsFile { get; set; }
      public string[] Extensions { get; set; }
      public string Title { get; set; }
      public string Hotkey { get; set; }
      public string Command { get; set; }
      public bool InWorkingDir { get; set; }

      public Key HotkeyAsKey { get { return Enum.Parse<Key>(Hotkey, true); } set { Hotkey = value.ToString(); } }
   }
}
