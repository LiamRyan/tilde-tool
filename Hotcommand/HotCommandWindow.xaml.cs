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

      [DllImport("user32.dll", SetLastError = true)]
      static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

      private const int SWP_NOSIZE = 0x0001;
      private const int SWP_NOMOVE = 0x0001;
      private const int SWP_NOZORDER = 0x0004;
      private const int SWP_SHOWWINDOW = 0x0040;

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

         //
         _MediaPlayer.Open(new Uri("Resource\\beepG.mp3", UriKind.Relative));
         _MediaPlayer.Play();
      }
      protected override void OnClosed(EventArgs e)
      {
         base.OnClosed(e);
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
         _AnimateCancel();
         _MediaPlayer.Open(new Uri("Resource\\beepA.mp3", UriKind.Relative));
         _MediaPlayer.Play();
         _Finished = true;
         OnFinish?.Invoke(this);
      }

      Timer? _SpawnTimer = null;
      Dictionary<CommandSpawn, Process> _SpawnProcess = new Dictionary<CommandSpawn, Process>();
      void WaitForSpawn()
      {
         _SpawnTimer = new Timer();
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
      bool _Finished = false;
      bool _FadedIn = false;

      Tildetool.Hotcommand.HmContext? _SuggestedContext = null;
      Tildetool.Hotcommand.Command? _Suggested = null;
      string _LastSuggested = "";

      const string _Number = "0123456789";
      private void Window_KeyDown(object sender, KeyEventArgs e)
      {
         if (_Finished)
            return;

         // Handle escape
         switch (e.Key)
         {
            case Key.Escape:
               e.Handled = true;
               Cancel();
               return;
         }

         Tildetool.Hotcommand.Command? command;

         // Handle key entry.
         if (e.Key >= Key.A && e.Key <= Key.Z)
         {
            _Text += e.Key.ToString();
            e.Handled = true;
         }
         else if (e.Key >= Key.D0 && e.Key <= Key.D9)
         {
            _Text += _Number[e.Key - Key.D0];
            e.Handled = true;
         }
         else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
         {
            _Text += _Number[e.Key - Key.NumPad0];
            e.Handled = true;
         }
         else if (e.Key == Key.Space)
         {
            _Text += " ";
            e.Handled = true;
         }
         else if (e.Key == Key.Back && _Text.Length > 0)
         {
            _Text = _Text.Substring(0, _Text.Length - 1);
            e.Handled = true;
         }
         else if (e.Key == Key.Return)
         {
            if (_Text.Length == 0)
            {
               Process process = new Process();
               ProcessStartInfo startInfo = new ProcessStartInfo();
               startInfo.FileName = System.AppDomain.CurrentDomain.BaseDirectory + "\\Hotcommand.json";
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
               _MediaPlayer.Open(new Uri("Resource\\beepC.mp3", UriKind.Relative));
               _MediaPlayer.Play();
            }
            else if (_Suggested != null)
               Execute(_Suggested);
            else
               Cancel();

            e.Handled = true;
            return;
         }
         CommandEntry.Text = _Text;
         RootFrame.UpdateLayout();

         if (!_FadedIn && _Text.Length == 1)
            _AnimateTextIn();
         if (_FadedIn && _Text.Length == 0)
            _AnimateTextOut();

         HmContext? defaultContext;
         if (HotcommandManager.Instance.Context.TryGetValue("DEFAULT", out defaultContext))
            if (defaultContext == HotcommandManager.Instance.CurrentContext)
               defaultContext = null;

         //
         _Suggested = null;
         _SuggestedContext = null;
         bool suggestedFull = false;
         List<string> altCmds = new List<string>();
         List<string> altCmdsFull = new List<string>();
         if (_Text.Length > 0)
         {
            HashSet<Tildetool.Hotcommand.HmContext> usedC = new HashSet<Tildetool.Hotcommand.HmContext>();
            usedC.Add(HotcommandManager.Instance.CurrentContext);

            HashSet<Tildetool.Hotcommand.Command> used = new HashSet<Tildetool.Hotcommand.Command>();

            void _addTag(string tag, string full, Command? command, HmContext? context, bool quicktag)
            {
               if (command != null)
                  used.Add(command);
               if (context != null)
                  usedC.Add(context);

               if (_Suggested == null && _SuggestedContext == null)
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
               else
               {
                  altCmds.Add(tag);
                  if (quicktag)
                     altCmdsFull.Add(" \u2192 " + full);
                  else
                     altCmdsFull.Add("");
               }
            }

            foreach (var c in HotcommandManager.Instance.ContextTag)
               if (c.Key.StartsWith(_Text) && !usedC.Contains(c.Value))
                  _addTag(c.Key, c.Value.Name, null, c.Value, true);

            foreach (var c in HotcommandManager.Instance.CurrentContext.QuickTags)
               if (c.Key.StartsWith(_Text) && !used.Contains(c.Value))
                  _addTag(c.Key, c.Value.Tag, c.Value, null, true);

            if (defaultContext != null)
               foreach (var c in defaultContext.QuickTags)
                  if (c.Key.StartsWith(_Text) && !used.Contains(c.Value))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, true);

            foreach (var c in HotcommandManager.Instance.Context)
               if (c.Key.StartsWith(_Text) && !usedC.Contains(c.Value))
                  _addTag(c.Key, c.Value.Name, null, c.Value, false);

            foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
               if (c.Key.StartsWith(_Text) && !used.Contains(c.Value))
                  _addTag(c.Key, c.Value.Tag, c.Value, null, false);

            if (defaultContext != null)
               foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
                  if (c.Key.StartsWith(_Text) && !used.Contains(c.Value))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, false);


            foreach (var c in HotcommandManager.Instance.CurrentContext.QuickTags)
               if (c.Key.Contains(_Text) && !used.Contains(c.Value))
                  _addTag(c.Key, c.Value.Tag, c.Value, null, true);

            if (defaultContext != null)
               foreach (var c in defaultContext.QuickTags)
                  if (c.Key.Contains(_Text) && !used.Contains(c.Value))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, true);

            foreach (var c in HotcommandManager.Instance.CurrentContext.Commands)
               if (c.Key.Contains(_Text) && !used.Contains(c.Value))
                  _addTag(c.Key, c.Value.Tag, c.Value, null, false);

            if (defaultContext != null)
               foreach (var c in defaultContext.Commands)
                  if (c.Key.Contains(_Text) && !used.Contains(c.Value))
                     _addTag(c.Key, c.Value.Tag, c.Value, null, false);
         }
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
            for (int i = 0; i < altCmds.Count; i++)
            {
               ContentControl content = OptionGrid.Children[i] as ContentControl;
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
               FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
               TextBlock text = grid.FindElementByName<TextBlock>("Text");
               TextBlock expand = grid.FindElementByName<TextBlock>("Expand");

               text.Text = altCmds[i];
               expand.Text = altCmdsFull[i];
               Thickness t = grid.Margin;
               t.Bottom = (i + 1 >= altCmds.Count) ? 10 : 0;
               grid.Margin = t;
            }
         }

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

            _MediaPlayer.Open(new Uri("Resource\\beepC.mp3", UriKind.Relative));
            _MediaPlayer.Play();
         }
         else if (HotcommandManager.Instance.CurrentContext.QuickTags.TryGetValue(_Text, out command))
            Execute(command);
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

         if (waitSpawn)
            WaitForSpawn();

         _AnimateCommand();
         _MediaPlayer.Open(new Uri("Resource\\beepC.mp3", UriKind.Relative));
         _MediaPlayer.Play();
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
         _StoryboardFit = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 40.0;
            myDoubleAnimation.To = Width;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 2.5, EasingMode = EasingMode.EaseOut };
            _StoryboardFit.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.WidthProperty));
         }

         _StoryboardAppear.Completed += (sender, e) => { _StoryboardAppear.Remove(); _StoryboardAppear = null; };
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

         _StoryboardTextFade.Completed += (sender, e) => { _StoryboardTextFade.Remove(); _StoryboardTextFade = null; };
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

         _StoryboardTextFade.Completed += (sender, e) => { _StoryboardTextFade.Remove(); _StoryboardTextFade = null; };
         _StoryboardTextFade.Begin(this);
      }

      private Storyboard? _StoryboardCommand;
      void _AnimateCommand()
      {
         if (_Finished)
            return;
         _Finished = true;
         OnFinish?.Invoke(this);

         _StoryboardCommand = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimationUsingKeyFrames();
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.7f));
            myDoubleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1f))));
            myDoubleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5f))));
            myDoubleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromPercent(1.0)));
            _StoryboardCommand.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandBox);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimationUsingKeyFrames();
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.7f));
            myDoubleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2f))));
            myDoubleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5f))));
            myDoubleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromPercent(1.0)));
            _StoryboardCommand.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0x80, 0x00, 0x00, 0x00), KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            _StoryboardCommand.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, CommandBox);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }

         _StoryboardCommand.Completed += (sender, e) => { _StoryboardCommand.Remove(); Dispatcher.Invoke(Close); };
         _StoryboardCommand.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      private Storyboard? _StoryboardCancel;
      void _AnimateCancel()
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

         _StoryboardCancel.Completed += (sender, e) => { _StoryboardCancel.Remove(); Dispatcher.Invoke(Close); };
         _StoryboardCancel.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      #endregion
   }
}
