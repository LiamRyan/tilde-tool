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

      public void _Animate()
      {
         _StoryboardFade = new Storyboard();
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.Duration = new Duration(TimeSpan.FromSeconds(3.0f));
            Opacity = 0.0f;
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.25f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0f, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.75f))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(0.0f, KeyTime.FromPercent(1.0)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         {
            var animation = new ThicknessAnimationUsingKeyFrames();
            animation.Duration = new Duration(TimeSpan.FromSeconds(3.0f));
            Text.Margin = new Thickness(20, 0, 20, 0);
            animation.KeyFrames.Add(new LinearThicknessKeyFrame(new Thickness(20, 0, 20, 0), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            animation.KeyFrames.Add(new EasingThicknessKeyFrame(new Thickness(40, 20, 40, 20), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8f)), new ExponentialEase { Exponent = 5.0f }));
            animation.KeyFrames.Add(new LinearThicknessKeyFrame(new Thickness(40, 20, 40, 20), KeyTime.FromPercent(1.0)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Text);
            Storyboard.SetTargetProperty(animation, new PropertyPath(TextBlock.MarginProperty));
         }
         {
            var animation = new ThicknessAnimationUsingKeyFrames();
            animation.Duration = new Duration(TimeSpan.FromSeconds(3.0f));
            Border.Margin = new Thickness(21, 21, 21, 21);
            animation.KeyFrames.Add(new LinearThicknessKeyFrame(new Thickness(21, 21, 21, 21), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            animation.KeyFrames.Add(new EasingThicknessKeyFrame(new Thickness(1, 1, 1, 1), KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8f)), new ExponentialEase { Exponent = 5.0f }));
            animation.KeyFrames.Add(new LinearThicknessKeyFrame(new Thickness(1, 1, 1, 1), KeyTime.FromPercent(1.0)));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         _StoryboardFade.Completed += (sender, e) => { if (_StoryboardFade != null) _StoryboardFade.Remove(); _StoryboardFade = null; Close(); };
         _StoryboardFade.Begin(this);
      }
   }
}
