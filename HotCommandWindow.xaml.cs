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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Tildetool
{
   /// <summary>
   /// Interaction logic for HotCommandWindow.xaml
   /// </summary>
   public partial class HotCommandWindow : Window
   {
      MediaPlayer _MediaPlayer = new MediaPlayer();

      public HotCommandWindow()
      {
         InitializeComponent();
         _AnimateIn();

         //
         _Commands["MINI"] = _CmdMinigolf;
         _Commands["DOC"] = _CmdDocuments;

         //
         DataTemplate template = Resources["CommandOption"] as DataTemplate;

         _MediaPlayer.Open(new Uri("Resource\\beepG.mp3", UriKind.Relative));
         _MediaPlayer.Play();

         //ContentControl ctrl = new ContentControl();
         //ctrl.ContentTemplate = template;
         //Grid.SetRow(ctrl, 1);
         //OptionGrid.Children.Add(ctrl);

         Thickness t = OptionGrid.Margin;
         t.Bottom = OptionGrid.Children.Count > 0 ? 10 : 0;
         OptionGrid.Margin = t;
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
         _Finished = true;
         _AnimateCancel();
         _MediaPlayer.Open(new Uri("Resource\\beepA.mp3", UriKind.Relative));
         _MediaPlayer.Play();
      }

      string _Text = "";
      bool _Finished = false;
      bool _FadedIn = false;

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
            e.Handled = true;
            Dispatcher.BeginInvoke(Close);
            return;
         }
         CommandEntry.Text = _Text;
         RootFrame.UpdateLayout();

         if (!_FadedIn && _Text.Length == 1)
            _AnimateTextIn();

         double rootWidth = CommandEntry.ActualWidth + CommandEntry.Margin.Left + CommandEntry.Margin.Right + CommandBox.Margin.Left + CommandBox.Margin.Right;
         if (rootWidth > RootFrame.Width)
         {
            if (_StoryboardFit != null)
            {
               _StoryboardFit.Stop();
               _StoryboardFit.Remove();
            }
            _StoryboardFit = new Storyboard();
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = rootWidth;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardFit.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.WidthProperty));
            _StoryboardFit.Begin(this, HandoffBehavior.SnapshotAndReplace);
         }

         //
         System.Action command;
         if (_Commands.TryGetValue(_Text, out command))
         {
            command();
            _AnimateCommand();
            _MediaPlayer.Open(new Uri("Resource\\beepC.mp3", UriKind.Relative));
            _MediaPlayer.Play();
         }
      }

      Dictionary<string, System.Action> _Commands = new Dictionary<string, Action>();
      void _CmdMinigolf()
      {
         System.Diagnostics.Process process = new System.Diagnostics.Process();
         System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
         startInfo.FileName = "C:\\dev\\minigolf\\example\\minigolfBlast_DEBUG.exe";
         startInfo.WorkingDirectory = "C:\\dev\\minigolf\\example\\";
         process.StartInfo = startInfo;
         process.Start();
      }
      void _CmdDocuments()
      {
         System.Diagnostics.Process process = new System.Diagnostics.Process();
         System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
         startInfo.FileName = "explorer.exe";
         startInfo.Arguments = "D:\\Documents\\";
         process.StartInfo = startInfo;
         process.Start();
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
            myDoubleAnimation.To = 300.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardFit.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.WidthProperty));
         }

         _StoryboardAppear.Completed += (sender, e) => { _StoryboardAppear.Remove(); _StoryboardAppear = null; };
         _StoryboardAppear.Begin(this);
         _StoryboardFit.Begin(this);
      }

      private Storyboard? _StoryboardTextIn;
      void _AnimateTextIn()
      {
         _FadedIn = true;

         _StoryboardTextIn = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            _StoryboardTextIn.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, CommandBox);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }

         _StoryboardTextIn.Completed += (sender, e) => { _StoryboardTextIn.Remove(); _StoryboardTextIn = null; };
         _StoryboardTextIn.Begin(this);
      }

      private Storyboard? _StoryboardCommand;
      void _AnimateCommand()
      {
         if (_Finished)
            return;
         _Finished = true;

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
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardCommand.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = CommandEntry.ActualWidth + CommandEntry.Margin.Left + CommandEntry.Margin.Right + CommandBox.Margin.Left + CommandBox.Margin.Right;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4f));
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 5.0, EasingMode = EasingMode.EaseOut };
            _StoryboardCommand.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.WidthProperty));
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
