using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Tildetool.Hotcommand.Serialization
{
    public class Cliphotkey
    {
      public string[] Chord { get; set; }
      public string Value { get; set; }

      public Key[] KeyChord;
   }
}
