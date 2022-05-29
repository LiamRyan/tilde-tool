using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Tildetool.Status
{
   /// <summary>
   /// Interaction logic for StatusBar.xaml
   /// </summary>
   public partial class StatusBar : Window
   {
      public StatusBar()
      {
         // Initialize
         InitializeComponent();

         // Bind event
         SourceManager.Instance.SourceChanged += Instance_SourceChanged;

         // Spawn controls
         StatusPanel.Children.RemoveRange(0, StatusPanel.Children.Count);
         {
            DataTemplate? template = Resources["StatusBox"] as DataTemplate;
            while (StatusPanel.Children.Count > SourceManager.Instance.Sources.Count)
               StatusPanel.Children.RemoveAt(StatusPanel.Children.Count - 1);
            while (StatusPanel.Children.Count < SourceManager.Instance.Sources.Count)
            {
               ContentControl content = new ContentControl { ContentTemplate = template };
               StatusPanel.Children.Add(content);
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
            }
         }

         // Do an initial refresh of the controls.
         for (int i = 0; i < SourceManager.Instance.Sources.Count; i++)
            UpdateStatusBar(i, false);

         // Fade in.
         RootFrame.Opacity = 0;
         AnimateShow();
      }

      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);
         SourceManager.Instance.SourceChanged -= Instance_SourceChanged;
      }

      private void Instance_SourceChanged(object? sender, int index)
      {
         Dispatcher.Invoke(() => { UpdateStatusBar(index, true); });
      }

      private List<Storyboard> _Storyboards = new List<Storyboard>();
      public void UpdateStatusBar(int index, bool fromUpdate)
      {
         ContentControl content = StatusPanel.Children[index] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
         TextBlock title = grid.FindElementByName<TextBlock>("Title");
         Grid divider = grid.FindElementByName<Grid>("Divider");
         TextBlock subtitle = grid.FindElementByName<TextBlock>("Subtitle");
         TextBlock status = grid.FindElementByName<TextBlock>("Status");

         title.Text = SourceManager.Instance.Sources[index].Title;
         subtitle.Text = SourceManager.Instance.Sources[index].Subtitle;
         status.Text = SourceManager.Instance.Sources[index].Status;
         status.Margin = new Thickness(0, String.IsNullOrEmpty(SourceManager.Instance.Sources[index].Subtitle) ? 22 : 42, 0, 0);
         title.Foreground = new SolidColorBrush(SourceManager.Instance.Sources[index].ColorDim);
         divider.Background = new SolidColorBrush(SourceManager.Instance.Sources[index].ColorDim);
         subtitle.Foreground = new SolidColorBrush(SourceManager.Instance.Sources[index].Color);
         status.Foreground = new SolidColorBrush(SourceManager.Instance.Sources[index].Color);

         if (fromUpdate)
         {
            Storyboard storyboard = new Storyboard();
            {
               var flashAnimation = new ColorAnimationUsingKeyFrames();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0x13, 0x10, 0x12), KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
               storyboard.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, grid);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
            }
            _Storyboards.Add(storyboard);
            storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
            storyboard.Begin(this);
         }
      }

      public bool IsShowing { get; protected set; }
      Storyboard _StoryboardHide;
      public void AnimateShow()
      {
         IsShowing = true;

         if (_StoryboardHide != null)
         {
            _StoryboardHide.Stop();
            _StoryboardHide.Remove(this);
         }

         //
         _StoryboardHide = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardHide.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         _StoryboardHide.Completed += (sender, e) => { if (_StoryboardHide != null) _StoryboardHide.Remove(this); _StoryboardHide = null; };
         _StoryboardHide.Begin(this);

         ResetHideTimer();
      }

      Timer? _HideTimer = null;
      public void ResetHideTimer()
      {
         if (_HideTimer != null)
            _HideTimer.Stop();
         _HideTimer = new Timer();
         _HideTimer.Interval = 2500;
         _HideTimer.Elapsed += (s, e) =>
         {
            _HideTimer.Stop();
            _HideTimer = null;
            Dispatcher.Invoke(() => AnimateClose());
         };
         _HideTimer.Start();
      }

      public void AnimateClose()
      {
         IsShowing = false;

         if (_StoryboardHide != null)
         {
            _StoryboardHide.Stop();
            _StoryboardHide.Remove(this);
         }

         //
         _StoryboardHide = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.To = 0.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardHide.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         _StoryboardHide.Completed += (sender, e) => { if (_StoryboardHide != null) _StoryboardHide.Remove(this); _StoryboardHide = null; Close(); };
         _StoryboardHide.Begin(this);
      }
   }
}
