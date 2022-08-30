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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Timers;
using System.Windows.Media.Animation;
using System.ComponentModel;

namespace Tildetool.Status
{
   /// <summary>
   /// Interaction logic for StatusProgress.xaml
   /// </summary>
   public partial class StatusProgress : Window
   {
      private Storyboard? _StoryboardFade;
      private Storyboard? _StoryboardSpin;

      public StatusProgress()
      {
         InitializeComponent();

         if (App.StatusBar == null)
            _AnimateShow();
         else
            Opacity = 0.0f;

         App.StatusBarAwake += App_StatusBarAwake;
      }
      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);
         App.StatusBarAwake -= App_StatusBarAwake;
      }
      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
         App.Clickthrough(this);
      }

      private void App_StatusBarAwake(Window window, bool awake)
      {
         if (Finished)
            return;

         if (awake)
            _AnimateShow();
         else
            _AnimateHide();
      }

      public void Cancel()
      {
         if (Finished)
            return;

         if (!_Showing)
            Close();
         else
            _AnimateHide();

         Finished = true;
      }

      bool _Showing = false;
      void _AnimateShow()
      {
         if (Finished)
            return;

         if (_Showing)
            return;
         _Showing = true;

         if (_StoryboardFade != null)
         {
            _StoryboardFade.Stop(this);
            _StoryboardFade.Remove(this);
         }

         Opacity = 1.0f;

         _StoryboardFade = new Storyboard();
         {
            var animation = new DoubleAnimation(0.0f, Width, new Duration(TimeSpan.FromSeconds(0.33f)));
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation(0.0f, Height, new Duration(TimeSpan.FromSeconds(0.33f)));
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation(16.0f, 2.0f, new Duration(TimeSpan.FromSeconds(0.0f)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation(16.0f, 2.0f, new Duration(TimeSpan.FromSeconds(0.5f)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, GlowBlur);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation(6.0f, Width, new Duration(TimeSpan.FromSeconds(0.5f)));
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation(6.0f, Height, new Duration(TimeSpan.FromSeconds(0.5f)));
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            Border.Opacity = 0.0f;
            var animation = new DoubleAnimation(0.0f, 1.0f, new Duration(TimeSpan.FromSeconds(0.2f)));
            animation.BeginTime = TimeSpan.FromSeconds(0.3f);
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.OpacityProperty));
         }
         _StoryboardFade.Completed += (sender, e) => { if (_StoryboardFade != null) _StoryboardFade.Remove(this); _StoryboardFade = null; };
         _StoryboardFade.Begin(this);

         if (_StoryboardSpin != null)
         {
            _StoryboardSpin.Stop(this);
            _StoryboardSpin.Remove(this);
            _StoryboardSpin = null;
         }
         _StoryboardSpin = new Storyboard();
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(1.0f));
            flashAnimation.From = 0.0f;
            flashAnimation.To = 360.0f;
            flashAnimation.RepeatBehavior = RepeatBehavior.Forever;
            _StoryboardSpin.Children.Add(flashAnimation);
            ProgressArc.RenderTransform = new RotateTransform(0, 0, 0);
            Storyboard.SetTarget(flashAnimation, ProgressArc);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("RenderTransform.Angle"));
         }
         _StoryboardSpin.Begin(this);
      }

      public bool Finished { get; protected set; }
      void _AnimateHide()
      {
         if (Finished)
            return;

         if (!_Showing)
            return;
         _Showing = false;

         if (_StoryboardFade != null)
         {
            _StoryboardFade.Stop(this);
            _StoryboardFade.Remove(this);
         }

         _StoryboardFade = new Storyboard();
         {
            var animation = new DoubleAnimation(0.0f, new Duration(TimeSpan.FromSeconds(0.33f)));
            animation.BeginTime = TimeSpan.FromSeconds(0.17f);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation(0.0f, new Duration(TimeSpan.FromSeconds(0.33f)));
            animation.BeginTime = TimeSpan.FromSeconds(0.17f);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation(16.0f, new Duration(TimeSpan.FromSeconds(0.5f)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation(16.0f, new Duration(TimeSpan.FromSeconds(0.5f)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, GlowBlur);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation(6.0f, new Duration(TimeSpan.FromSeconds(0.5f)));
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation(6.0f, new Duration(TimeSpan.FromSeconds(0.5f)));
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation(0.0f, new Duration(TimeSpan.FromSeconds(0.2f)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation(0.0f, new Duration(TimeSpan.FromSeconds(0.3f)));
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardFade.Completed += (sender, e) =>
         {
            if (_StoryboardFade != null)
               _StoryboardFade.Remove(this);
            _StoryboardFade = null;

            _StoryboardSpin.Stop(this);
            _StoryboardSpin.Remove(this);
            _StoryboardSpin = null;

            if (Finished)
               Close();
         };
         _StoryboardFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
   }
}
