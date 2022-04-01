using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Forms;

namespace Tildetool
{
   public class KeyMod
   {
      public const uint None  = 0x0000; //[NONE]
      public const uint Alt   = 0x0001; //ALT
      public const uint Ctrl  = 0x0002; //CTRL
      public const uint Shift = 0x0004; //SHIFT
      public const uint Win   = 0x0008; //WINDOWS
   }
   internal class Hotkey
   {
      [DllImport("user32.dll")]
      private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

      [DllImport("user32.dll")]
      private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

      private const int HOTKEY_BASE_ID = 9000;

      private static Dictionary<int,System.Action<Keys>> HotkeyRegistry = new Dictionary<int,System.Action<Keys>>();

      public static int Register(uint mods, Keys key, System.Action<Keys> action)
      {
         if (HotkeyRegistry.Count == 0)
            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcherThreadFilterMessage;

         int hotkeyId = HOTKEY_BASE_ID + HotkeyRegistry.Count;
         RegisterHotKey(IntPtr.Zero, hotkeyId, mods, (uint)key);
         HotkeyRegistry[hotkeyId] = action;
         return hotkeyId;
      }
      public static void Unregister(int hotkeyId)
      {
         UnregisterHotKey(IntPtr.Zero, hotkeyId);
         HotkeyRegistry.Remove(hotkeyId);

         if (HotkeyRegistry.Count == 0)
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcherThreadFilterMessage;
      }

      private const int WM_HOTKEY = 0x0312;
      private static void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
      {
         if (!handled)
         {
            switch (msg.message)
            {
               case WM_HOTKEY:
                  System.Action<Keys>? action;
                  if (HotkeyRegistry.TryGetValue(msg.wParam.ToInt32(), out action))
                  {
                     int vkey = (msg.lParam.ToInt32() >> 16) & 0xFFFF;
                     action((Keys)vkey);
                  }
                  handled = true;
                  break;
            }
         }
      }
   }
}
