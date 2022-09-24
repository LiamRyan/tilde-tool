using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Tildetool.Hotcommand;
using System.Runtime.InteropServices;
using System.Timers;
using System.Threading;
using System.Runtime.CompilerServices;
using Tildetool.Hotcommand.Serialization;
using System.Reflection;

namespace Tildetool
{
   /// <summary>
   /// Interaction logic for HotCommandWindow.xaml
   /// </summary>
   public partial class HotCommandWindow : Window
   {
      #region Events

      public delegate void PopupEvent(object sender);
      public event PopupEvent? OnFinish;

      #endregion

      private const int WH_JOURNALRECORD = 0;
      private const int WH_JOURNALPLAYBACK = 1;
      private const int WH_KEYBOARD = 2;
      private const int WH_GETMESSAGE = 3;
      private const int WH_CALLWNDPROC = 4;
      private const int WH_CBT = 5;
      private const int WH_SYSMSGFILTER = 6;
      private const int WH_MOUSE = 7;
      private const int WH_HARDWARE = 8;
      private const int WH_DEBUG = 9;
      private const int WH_SHELL = 10;
      private const int WH_FOREGROUNDIDLE = 11;
      private const int WH_CALLWNDPROCRET = 12;
      private const int WH_KEYBOARD_LL = 13;
      private const int WH_MOUSE_LL = 14;

      private delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);

      [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
      public static extern IntPtr GetModuleHandle(string lpModuleName);

      [DllImport("user32.dll", EntryPoint = "SetWindowsHookEx", SetLastError = true)]
      static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

      [DllImport("User32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
      private static extern byte UnhookWindowsHookEx(IntPtr hHook);

      [DllImport("user32.dll")]
      static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);


      [StructLayout(LayoutKind.Sequential)]
      public struct RECT
      {
         public int left;
         public int top;
         public int right;
         public int bottom;
      }

      [DllImport("user32.dll", SetLastError = true)]
      static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

      [DllImport("user32.dll")]
      static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

      private const int SW_HIDE = 0;
      private const int SW_SHOWNORMAL = 1;
      private const int SW_SHOWMINIMIZED = 2;
      private const int SW_SHOWMAXIMIZED = 3;
      private const int SW_SHOWNOACTIVATE = 4;
      private const int SW_SHOW = 5;
      private const int SW_MINIMIZE = 6;
      private const int SW_SHOWMINNOACTIVE = 7;
      private const int SW_SHOWNA = 8;
      private const int SW_RESTORE = 9;
      private const int SW_SHOWDEFAULT = 10;
      private const int SW_FORCEMINIMIZE = 11;

      private const int SWP_NOSIZE = 0x0001;
      private const int SWP_NOMOVE = 0x0002;
      private const int SWP_NOZORDER = 0x0004;
      private const int SWP_SHOWWINDOW = 0x0040;

      private const int WM_KEYDOWN = 0x0100;
      private const int WM_KEYUP = 0x0101;

      MediaPlayer _MediaPlayer = new MediaPlayer();

      public HotCommandWindow()
      {
         Width = System.Windows.SystemParameters.PrimaryScreenWidth;

         InitializeComponent();
         CommandBox.Opacity = 0;
         CommandEntry.Text = "";
         CommandPreviewPre.Text = "";
         CommandPreviewPost.Text = "";
         CommandExpand.Text = "";

         CommandContext.Visibility = (HotcommandManager.Instance.CurrentContext.Name == "DEFAULT") ? Visibility.Collapsed : Visibility.Visible;
         CommandContext.Text = HotcommandManager.Instance.CurrentContext.Name;

         OptionGrid.Children.Clear();

         _AnimateColor(true);

         //
         App.PlayBeep("Resource\\beepG.mp3");
      }
      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);

         RefreshDisplay();
         _AnimateIn();
      }
      IntPtr hKeyboardHook = IntPtr.Zero;
      HookProc KeyboardHook;
      public override void EndInit()
      {
         base.EndInit();
         using (Process curProcess = Process.GetCurrentProcess())
         using (ProcessModule curModule = curProcess.MainModule)
         {
            KeyboardHook = new HookProc(KeyboardHookProcedure);
            hKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHook, GetModuleHandle(curModule.ModuleName), 0);
         }
      }
      protected override void OnClosed(EventArgs e)
      {
         base.OnClosed(e);

         if (hKeyboardHook != IntPtr.Zero)
            UnhookWindowsHookEx(hKeyboardHook);
      }
      protected override void OnLostFocus(RoutedEventArgs e)
      {
         base.OnLostFocus(e);
         Cancel();
      }

      public void Cancel()
      {
         if (_Finished)
            return;
         _AnimateFadeOut();
         if (!_AnyCommand)
            App.PlayBeep("Resource\\beepA.mp3");
         _Finished = true;
         OnFinish?.Invoke(this);
      }

      [MethodImpl(MethodImplOptions.NoInlining)]
      public int KeyboardHookProcedure(int nCode, IntPtr wParam, IntPtr lParam)
      {
         try
         {
            if (!_Finished)
               if (nCode >= 0)
               {
                  if (wParam == (IntPtr)WM_KEYDOWN)
                  {
                     int vkCode = Marshal.ReadInt32(lParam);
                     Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                     bool handled = HandleKeyDown(key);
                     if (handled)
                        return 1;
                  }
                  else if (wParam == (IntPtr)WM_KEYUP)
                  {
                     int vkCode = Marshal.ReadInt32(lParam);
                     Key key = KeyInterop.KeyFromVirtualKey(vkCode);
                     if (key == Key.LWin || key == Key.LWin)
                     {
                        Cancel();
                        return 0;
                     }
                  }
               }
         }
         catch (Exception e)
         {
            App.WriteLog(e.ToString());
         }

         //
         return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
      }

      System.Timers.Timer? _SpawnTimer = null;
      int _SpawnCountLeft = 0;
      Dictionary<CommandSpawn, Process> _SpawnProcess = new Dictionary<CommandSpawn, Process>();
      void WaitForSpawn()
      {
         _SpawnTimer = new System.Timers.Timer();
         _SpawnTimer.Interval = 50;
         _SpawnTimer.Elapsed += (s, e) =>
         {
            _SpawnTimer.Stop();

            //
            Dictionary<CommandSpawn, Process> spawnProcess = new Dictionary<CommandSpawn, Process>();
            foreach (var entry in _SpawnProcess)
            {
               if (entry.Value.MainWindowHandle != IntPtr.Zero)
               {
                  // If we're going to move it, and it's minimized or maximized, go back to normal so movement works.
                  if ((entry.Key.WindowX != null && entry.Key.WindowY != null) || entry.Key.Monitor != null)
                     ShowWindow(entry.Value.MainWindowHandle, SW_SHOWNORMAL);

                  // Switch monitors.
                  int baseX = 0, baseY = 0;
                  if (entry.Key.Monitor != null)
                  {
                     // Translate out of its current screen space.
                     RECT rect;
                     GetWindowRect(entry.Value.MainWindowHandle, out rect);
                     System.Windows.Forms.Screen curScreen = null;
                     try
                     {
                        curScreen = System.Windows.Forms.Screen.AllScreens.Where(s => s.Bounds.Contains(rect.left, rect.top)).First();
                     }
                     catch (Exception ex) { }
                     if (curScreen != null)
                     {
                        baseX = rect.left - curScreen.WorkingArea.X;
                        baseY = rect.top - curScreen.WorkingArea.Y;
                     }

                     // Translate into the new screen space.
                     System.Windows.Forms.Screen targetScreen = System.Windows.Forms.Screen.AllScreens[entry.Key.Monitor.Value];
                     baseX += targetScreen.WorkingArea.X;
                     baseY += targetScreen.WorkingArea.Y;
                  }

                  // Move and resize.
                  int moveBit = ((entry.Key.WindowX != null && entry.Key.WindowY != null) || entry.Key.Monitor != null) ? 0 : SWP_NOMOVE;
                  int sizeBit = (entry.Key.WindowW != null && entry.Key.WindowH != null) ? 0 : SWP_NOSIZE;
                  if (moveBit == 0 || sizeBit == 0)
                  {
                     int wx = ((entry.Key.WindowX != null) ? entry.Key.WindowX.Value : 0) + baseX;
                     int wy = ((entry.Key.WindowY != null) ? entry.Key.WindowY.Value : 0) + baseY;
                     int ww = (entry.Key.WindowW != null) ? entry.Key.WindowW.Value : 0;
                     int wh = (entry.Key.WindowH != null) ? entry.Key.WindowH.Value : 0;
                     SetWindowPos(entry.Value.MainWindowHandle, IntPtr.Zero, wx, wy, ww, wh, SWP_NOZORDER | moveBit | sizeBit | SWP_SHOWWINDOW);
                  }

                  // Maximize and minimize.
                  if (entry.Key.Maximize)
                     ShowWindow(entry.Value.MainWindowHandle, SW_SHOWMAXIMIZED);
                  else if (entry.Key.Minimize)
                     ShowWindow(entry.Value.MainWindowHandle, SW_SHOWMINIMIZED);

                  _SpawnCountLeft--;
                  entry.Value.Dispose();
               }
               else if (entry.Value.HasExited)
               {
                  App.WriteLog("App exited before move: " + entry.Key.FileName);

                  _SpawnCountLeft--;
                  entry.Value.Dispose();
               }
               else
                  spawnProcess[entry.Key] = entry.Value;
            }
            _SpawnProcess = spawnProcess;

            //
            if (_SpawnCountLeft == 0)
            {
               _SpawnTimer.Stop();
               _SpawnTimer.Dispose();
               _SpawnTimer = null;
            }
            else
               _SpawnTimer.Start();
         };
         _SpawnTimer.Start();
      }

      string _Text = "";
      bool _AnyCommand = false;
      bool _PendFinished = false;
      bool _Finished = false;
      bool _FadedIn = false;

      Tildetool.Hotcommand.HmContext? _SuggestedContext = null;
      Command? _Suggested = null;
      string _LastSuggested = "";

      public struct AltCommand
      {
         public Command? Command;
         public HmContext? Context;
         public string Tag;
         public string FullText;
         public bool IsQuickTag;
         public bool IsContextual;
      }
      List<AltCommand> _AltCmds = new List<AltCommand>();

      const string _Number = "0123456789";
      private void RefreshDisplay()
      {
         HmContext? defaultContext;
         if (HotcommandManager.Instance.ContextByTag.TryGetValue("DEFAULT", out defaultContext))
            if (defaultContext == HotcommandManager.Instance.CurrentContext)
               defaultContext = null;

         //
         _Suggested = null;
         _SuggestedContext = null;
         _AltCmds.Clear();
         bool suggestedFull = false;
         {
            HashSet<Tildetool.Hotcommand.HmContext> usedC = new HashSet<Tildetool.Hotcommand.HmContext>();
            usedC.Add(HotcommandManager.Instance.CurrentContext);

            HashSet<Command> used = new HashSet<Command>();

            void _addTag(string tag, string full, Command? command, HmContext? context, bool quicktag, bool inContext, bool allowSuggest)
            {
               bool already = false;
               if (command != null)
                  already = !used.Add(command);
               if (context != null)
                  already = !usedC.Add(context);

               quicktag = quicktag && tag.CompareTo(full) != 0;
               if (_Suggested == null && _SuggestedContext == null && allowSuggest)
               {
                  int index = tag.IndexOf(_Text);
                  CommandPreviewPre.Text = tag.Substring(0, index);
                  CommandPreviewPost.Text = tag.Substring(index + _Text.Length);
                  if (quicktag)
                  {
                     CommandExpand.Visibility = Visibility.Visible;
                     CommandExpand.Text = "\u2192 " + full;
                     suggestedFull = true;
                  }
                  _LastSuggested = tag;
                  _Suggested = command;
                  _SuggestedContext = context;
               }
               else if (!already && _AltCmds.Count < 10)
                  _AltCmds.Add(new AltCommand { Tag = tag, FullText = full, Command = command, Context = context, IsQuickTag = quicktag, IsContextual = inContext });
            }

            //
            List<HmUsage>? usages;
            if (HotcommandManager.Instance.CurrentContext.UsageByText.TryGetValue(_Text, out usages))
            {
               foreach (HmUsage usage in usages)
                  if (!used.Contains(usage.Command))
                     _addTag(usage.Command.Tag, usage.Command.Tag, usage.Command, null, false, true, _Text.Length > 0);
            }

            //
            if (_Text.Length > 0)
            {
               foreach (var c in HotcommandManager.Instance.ContextTag)
                  if (c.Key.StartsWith(_Text))
                     _addTag(c.Key, c.Value.Name, null, c.Value, true, false, true);

               foreach (var c in HotcommandManager.Instance.CurrentContext.QuickTags)
                  if (c.Key.StartsWith(_Text))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, true, defaultContext != null, true);

               if (defaultContext != null)
                  foreach (var c in defaultContext.QuickTags)
                     if (c.Key.StartsWith(_Text))
                        _addTag(c.Key, c.Value.Tag, c.Value, null, true, false, true);

               foreach (var c in HotcommandManager.Instance.ContextByTag)
                  if (c.Key.StartsWith(_Text))
                     _addTag(c.Key, c.Value.Name, null, c.Value, false, false, true);

               foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
                  if (c.Key.StartsWith(_Text))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, false, defaultContext != null, true);

               if (defaultContext != null)
                  foreach (var c in defaultContext.Commands)
                     if (c.Key.StartsWith(_Text))
                        _addTag(c.Key, c.Value.Tag, c.Value, null, false, false, true);


               foreach (var c in HotcommandManager.Instance.CurrentContext.QuickTags)
                  if (c.Key.Contains(_Text))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, true, defaultContext != null, true);

               if (defaultContext != null)
                  foreach (var c in defaultContext.QuickTags)
                     if (c.Key.Contains(_Text))
                        _addTag(c.Key, c.Value.Tag, c.Value, null, true, false, true);

               foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
                  if (c.Key.Contains(_Text))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, false, defaultContext != null, true);

               if (defaultContext != null)
                  foreach (var c in defaultContext.Commands)
                     if (c.Key.Contains(_Text))
                        _addTag(c.Key, c.Value.Tag, c.Value, null, false, false, true);
            }

            //
            if (_AltCmds.Count < 10)
               foreach (var c in HotcommandManager.Instance.CurrentContext.QuickTags)
               {
                  _addTag(c.Key, c.Value.Tag, c.Value, null, true, defaultContext != null, false);
                  if (_AltCmds.Count >= 10)
                     break;
               }

            if (_AltCmds.Count < 10)
               if (defaultContext != null)
                  foreach (var c in defaultContext.QuickTags)
                  {
                     _addTag(c.Key, c.Value.Tag, c.Value, null, true, false, false);
                     if (_AltCmds.Count >= 10)
                        break;
                  }

            if (_AltCmds.Count < 10)
               foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
               {
                  _addTag(c.Key, c.Value.Tag, c.Value, null, false, defaultContext != null, false);
                  if (_AltCmds.Count >= 10)
                     break;
               }

            if (_AltCmds.Count < 10)
               if (defaultContext != null)
                  foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
                  {
                     _addTag(c.Key, c.Value.Tag, c.Value, null, false, false, false);
                     if (_AltCmds.Count >= 10)
                        break;
                  }
         }

         //
         if (_Suggested == null && _SuggestedContext == null)
         {
            CommandPreviewPre.Text = _Text.Length == 0 ? _LastSuggested : "";
            CommandPreviewPost.Text = "";
         }
         if (!suggestedFull && (_Suggested != null || _SuggestedContext != null || _Text.Length != 0))
            CommandExpand.Visibility = Visibility.Collapsed;

         //
         {
            DataTemplate? template = Resources["CommandOption"] as DataTemplate;
            while (OptionGrid.Children.Count > _AltCmds.Count)
               OptionGrid.Children.RemoveAt(OptionGrid.Children.Count - 1);
            while (OptionGrid.Children.Count < _AltCmds.Count)
            {
               ContentControl content = new ContentControl { ContentTemplate = template };
               OptionGrid.Children.Add(content);
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
            }
            for (int i = 0; i < _AltCmds.Count; i++)
            {
               ContentControl content = OptionGrid.Children[i] as ContentControl;
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
               Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;
               TextBlock number = grid.FindElementByName<TextBlock>("Number");
               TextBlock text = grid.FindElementByName<TextBlock>("Text");
               TextBlock expand = grid.FindElementByName<TextBlock>("Expand");
               Grid area = grid.FindElementByName<Grid>("Area");

               number.Text = ((i + 1) % 10).ToString();
               text.Text = (_AltCmds[i].IsContextual ? "\u2192 " : "") + _AltCmds[i].Tag;
               if (_AltCmds[i].IsQuickTag)
                  expand.Text = _AltCmds[i].FullText;
               else
                  expand.Text = "";

               grid.Background = new SolidColorBrush { Color = Extension.FromArgb(_AltCmds[i].IsContextual ? 0xFF001310 : 0xFF021204) };
               area.Background = _AltCmds[i].IsContextual ? (Resources["ColorBackground"] as SolidColorBrush) : new SolidColorBrush { Color = Extension.FromArgb(0xFF042508) };
               number.Foreground = _AltCmds[i].IsContextual ? (Resources["ColorTextBack"] as SolidColorBrush) : new SolidColorBrush { Color = Extension.FromArgb(0xFF449637) };
               text.Foreground = _AltCmds[i].IsContextual ? (Resources["ColorTextBack"] as SolidColorBrush) : new SolidColorBrush { Color = Extension.FromArgb(0xFF449637) };
               expand.Foreground = _AltCmds[i].IsContextual ? (Resources["ColorTextBack"] as SolidColorBrush) : new SolidColorBrush { Color = Extension.FromArgb(0xFF449637) };
            }
         }
      }

      private bool HandleKeyDown(Key key)
      {
         bool handled = false;
         if (_Finished)
            return handled;

         // Handle escape
         switch (key)
         {
            case Key.Escape:
               Cancel();
               return true;
         }

         // Handle shift-commands
         if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
         {
            switch (key)
            {
               case Key.Q:
                  Cancel();
                  App.Current.Shutdown();
                  return true;
            }

            int number = -1;
            if (key >= Key.D0 && key <= Key.D9)
               number = key - Key.D0;
            else if (key >= Key.NumPad0 && key <= Key.NumPad9)
               number = key - Key.NumPad0;
            if (number != -1)
            {
               number = (number + 9) % 10;
               if (number < _AltCmds.Count)
               {
                  if (_AltCmds[number].Context != null)
                     SetContext(_AltCmds[number].Context);
                  else if (_AltCmds[number].Command != null)
                  {
                     RefreshDisplay();
                     Execute(_AltCmds[number].Command, number);
                  }
               }
            }

            return true;
         }

         Command? command;

         void _handleText(char text)
         {
            if (_PendFinished)
            {
               _PendFinished = false;
               _Text = text.ToString();
            }
            else
               _Text += text;
            handled = true;
         }

         // Handle key entry.
         if (key >= Key.A && key <= Key.Z)
            _handleText(key.ToString()[0]);
         else if (key >= Key.D0 && key <= Key.D9)
            _handleText(_Number[key - Key.D0]);
         else if (key >= Key.NumPad0 && key <= Key.NumPad9)
            _handleText(_Number[key - Key.NumPad0]);
         else if (key == Key.Space)
            _handleText(' ');
         else if (key == Key.Back && _Text.Length > 0)
         {
            if (_PendFinished)
               _Text = "";
            else
               _Text = _Text.Substring(0, _Text.Length - 1);
            _PendFinished = false;
            handled = true;
         }
         else if (key == Key.Return)
         {
            if (_Text.Length == 0)
            {
               Process process = new Process();
               ProcessStartInfo startInfo = new ProcessStartInfo();
               startInfo.FileName = System.IO.Directory.GetCurrentDirectory() + "\\Hotcommand.json";
               startInfo.UseShellExecute = true;
               process.StartInfo = startInfo;
               process.Start();
               Cancel();
            }
            else if (HotcommandManager.Instance.CurrentContext.Commands.TryGetValue(_Text, out command))
               Execute(command);
            else if (_SuggestedContext != null)
               SetContext(_SuggestedContext);
            else if (_Suggested != null)
               Execute(_Suggested);
            else
               Cancel();

            return true;
         }

         //
         CommandEntry.Text = _Text;

         if (!_FadedIn && _Text.Length == 1)
            _AnimateTextIn();
         if (_FadedIn && _Text.Length == 0)
            _AnimateTextOut();

         //
         HmContext context;
         if (HotcommandManager.Instance.ContextTag.TryGetValue(_Text, out context))
            SetContext(context);
         else if (HotcommandManager.Instance.CurrentContext.QuickTags.TryGetValue(_Text, out command))
         {
            RefreshDisplay();
            Execute(command);
         }
         else if (HotcommandManager.Instance.ContextByTag["DEFAULT"].QuickTags.TryGetValue(_Text, out command))
         {
            RefreshDisplay();
            Execute(command);
         }
         else
            RefreshDisplay();

         return handled;
      }

      public void SetContext(HmContext context)
      {
         HotcommandManager.Instance.CurrentContext = context;
         CommandContext.Visibility = (HotcommandManager.Instance.CurrentContext.Name == "DEFAULT") ? Visibility.Collapsed : Visibility.Visible;
         CommandContext.Text = HotcommandManager.Instance.CurrentContext.Name;

         _AnyCommand = true;
         _PendFinished = true;
         _Suggested = null;

         _AnimateColor(false);
         RefreshDisplay();
         _AnimateCommand(-1);

         App.PlayBeep("Resource\\beepC.mp3");
      }

      public void Execute(Command command, int index = -1)
      {
         bool waitSpawn = false;
         try
         {
            foreach (CommandSpawn spawn in command.Spawns)
            {
               bool wantSpawn = (spawn.WindowX != null && spawn.WindowY != null) || (spawn.WindowW != null && spawn.WindowH != null) || spawn.Monitor != null || spawn.Maximize || spawn.Minimize;
               Thread trd = new Thread(new ThreadStart(() =>
               {
                  try
                  {
                     Process process = new Process();
                     ProcessStartInfo startInfo = new ProcessStartInfo();
                     if (!string.IsNullOrEmpty(spawn.ShellOpen))
                     {
                        startInfo.UseShellExecute = true;
                        startInfo.FileName = spawn.ShellOpen;
                     }
                     else
                        startInfo.FileName = spawn.FileName;
                     if (spawn.ArgumentList != null && spawn.ArgumentList.Length > 0)
                     {
                        foreach (string argument in spawn.ArgumentList)
                           startInfo.ArgumentList.Add(argument);
                     }
                     else
                        startInfo.Arguments = spawn.Arguments;
                     startInfo.WorkingDirectory = spawn.WorkingDirectory;
                     process.StartInfo = startInfo;
                     process.Start();
                     if (wantSpawn)
                        _SpawnProcess[spawn] = process;
                     else
                        process.Dispose();
                  }
                  catch (Exception ex)
                  {
                     MessageBox.Show(ex.ToString());
                     App.WriteLog(ex.ToString());
                  }
               }));
               trd.IsBackground = true;
               trd.Start();

               if (wantSpawn)
               {
                  waitSpawn = true;
                  _SpawnCountLeft++;
               }
            }
         }
         catch (Exception ex)
         {
            App.WriteLog(ex.ToString());
            Cancel();
            MessageBox.Show(ex.Message);
            return;
         }

         HotcommandManager.Instance.IncFrequency("", command, 0.9f);
         //HotcommandManager.Instance.IncFrequency(_Text, command, 0.66f);
         HotcommandManager.Instance.SaveUsageLater();

         if (waitSpawn)
            WaitForSpawn();

         _AnyCommand = true;

         _Suggested = command;
         _AnimateCommand(index);
         App.PlayBeep("Resource\\beepC.mp3");
      }

      #region Animation

      private Storyboard? _StoryboardColor;
      void _AnimateColor(bool immediate)
      {
         void animateBrush(string resourceName, uint argb)
         {
            if (immediate)
            {
               (Resources[resourceName] as SolidColorBrush).Color = Extension.FromArgb(argb);
               return;
            }

            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.To = Extension.FromArgb(argb);
            _StoryboardColor.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, Resources[resourceName] as SolidColorBrush);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(SolidColorBrush.ColorProperty));
         }
         void animateColor(string resourceName, uint argb)
         {
            //if (immediate)
            //{
            Resources[resourceName] = Extension.FromArgb(argb);
            return;
            //}

            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.To = Extension.FromArgb(argb);
            _StoryboardColor.Children.Add(flashAnimation);
            //Storyboard.SetTargetName(flashAnimation, resourceName);
            //Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(SolidColorBrush.ColorProperty));
         }

         if (_StoryboardColor != null)
            _StoryboardColor.Remove(this);
         _StoryboardColor = new Storyboard();
         if (HotcommandManager.Instance.CurrentContext.Name == "DEFAULT")
         {
            animateBrush("ColorBackfill", 0xFF021204);
            animateBrush("ColorBackground", 0xFF042508);
            animateBrush("ColorTextFore", 0xFFC3F1AF);
            animateBrush("ColorTextBack", 0xFF449637);
            animateColor("ColorGlow1", 0xFFDEEFBA);
            animateColor("ColorGlow2", 0xFFAAF99D);
         }
         else
         {
            animateBrush("ColorBackfill", 0xFF001310);
            animateBrush("ColorBackground", 0xFF002720);
            animateBrush("ColorTextFore", 0xFF8ff8e0);
            animateBrush("ColorTextBack", 0xFF009d7f);
            animateColor("ColorGlow1", 0xFFadf8e5);
            animateColor("ColorGlow2", 0xFF4fffdf);
         }
         _StoryboardColor.Completed += (sender, e) => { if (_StoryboardColor != null) { _StoryboardColor.Stop(); _StoryboardColor.Remove(this); _StoryboardColor = null; } };
         _StoryboardColor.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      private Storyboard? _StoryboardAppear;
      void _AnimateIn()
      {
         _StoryboardAppear = new Storyboard();
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 0.0f;
            animation.To = Width;
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.From = 16.0f;
            animation.To = 2.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.From = 16.0f;
            animation.To = 2.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.From = 6.0f;
            animation.To = Height;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.3f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            Border.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }

         _StoryboardAppear.Completed += (sender, e) => { if (_StoryboardAppear != null) _StoryboardAppear.Remove(this); _StoryboardAppear = null; };
         _StoryboardAppear.Begin(this);
      }

      private Storyboard? _StoryboardTextFade;
      void _AnimateTextIn()
      {
         _FadedIn = true;

         CommandLine.UpdateLayout();

         if (_StoryboardTextFade != null)
            _StoryboardTextFade.Remove(this);
         _StoryboardTextFade = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            _StoryboardTextFade.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandBox);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            _StoryboardTextFade.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandExpand);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimationUsingKeyFrames();
            myDoubleAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame(double.NaN, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            _StoryboardTextFade.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandLine);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(StackPanel.WidthProperty));
         }

         _StoryboardTextFade.Completed += (sender, e) => { if (_StoryboardTextFade == sender) { _StoryboardTextFade.Remove(this); _StoryboardTextFade = null; } };
         _StoryboardTextFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      void _AnimateTextOut()
      {
         _FadedIn = false;

         if (_StoryboardTextFade != null)
            _StoryboardTextFade.Remove(this);
         _StoryboardTextFade = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = 0.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            _StoryboardTextFade.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandBox);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = 0.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            _StoryboardTextFade.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandExpand);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = CommandLine.ActualWidth;
            myDoubleAnimation.To = 0.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.6f));
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 5.0, EasingMode = EasingMode.EaseOut };
            _StoryboardTextFade.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandLine);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(StackPanel.WidthProperty));
         }

         _StoryboardTextFade.Completed += (sender, e) => { if (_StoryboardTextFade == sender) { _StoryboardTextFade.Remove(this); _StoryboardTextFade = null; } };
         _StoryboardTextFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      private Storyboard? _StoryboardCommand;
      void _AnimateCommand(int index)
      {
         DependencyObject background;
         if (index == -1)
            background = CommandBox;
         else
         {
            ContentControl content = OptionGrid.Children[index] as ContentControl;
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
            background = grid.FindElementByName<Grid>("Area");
         }
         SolidColorBrush old = background.GetValue(Panel.BackgroundProperty) as SolidColorBrush;
         background.SetValue(Panel.BackgroundProperty, old.Clone());

         _StoryboardCommand = new Storyboard();
         {
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), KeyTime.FromPercent(0.25), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame((Resources["ColorBackground"] as SolidColorBrush).Color, KeyTime.FromPercent(0.5), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            _StoryboardCommand.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, background);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }

         _PendFinished = true;
         _StoryboardCommand.Completed += (sender, e) =>
         {
            _StoryboardCommand.Remove(this);
         };
         _StoryboardCommand.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      private Storyboard? _StoryboardCancel;
      void _AnimateFadeOut()
      {
         _StoryboardCancel = new Storyboard();
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.05f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.35f));
            animation.To = 0.0f;
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 16.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 16.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 6.0f;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut };
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.25f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }

         _StoryboardCancel.Completed += (sender, e) => { _StoryboardCancel.Remove(this); Dispatcher.Invoke(Close); };
         _StoryboardCancel.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      #endregion
   }
}
