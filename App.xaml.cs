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
using Tildetool.Time;
using VirtualDesktopApi;
using System.Diagnostics;
using System.Threading;
using System.Windows.Media;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tildetool
{
   /// <summary>
   /// Interaction logic for App.xaml
   /// </summary>
   public partial class App : System.Windows.Application
   {
      static FileStream LogOut;

      public static double GetBarTop(double barHeight) => Screen.PrimaryScreen.WorkingArea.Top + (0.1 * Screen.PrimaryScreen.WorkingArea.Height);

      public static void WriteLog(string? log)
      {
         App.Current.Dispatcher.Invoke(() =>
         {
            Debug.Write(log + "\n");

            using (LogOut = File.Open("Out.txt", FileMode.Append, FileAccess.Write))
            {
               byte[] utf8 = Encoding.UTF8.GetBytes(log + "\n");
               LogOut.Write(utf8);
            }
         });
      }

      public enum BeepSound
      {
         Wake,
         Accept,
         Cancel,
         Notify
      }

      static Dictionary<BeepSound, MediaPlayer> MediaPlayer;
      public static void PlayBeep(BeepSound sound)
      {
         MediaPlayer[sound].Position = TimeSpan.Zero;
         MediaPlayer[sound].Play();
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

      private void OnStartup(object sender, StartupEventArgs e)
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

         MediaPlayer = new Dictionary<BeepSound, MediaPlayer>()
            { { BeepSound.Wake, new MediaPlayer() }, { BeepSound.Accept, new MediaPlayer() }, { BeepSound.Cancel, new MediaPlayer() }, { BeepSound.Notify, new MediaPlayer() } };
         MediaPlayer[BeepSound.Wake].Open(  new Uri("Resource\\dingG.mp3", UriKind.Relative));
         MediaPlayer[BeepSound.Accept].Open(new Uri("Resource\\dingC.mp3", UriKind.Relative));
         MediaPlayer[BeepSound.Cancel].Open(new Uri("Resource\\dingA.mp3", UriKind.Relative));
         MediaPlayer[BeepSound.Notify].Open(new Uri("Resource\\beepC.mp3", UriKind.Relative));

         HotcommandManager.Instance.Load();
         HotcommandManager.Instance.LoadUsage();
         HotcommandManager.Instance.WatchFile();
         SourceManager.Instance.Load();
         SourceManager.Instance.LoadCache();
         SourceManager.Instance.StartTick();
         ExplorerManager.Instance.Load();
         ExplorerManager.Instance.WatchFile();
         TimeManager.Instance.LoadCache();
         TimeManager.Instance.ConnectSqlite();
         TimeManager.Instance.StartTick();

         Hotkey.Register(KeyMod.Win, Keys.Insert, HotkeyInsert);
         Hotkey.Register(KeyMod.Ctrl, Keys.Oemtilde, HotkeyTilde);

         VirtualDesktopManager.Initialize();

         SourceManager.Instance.SourceChanged += (s, args) =>
            {
               if (_StatusBar == null)
                  if (!args.CacheChanged || SourceManager.Instance.Sources[args.Index].Silent)
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
                  _StatusBar.Dispatcher.Invoke(new Action(() => _StatusBar.UpdatePanel(args.Index, args.CacheChanged)));

                  if (_StatusProgress != null)
                     _StatusProgress.Dispatcher.Invoke(new Action(() => _StatusProgress.Update()));
               }));
            };
         SourceManager.Instance.SourceQuery += (s, args) =>
            {
               bool shouldShow = SourceManager.Instance.Sources.Any(src => !src.Ephemeral && (src.IsQuerying || SourceManager.Instance.NeedRefresh(src)));
               Dispatcher.Invoke(new Action(() =>
               {
                  if (shouldShow)
                  {
                     if (_StatusProgress == null)
                     {
                        _StatusProgress = new StatusProgress();
                        _StatusProgress.Closing += (sender, e) => { if (sender == _StatusProgress) _StatusProgress = null; };

                        _StatusProgress.Show();
                        _StatusProgress.Topmost = true;
                     }
                     else
                        _StatusProgress.Dispatcher.Invoke(new Action(() => _StatusProgress.Update()));
                  }
                  else if (_StatusProgress != null)
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
      public void HotkeyStatus(Keys keys = Keys.None)
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
         {
            _StatusBar.AnimateClose();
            App.PlayBeep(App.BeepSound.Cancel);
         }
         else
         {
            _StatusBar.ClearTimer();
            _StatusBar.AnimateShow();
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

      Timekeep? _Timekeep = null;
      public void ShowTimekeep(bool temporary)
      {
         if (_Timekeep != null)
            return;
         _Timekeep = new Timekeep();
         _Timekeep.OnFinish += (sender) => { if (_Timekeep == sender) _Timekeep = null; };
         _Timekeep.Closing += (sender, e) => { if (_Timekeep == sender) _Timekeep = null; };

         _Timekeep.Show();
         _Timekeep.Topmost = true;
         _Timekeep.Activate();

         if (temporary)
            _Timekeep.ScheduleCancel();
      }
      public void HotkeyTimekeep(Keys keys = Keys.None)
      {
         if (_Timekeep == null)
         {
            ShowTimekeep(false);
            App.PlayBeep(App.BeepSound.Wake);
         }
         else
         {
            _Timekeep.Cancel();
            App.PlayBeep(App.BeepSound.Cancel);
         }
      }

      WordLookup? _WordLookup = null;
      public void HotkeyLookup(Keys keys = Keys.None)
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
   }
}
