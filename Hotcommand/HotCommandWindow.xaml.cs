using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
using VirtualDesktopApi;
using static Tildetool.WindowsApi;

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

      public HotCommandWindow()
      {
         Width = System.Windows.SystemParameters.PrimaryScreenWidth;
         InitializeComponent();
         Top = App.GetBarTop(Height);

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
         App.PlayBeep(App.BeepSound.Wake);
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
            App.PlayBeep(App.BeepSound.Cancel);
         _Finished = true;
         OnFinish?.Invoke(this);
      }

      bool _AnyInput = false;

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
                        if (_AnyInput)
                        {
                           Cancel();
                           return 0;
                        }
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

      List<CommandRun> _RunList = new List<CommandRun>();

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

               grid.Background = new SolidColorBrush { Color = _AltCmds[i].Context != null ? _AltCmds[i].Context.Colors[(int)ColorIndex.Background].Lerp(Extension.FromRgb(0x000000), 0.5f) : Extension.FromArgb(0xFF021204) };
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
            _AnyInput = true;
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
         else if (key == Key.Back && (_Text.Length > 0 || _PendFinished))
         {
            if (_PendFinished)
               _Text = "";
            else
               _Text = _Text.Substring(0, _Text.Length - 1);
            _PendFinished = false;
            handled = true;
            _AnyInput = true;
         }
         else if (key == Key.Return)
         {
            if (_PendFinished)
               Cancel();
            else if (_Text.Length == 0)
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

            handled = true;
            _AnyInput = true;
            return handled;
         }
         else if (key == Key.Escape || (key == Key.OemTilde && (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))))
         {
            Cancel();
            handled = true;
            _AnyInput = true;
            return handled;
         }
         else
            return handled;

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

         App.PlayBeep(App.BeepSound.Accept);
      }

      public void Execute(Command command, int index = -1)
      {
         bool anyAdmin = false;
         try
         {
            foreach (CommandSpawn spawn in command.Spawns)
            {
               CommandRun run = new CommandRun(spawn, Dispatcher);
               if (!run.Done)
                  _RunList.Add(run);

               if (spawn.AsAdmin)
                  anyAdmin = true;
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

         _AnyCommand = true;

         _Suggested = command;
         _AnimateCommand(index);
         App.PlayBeep(App.BeepSound.Accept);

         if (anyAdmin)
            Cancel();
      }

      #region Animation

      private Storyboard? _StoryboardColor;
      void _AnimateColor(bool immediate)
      {
         void animateBrush(string resourceName, Color color)
         {
            if (immediate)
            {
               (Resources[resourceName] as SolidColorBrush).Color = color;
               return;
            }

            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.To = color;
            _StoryboardColor.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, Resources[resourceName] as SolidColorBrush);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(SolidColorBrush.ColorProperty));
         }
         void animateColor(string resourceName, Color color)
         {
            //if (immediate)
            //{
            Resources[resourceName] = color;
            return;
            //}

            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.To = color;
            _StoryboardColor.Children.Add(flashAnimation);
            //Storyboard.SetTargetName(flashAnimation, resourceName);
            //Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(SolidColorBrush.ColorProperty));
         }

         if (_StoryboardColor != null)
            _StoryboardColor.Remove(this);
         _StoryboardColor = new Storyboard();
         if (HotcommandManager.Instance.CurrentContext.Name == "DEFAULT")
         {
            animateBrush("ColorBackfill", Extension.FromArgb(0xFF021204));
            animateBrush("ColorBackground", Extension.FromArgb(0xFF042508));
            animateBrush("ColorTextFore", Extension.FromArgb(0xFFC3F1AF));
            animateBrush("ColorTextBack", Extension.FromArgb(0xFF449637));
            animateColor("ColorGlow1", Extension.FromArgb(0xFFDEEFBA));
            animateColor("ColorGlow2", Extension.FromArgb(0xFFAAF99D));
         }
         else
         {
            Color[] colors = HotcommandManager.Instance.CurrentContext.Colors;
            animateBrush("ColorBackfill", colors[(int)ColorIndex.Background].Lerp(Extension.FromRgb(0x000000), 0.5f));
            animateBrush("ColorBackground", colors[(int)ColorIndex.Background]);
            animateBrush("ColorTextFore", colors[(int)ColorIndex.TextFore]);
            animateBrush("ColorTextBack", colors[(int)ColorIndex.TextBack]);
            animateColor("ColorGlow1", colors[(int)ColorIndex.Glow].Lerp(Extension.FromRgb(0xFFFFFF), 0.5f));
            animateColor("ColorGlow2", colors[(int)ColorIndex.Glow]);
         }
         _StoryboardColor.Completed += (sender, e) => { if (_StoryboardColor != null) { _StoryboardColor.Stop(); _StoryboardColor.Remove(this); _StoryboardColor = null; } };
         _StoryboardColor.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      private Storyboard? _StoryboardAppear;
      void _AnimateIn()
      {
         _StoryboardAppear = new Storyboard();
         {
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = new Thickness(0, 10, 0, 10);
            animation.To = new Thickness(0, 0, 0, 0);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn };
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         {
            var animation = new ColorAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.0f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.To = Extension.FromArgb(0xFF021204);
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Fill.Color"));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 16.0f;
            animation.To = 2.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 16.0f;
            animation.To = 2.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            Content.Height = 6.0f;
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(6.0f, TimeSpan.FromSeconds(0)));
            //animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height / 2, TimeSpan.FromSeconds(0.2f), new ExponentialEase { Exponent = 2.0, EasingMode = EasingMode.EaseIn }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height, TimeSpan.FromSeconds(0.33f), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut }));
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
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
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = new Thickness(0, 10, 0, 10);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
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
