using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Tildetool.Hotcommand;

namespace Tildetool
{
   /// <summary>
   /// Interaction logic for WordLookup.xaml
   /// </summary>
   public partial class WordLookup : Window
   {
      #region Events

      public delegate void PopupEvent(object sender);
      public event PopupEvent? OnFinish;

      #endregion

      public WordLookup()
      {
         Width = System.Windows.SystemParameters.PrimaryScreenWidth;

         InitializeComponent();
         WordBox.Opacity = 0;
         WordEntry.Text = "";

         _AnimateIn();
      }
      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
         App.Clickthrough(this);
      }

      public void Cancel()
      {
         if (_Finished)
            return;
         _AnimateFadeOut();
         _Finished = true;
         OnFinish?.Invoke(this);
      }

      string _Text = "";
      const string _Number = "0123456789";
      bool _Finished = false;
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

         void _handleText(char text)
         {
            _Text += text;
            e.Handled = true;
         }

         // Handle key entry.
         if (e.Key >= Key.A && e.Key <= Key.Z)
            _handleText(e.Key.ToString()[0]);
         else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            _handleText(_Number[e.Key - Key.D0]);
         else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            _handleText(_Number[e.Key - Key.NumPad0]);
         else if (e.Key == Key.Space)
            _handleText(' ');
         else if (e.Key == Key.Back && _Text.Length > 0)
         {
            _Text = _Text.Substring(0, _Text.Length - 1);
            e.Handled = true;
         }
         else if (e.Key == Key.Return)
         {
            if (!string.IsNullOrEmpty(_Text))
            {
               Process.Start(new ProcessStartInfo(HotcommandManager.Instance.DictionaryURL.Replace("@URL@", _Text.ToLower())) { UseShellExecute = true });
               _AnimateCommand();
            }
            else
               Cancel();

            e.Handled = true;
            return;
         }

         if (string.IsNullOrEmpty(_Text))
            _AnimateTextOut();
         else
            _AnimateTextIn();

         WordEntry.Text = _Text;
      }


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
            myDoubleAnimation.From = 5.0;
            myDoubleAnimation.To = WordArea.Height;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4f));
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut };
            _StoryboardFit.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, WordArea);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.HeightProperty));
         }

         _StoryboardAppear.Completed += (sender, e) => { if (_StoryboardAppear != null) _StoryboardAppear.Remove(); _StoryboardAppear = null; };
         _StoryboardAppear.Begin(this);
         _StoryboardFit.Begin(this);
      }

      private Storyboard? _StoryboardCommand;
      void _AnimateCommand()
      {
         if (_Finished)
            return;
         _Finished = true;

         _StoryboardCommand = new Storyboard();
         {
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.25), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0x80, 0x00, 0x00, 0x00), KeyTime.FromPercent(0.5), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            _StoryboardCommand.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, WordBox);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.BeginTime = TimeSpan.FromSeconds(0.5f);
            myDoubleAnimation.To = 0.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardCommand.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.BeginTime = TimeSpan.FromSeconds(0.5f);
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            myDoubleAnimation.To = 5.0f;
            myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut };
            _StoryboardCommand.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, WordArea);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.HeightProperty));
         }

         _StoryboardCommand.Completed += (sender, e) => { _StoryboardCommand.Remove(); Close(); };
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
            Storyboard.SetTarget(myDoubleAnimation, WordArea);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.HeightProperty));
         }

         _StoryboardCancel.Completed += (sender, e) => { _StoryboardCancel.Remove(); Dispatcher.Invoke(Close); };
         _StoryboardCancel.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }


      bool _FadedIn = false;
      private Storyboard? _StoryboardTextFade;
      void _AnimateTextIn()
      {
         if (_FadedIn)
            return;
         _FadedIn = true;

         _StoryboardTextFade = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            _StoryboardTextFade.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, WordBox);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }

         _StoryboardTextFade.Completed += (sender, e) => { if (_StoryboardTextFade != null) _StoryboardTextFade.Remove(); _StoryboardTextFade = null; };
         _StoryboardTextFade.Begin(this);
      }
      void _AnimateTextOut()
      {
         if (!_FadedIn)
            return;
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
            Storyboard.SetTarget(myDoubleAnimation, WordBox);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }

         _StoryboardTextFade.Completed += (sender, e) => { if (_StoryboardTextFade != null) _StoryboardTextFade.Remove(); _StoryboardTextFade = null; };
         _StoryboardTextFade.Begin(this);
      }
   }
}
