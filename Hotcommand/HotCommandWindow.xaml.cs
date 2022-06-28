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


      [DllImport("user32.dll", SetLastError = true)]
      static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

      private const int SWP_NOSIZE = 0x0001;
      private const int SWP_NOMOVE = 0x0001;
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

         _AnimateIn();
         RefreshDisplay();

         //
         _MediaPlayer.Open(new Uri("Resource\\beepG.mp3", UriKind.Relative));
         _MediaPlayer.Play();
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
         _MediaPlayer.Open(new Uri("Resource\\beepA.mp3", UriKind.Relative));
         _MediaPlayer.Play();
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
                  }
               }
         }
         catch (Exception e)
         {
            Console.WriteLine(e.ToString());
         }

         //
         return CallNextHookEx(hKeyboardHook, nCode, wParam, lParam);
      }

      System.Timers.Timer? _SpawnTimer = null;
      Dictionary<CommandSpawn, Process> _SpawnProcess = new Dictionary<CommandSpawn, Process>();
      void WaitForSpawn()
      {
         _SpawnTimer = new System.Timers.Timer();
         _SpawnTimer.Interval = 50;
         _SpawnTimer.Elapsed += (s, e) =>
         {
            //
            Dictionary<CommandSpawn, Process> spawnProcess = new Dictionary<CommandSpawn, Process>();
            foreach (var entry in _SpawnProcess)
            {
               if (entry.Value.MainWindowHandle != IntPtr.Zero)
               {
                  int moveBit = (entry.Key.WindowX != null && entry.Key.WindowY != null) ? 0 : SWP_NOMOVE;
                  int sizeBit = (entry.Key.WindowW != null && entry.Key.WindowH != null) ? 0 : SWP_NOSIZE;
                  int wx = (entry.Key.WindowX != null) ? entry.Key.WindowX.Value : 0;
                  int wy = (entry.Key.WindowY != null) ? entry.Key.WindowY.Value : 0;
                  int ww = (entry.Key.WindowW != null) ? entry.Key.WindowW.Value : 0;
                  int wh = (entry.Key.WindowH != null) ? entry.Key.WindowH.Value : 0;
                  SetWindowPos(entry.Value.MainWindowHandle, IntPtr.Zero, wx, wy, ww, wh, SWP_NOZORDER | moveBit | sizeBit | SWP_SHOWWINDOW);
                  entry.Value.Dispose();
               }
               else
                  spawnProcess[entry.Key] = entry.Value;
            }
            _SpawnProcess = spawnProcess;

            //
            if (_SpawnProcess.Count == 0)
            {
               _SpawnTimer.Stop();
               _SpawnTimer.Dispose();
               _SpawnTimer = null;
            }
         };
         _SpawnTimer.Start();
      }

      string _Text = "";
      bool _PendFinished = false;
      bool _Finished = false;
      bool _FadedIn = false;

      Tildetool.Hotcommand.HmContext? _SuggestedContext = null;
      Tildetool.Hotcommand.Command? _Suggested = null;
      string _LastSuggested = "";

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
         bool suggestedFull = false;
         List<string> altCmds = new List<string>();
         List<string> altCmdsFull = new List<string>();
         List<bool> altCmdsContext = new List<bool>();
         {
            HashSet<Tildetool.Hotcommand.HmContext> usedC = new HashSet<Tildetool.Hotcommand.HmContext>();
            usedC.Add(HotcommandManager.Instance.CurrentContext);

            HashSet<Tildetool.Hotcommand.Command> used = new HashSet<Tildetool.Hotcommand.Command>();

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
               else if (!already && altCmds.Count < 10)
               {
                  altCmds.Add(tag);
                  if (quicktag)
                     altCmdsFull.Add(" \u2192 " + full);
                  else
                     altCmdsFull.Add("");
                  altCmdsContext.Add(inContext);
               }
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
            if (altCmds.Count < 10)
               foreach (var c in HotcommandManager.Instance.CurrentContext.QuickTags)
               {
                  _addTag(c.Key, c.Value.Tag, c.Value, null, true, defaultContext != null, false);
                  if (altCmds.Count >= 10)
                     break;
               }

            if (altCmds.Count < 10)
               if (defaultContext != null)
                  foreach (var c in defaultContext.QuickTags)
                  {
                     _addTag(c.Key, c.Value.Tag, c.Value, null, true, false, false);
                     if (altCmds.Count >= 10)
                        break;
                  }

            if (altCmds.Count < 10)
               foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
               {
                  _addTag(c.Key, c.Value.Tag, c.Value, null, false, defaultContext != null, false);
                  if (altCmds.Count >= 10)
                     break;
               }

            if (altCmds.Count < 10)
               if (defaultContext != null)
                  foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
                  {
                     _addTag(c.Key, c.Value.Tag, c.Value, null, false, false, false);
                     if (altCmds.Count >= 10)
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
            while (OptionGrid.Children.Count > altCmds.Count)
               OptionGrid.Children.RemoveAt(OptionGrid.Children.Count - 1);
            while (OptionGrid.Children.Count < altCmds.Count)
            {
               ContentControl content = new ContentControl { ContentTemplate = template };
               OptionGrid.Children.Add(content);
               Grid.SetRow(content, OptionGrid.Children.Count);
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
            }
            if (OptionGrid.RowDefinitions.Count > altCmds.Count)
               OptionGrid.RowDefinitions.RemoveRange(0, OptionGrid.RowDefinitions.Count - altCmds.Count);
            while (OptionGrid.RowDefinitions.Count < altCmds.Count)
               OptionGrid.RowDefinitions.Add(new RowDefinition());
            for (int i = 0; i < altCmds.Count; i++)
            {
               ContentControl content = OptionGrid.Children[i] as ContentControl;
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
               FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
               TextBlock text = grid.FindElementByName<TextBlock>("Text");
               TextBlock expand = grid.FindElementByName<TextBlock>("Expand");
               TextBlock pre = grid.FindElementByName<TextBlock>("Pre");

               text.Text = altCmds[i];
               expand.Text = altCmdsFull[i];
               pre.Visibility = altCmdsContext[i] ? Visibility.Visible : Visibility.Collapsed;
               Thickness t = grid.Margin;
               t.Bottom = (i + 1 >= altCmds.Count) ? 10 : 0;
               grid.Margin = t;
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

         Tildetool.Hotcommand.Command? command;

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
            {
               HotcommandManager.Instance.CurrentContext = _SuggestedContext;
               _AnimateCommand();
               Thread trd = new Thread(new ThreadStart(_PlayBeep));
               trd.IsBackground = true;
               trd.Start();
            }
            else if (_Suggested != null)
               Execute(_Suggested);
            else
               Cancel();

            return true;
         }
         CommandEntry.Text = _Text;

         if (!_FadedIn && _Text.Length == 1)
            _AnimateTextIn();
         if (_FadedIn && _Text.Length == 0)
            _AnimateTextOut();

         //
         HmContext context;
         if (HotcommandManager.Instance.ContextTag.TryGetValue(_Text, out context))
         {
            HotcommandManager.Instance.CurrentContext = context;
            CommandContext.Visibility = (HotcommandManager.Instance.CurrentContext.Name == "DEFAULT") ? Visibility.Collapsed : Visibility.Visible;
            CommandContext.Text = HotcommandManager.Instance.CurrentContext.Name;

            _Text = "";
            _LastSuggested = "";
            _SuggestedContext = null;
            _Suggested = null;
            CommandEntry.Text = "";
            CommandPreviewPre.Text = "";
            CommandPreviewPost.Text = "";
            CommandExpand.Visibility = Visibility.Collapsed;

            _AnimateTextOut();

            Thread trd = new Thread(new ThreadStart(_PlayBeep));
            trd.IsBackground = true;
            trd.Start();
         }
         else if (HotcommandManager.Instance.CurrentContext.QuickTags.TryGetValue(_Text, out command))
            Execute(command);
         else if (HotcommandManager.Instance.ContextByTag["DEFAULT"].QuickTags.TryGetValue(_Text, out command))
            Execute(command);

         RefreshDisplay();
         return handled;
      }

      public void Execute(Tildetool.Hotcommand.Command command)
      {
         bool waitSpawn = false;
         try
         {
            foreach (CommandSpawn spawn in command.Spawns)
            {
               Process process = new Process();
               ProcessStartInfo startInfo = new ProcessStartInfo();
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
               if ((spawn.WindowX != null && spawn.WindowY != null) || (spawn.WindowW != null && spawn.WindowH != null))
               {
                  _SpawnProcess[spawn] = process;
                  waitSpawn = true;
               }
               else
                  process.Dispose();
            }
         }
         catch (Exception ex)
         {
            Console.WriteLine(ex.ToString());
            Cancel();
            MessageBox.Show(ex.Message);
            return;
         }

         HotcommandManager.Instance.IncFrequency("", command, 0.9f);
         //HotcommandManager.Instance.IncFrequency(_Text, command, 0.66f);
         HotcommandManager.Instance.SaveUsageLater();

         if (waitSpawn)
            WaitForSpawn();

         _Suggested = command;
         _AnimateCommand();
         Thread trd = new Thread(new ThreadStart(_PlayBeep));
         trd.IsBackground = true;
         trd.Start();
      }
      void _PlayBeep()
      {
         MediaPlayer mediaPlayer = new MediaPlayer();
         mediaPlayer.Open(new Uri("Resource\\beepC.mp3", UriKind.Relative));
         mediaPlayer.Play();
      }

      #region Animation

      private Storyboard? _StoryboardAppear;
      private Storyboard? _StoryboardFit;
      void _AnimateIn()
      {
         _StoryboardAppear = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardAppear.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            SubcommandArea.Opacity = 0.0;
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.BeginTime = TimeSpan.FromSeconds(0.2f);
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.3f));
            _StoryboardAppear.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, SubcommandArea);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         _StoryboardFit = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 5.0;
            myDoubleAnimation.To = CommandArea.Height;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4f));
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut };
            _StoryboardFit.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandArea);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.HeightProperty));
         }

         _StoryboardAppear.Completed += (sender, e) => { if (_StoryboardAppear != null) _StoryboardAppear.Remove(); _StoryboardAppear = null; };
         _StoryboardAppear.Begin(this);
         _StoryboardFit.Begin(this);
      }

      private Storyboard? _StoryboardTextFade;
      void _AnimateTextIn()
      {
         _FadedIn = true;

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

         _StoryboardTextFade.Completed += (sender, e) => { if (_StoryboardTextFade != null) _StoryboardTextFade.Remove(); _StoryboardTextFade = null; };
         _StoryboardTextFade.Begin(this);
      }
      void _AnimateTextOut()
      {
         _FadedIn = false;

         if (_StoryboardTextFade != null)
         {
            _StoryboardTextFade.Stop();
            _StoryboardTextFade.Remove();
         }
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

         _StoryboardTextFade.Completed += (sender, e) => { if (_StoryboardTextFade != null) _StoryboardTextFade.Remove(); _StoryboardTextFade = null; };
         _StoryboardTextFade.Begin(this);
      }

      private Storyboard? _StoryboardCommand;
      void _AnimateCommand()
      {
         _StoryboardCommand = new Storyboard();
         {
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.25), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0x80, 0x00, 0x00, 0x00), KeyTime.FromPercent(0.5), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            _StoryboardCommand.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, CommandBox);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }

         _PendFinished = true;
         _StoryboardCommand.Completed += (sender, e) =>
         {
            _StoryboardCommand.Remove();
            if (_PendFinished && !_Finished)
            {
               _AnimateFadeOut();
               _Finished = true;
               OnFinish?.Invoke(this);
            }
         };
         _StoryboardCommand.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      private Storyboard? _StoryboardCancel;
      void _AnimateFadeOut()
      {
         _StoryboardCancel = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = 0.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardCancel.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            myDoubleAnimation.To = 5.0f;
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut };
            _StoryboardCancel.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandArea);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.HeightProperty));
         }

         _StoryboardCancel.Completed += (sender, e) => { _StoryboardCancel.Remove(); Dispatcher.Invoke(Close); };
         _StoryboardCancel.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      #endregion
   }
}
