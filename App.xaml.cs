using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using Tildetool.Hotcommand;
using Tildetool.Status;
using Tildetool.Explorer;
using WindowsDesktop;

namespace Tildetool
{
   /// <summary>
        /// Interaction logic for App.xaml
        /// </summary>
   public partial class App : System.Windows.Application
   {
      public static void WriteLog(string? log)
      {
         App.Current.Dispatcher.Invoke(() => Console.WriteLine(log));
      }

      [DllImport("user32.dll", SetLastError = true)]
      static extern int GetWindowLong(IntPtr hWnd, int nIndex);
      [DllImport("user32.dll")]
      static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
      private const int GWL_EX_STYLE = -20;
      private const int WS_EX_TRANSPARENT = 0x00000020;
      private const int WS_EX_TOOLWINDOW = 0x00000080;
      private const int WS_EX_APPWINDOW = 0x00040000;

      public static void PreventAltTab(Window window)
      {
         // Set the TOOLWINDOW and clear the APPWINDOW style flags.
         IntPtr hWindow = new WindowInteropHelper(window).Handle;
         SetWindowLong(hWindow, GWL_EX_STYLE, (GetWindowLong(hWindow, GWL_EX_STYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
      }
      public static void Clickthrough(Window window)
      {
         IntPtr hWindow = new WindowInteropHelper(window).Handle;
         SetWindowLong(hWindow, GWL_EX_STYLE, GetWindowLong(hWindow, GWL_EX_STYLE) | WS_EX_TRANSPARENT);
      }

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
         SourceManager.Instance.Load();
         SourceManager.Instance.LoadCache();
         SourceManager.Instance.StartTick();
         ExplorerManager.Instance.Load();
         ExplorerManager.Instance.WatchFile();

         //Hotkey.Register(KeyMod.Win, Keys.Escape, HotkeyEscape);
         Hotkey.Register(KeyMod.Win, Keys.Insert, HotkeyInsert);
         Hotkey.Register(KeyMod.Win, Keys.Oemtilde, HotkeyTilde);
         Hotkey.Register(KeyMod.Win, Keys.Y, HotkeyStatus);
         Hotkey.Register(KeyMod.Ctrl | KeyMod.Alt, Keys.W, HotkeyLookup);
         Hotkey.Register(KeyMod.Ctrl | KeyMod.Alt, Keys.D, HotkeyDesktop);

         VirtualDesktop.Configure();
         VirtualDesktop.CurrentChanged += (s, e) =>
         {
            Dispatcher.Invoke(() =>
            {
               if (_DesktopIcon == null)
                  HotkeyDesktop(0);
            });
         };

         SourceManager.Instance.SourceChanged += (s, args) =>
            {
               if (!args.CacheChanged && _StatusBar == null)
                  return;
               Dispatcher.Invoke(new Action(() =>
               {
                  if (_StatusBar == null)
                  {
                     _StatusBar = new StatusBar(true);
                     _StatusBar.Closing += (sender, e) => { if (sender != _StatusBar) return; StatusBarAwake?.Invoke(_StatusBar, false); _StatusBar = null; };

                     _StatusBar.Show();
                     _StatusBar.Topmost = true;
                     StatusBarAwake?.Invoke(_StatusBar, true);
                  }
                  else if (!_StatusBar.IsShowing)
                     _StatusBar.AnimateShow();
                  _StatusBar.Dispatcher.Invoke(new Action(() => _StatusBar.UpdateStatusBar(args.Index, args.CacheChanged)));
               }));
            };
         SourceManager.Instance.SourceQuery += (s, args) =>
            {
               bool shouldShow = SourceManager.Instance.Sources.Any(src => !src.Ephemeral && (src.IsQuerying || SourceManager.Instance.NeedRefresh(src)));
               bool isShowing = _StatusProgress != null && !_StatusProgress.Finished;

               if (shouldShow == isShowing)
                  return;

               Dispatcher.Invoke(new Action(() =>
               {
                  if (shouldShow)
                  {
                     _StatusProgress = new StatusProgress();
                     _StatusProgress.Closing += (sender, e) => { _StatusProgress = null; };

                     _StatusProgress.Show();
                     _StatusProgress.Topmost = true;
                  }
                  else
                     _StatusProgress.Cancel();
               }));
            };

         StartWindow window = new StartWindow();
         window.Show();
         window.Topmost = true;
      }

      public delegate void PanelAwake(Window window, bool awake);
      public static StatusBar? StatusBar { get { return _StatusBar; } }
      public static event PanelAwake? StatusBarAwake;


      StatusProgress? _StatusProgress = null;

      static StatusBar? _StatusBar = null;
      protected void HotkeyStatus(Keys keys)
      {
         if (_StatusBar == null)
         {
            _StatusBar = new StatusBar(false);
            _StatusBar.Closing += (sender, e) => { if (sender != _StatusBar) return; StatusBarAwake?.Invoke(_StatusBar, false); _StatusBar = null; };

            _StatusBar.Show();
            _StatusBar.Topmost = true;
            StatusBarAwake?.Invoke(_StatusBar, true);
         }
         else if (_StatusBar.IsShowing)
            _StatusBar.AnimateClose();
         else
         {
            _StatusBar.ClearTimer();
            _StatusBar.AnimateShow();
         }
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
            bool canLoad = ExplorerItemPopup.LoadFolderData();
            if (!canLoad)
               return;

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
            _HotCommandWindow.Closing += (sender, e) => { if (_HotCommandWindow == sender) _HotCommandWindow = null; };

            _HotCommandWindow.Show();
            _HotCommandWindow.Topmost = true;
            _HotCommandWindow.Activate();
         }
         else
            _HotCommandWindow.Cancel();
      }

      WordLookup? _WordLookup = null;
      protected void HotkeyLookup(Keys keys)
      {
         if (_WordLookup == null)
         {
            _WordLookup = new WordLookup();
            _WordLookup.OnFinish += (sender) => { if (_WordLookup == sender) _WordLookup = null; };
            _WordLookup.Closing += (sender, e) => { if (_WordLookup == sender) _WordLookup = null; };

            _WordLookup.Show();
            _WordLookup.Topmost = true;
            _WordLookup.Activate();
         }
         else
            _WordLookup.Cancel();
      }

      DesktopIcon? _DesktopIcon = null;
      protected void HotkeyDesktop(Keys keys)
      {
         if (_DesktopIcon == null)
         {
            _DesktopIcon = new DesktopIcon();
            _DesktopIcon.OnFinish += (sender) => { if (_DesktopIcon == sender) _DesktopIcon = null; };
            _DesktopIcon.Closing += (sender, e) => { if (_DesktopIcon == sender) _DesktopIcon = null; };

            _DesktopIcon.Show();
            _DesktopIcon.Topmost = true;
         }
         else
            _DesktopIcon.Cancel();
      }
   }
}
