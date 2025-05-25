using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
      private Storyboard? _StoryboardFill;
      private Storyboard? _StoryboardSpin;

      public StatusProgress()
      {
         InitializeComponent();

         _AnimateShow();
         Update();
      }
      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);
      }
      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
         App.Clickthrough(this);
      }

      public void Cancel()
      {
         _AnimateHide();
      }

      HashSet<Source> SourcesNeed = new();
      public void Update()
      {
         _AnimateFill();
      }

      void _AnimateShow()
      {
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
         _AnimateFill();

         Finished = true;

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

            if (_StoryboardFill != null)
               _StoryboardFill.Remove(this);
            _StoryboardFill = null;

            if (_StoryboardSpin != null)
            {
               _StoryboardSpin.Stop(this);
               _StoryboardSpin.Remove(this);
            }
            _StoryboardSpin = null;

            Close();
         };
         _StoryboardFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      void _AnimateFill()
      {
         HashSet<Source> sourcesLeft = SourceManager.Instance.Sources.Where(src => !src.Ephemeral && (src.IsQuerying || SourceManager.Instance.NeedRefresh(src))).ToHashSet();
         SourcesNeed.UnionWith(sourcesLeft);

         ProgressFill.EndAngle = 90.0f + 360.0f - (360.0f * sourcesLeft.Count / SourcesNeed.Count);

         if (_StoryboardFill != null)
         {
            _StoryboardFill.Stop(this);
            _StoryboardFill.Remove(this);
         }

         _StoryboardFill = new Storyboard();
         {
            float targetAngle = 90.0f + 360.0f - (360.0f * sourcesLeft.Count / SourcesNeed.Count);
            var animation = new DoubleAnimation(targetAngle, new Duration(TimeSpan.FromSeconds(0.5f)));
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardFill.Children.Add(animation);
            Storyboard.SetTarget(animation, ProgressFill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Shape.Arc.EndAngleProperty));
         }

         _StoryboardFill.Begin(this, HandoffBehavior.SnapshotAndReplace);
         _StoryboardFill.Completed += (sender, e) => { if (_StoryboardFill != null) _StoryboardFill.Remove(this); _StoryboardFill = null; };
      }
   }
}
