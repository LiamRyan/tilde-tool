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

namespace Tildetool
{
   /// <summary>
   /// Interaction logic for StartWindow.xaml
   /// </summary>
   public partial class StartWindow : Window
   {
      private Storyboard? _StoryboardFade;

      public StartWindow()
      {
         InitializeComponent();
         _Animate();
      }
      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
         App.Clickthrough(this);
      }

      public void _Animate()
      {
         _StoryboardFade = new Storyboard();
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Width, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.33f)), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Width, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.67f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0f)), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn }));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.33f)), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.67f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0f)), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn }));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(16.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(2.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(2.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.5f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(16.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0f))));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(16.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(2.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(2.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.5f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(16.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0f))));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, GlowBlur);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(6.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Width, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5f)), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Width, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.5f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(6.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0f)), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut }));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(6.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5f)), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.5f))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(6.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0f)), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut }));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.5f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.7f))));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.0f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.7f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(3.0f))));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardFade.Completed += (sender, e) => { if (_StoryboardFade != null) _StoryboardFade.Remove(); _StoryboardFade = null; Close(); };
         _StoryboardFade.Begin(this);
      }
   }
}
