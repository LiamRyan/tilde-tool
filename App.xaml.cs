using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using Tildetool.Hotcommand;

namespace Tildetool
{
   /// <summary>
        /// Interaction logic for App.xaml
        /// </summary>
   public partial class App : System.Windows.Application
   {
      private void Main(object sender, StartupEventArgs e)
      {
         OnStartup(e);
      }
      private void Close(object target, ExecutedRoutedEventArgs e)
      {
         Shutdown();
      }

      private TaskbarIcon? AppNotifyIcon;
      protected override void OnStartup(StartupEventArgs e)
      {
         AppNotifyIcon = (TaskbarIcon)FindResource("AppNotifyIcon");

         HotcommandManager.Instance.Load();
         HotcommandManager.Instance.LoadUsage();
         HotcommandManager.Instance.WatchFile();

         Hotkey.Register(KeyMod.Win, Keys.Escape, HotkeyEscape);
         Hotkey.Register(KeyMod.Win, Keys.Insert, HotkeyInsert);
         Hotkey.Register(KeyMod.Win, Keys.Oemtilde, HotkeyTilde);

         StartWindow window = new StartWindow();
         window.Show();
         window.Topmost = true;
         window.Activate();
      }

      AppPaneWindow? _AppPaneWindow = null;
      protected void HotkeyEscape(Keys keys)
      {
         if (_AppPaneWindow == null)
         {
            _AppPaneWindow = new AppPaneWindow();
            _AppPaneWindow.Closing += (sender, e) => { _AppPaneWindow = null; };

            _AppPaneWindow.Show();
            _AppPaneWindow.Topmost = true;
            _AppPaneWindow.Activate();
         }
      }

      ExplorerItemPopup? _ExplorerItemPopup = null;
      protected void HotkeyInsert(Keys keys)
      {
         if (_ExplorerItemPopup == null)
         {
            _ExplorerItemPopup = new ExplorerItemPopup();
            _ExplorerItemPopup.OnFinish += (sender) => { if (_ExplorerItemPopup == sender) _ExplorerItemPopup = null; };
            _ExplorerItemPopup.Closing += (sender, e) => { if (_ExplorerItemPopup == sender) _ExplorerItemPopup = null; };

            System.Drawing.Point pos = System.Windows.Forms.Cursor.Position;
            _ExplorerItemPopup.Left = pos.X - (_ExplorerItemPopup.Width / 2);
            _ExplorerItemPopup.Top = pos.Y - (_ExplorerItemPopup.Height / 2);

            _ExplorerItemPopup.Show();
            _ExplorerItemPopup.Topmost = true;
            _ExplorerItemPopup.Activate();
         }
         else
            _ExplorerItemPopup.Cancel();
      }

      HotCommandWindow? _HotCommandWindow = null;
      protected void HotkeyTilde(Keys keys)
      {
         if (_HotCommandWindow == null)
         {
            _HotCommandWindow = new HotCommandWindow();
            _HotCommandWindow.OnFinish += (sender) => { if (_HotCommandWindow == sender) _HotCommandWindow = null; };
            _HotCommandWindow.Closing += (sender, e) => { _HotCommandWindow = null; };

            _HotCommandWindow.Show();
            _HotCommandWindow.Topmost = true;
            _HotCommandWindow.Activate();
         }
         else
            _HotCommandWindow.Cancel();
      }
   }
}
