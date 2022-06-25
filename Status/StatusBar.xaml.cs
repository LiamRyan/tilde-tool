using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Tildetool.Status
{
   /// <summary>
   /// Interaction logic for StatusBar.xaml
   /// </summary>
   public partial class StatusBar : Window
   {
      protected bool _HasTimer;
      public StatusBar(bool hasTimer)
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

               int index = StatusPanel.Children.Count - 1;
               FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
               grid.PreviewMouseDown += (s, e) => Grid_PreviewMouseDown(s, e, index);
               grid.MouseEnter += Grid_MouseEnter;
               grid.MouseLeave += Grid_MouseLeave;
            }
         }

         // Do an initial refresh of the controls.
         for (int i = 0; i < SourceManager.Instance.Sources.Count; i++)
            UpdateStatusBar(i, false);

         // Fade in.
         RootFrame.Opacity = 0;
         _HasTimer = hasTimer;
         AnimateShow();
      }

      private void Grid_MouseEnter(object sender, MouseEventArgs e)
      {
         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            flashAnimation.To = Color.FromArgb(0xFF, 0x2D, 0x26, 0x2A);
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      private void Grid_MouseLeave(object sender, MouseEventArgs e)
      {
         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            flashAnimation.To = Color.FromArgb(0xFF, 0x13, 0x10, 0x12);
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      private void Grid_PreviewMouseDown(object sender, MouseEventArgs e, int index)
      {
         if (e.RightButton == MouseButtonState.Pressed)
         {
            Storyboard storyboard = new Storyboard();
            {
               var flashAnimation = new ColorAnimationUsingKeyFrames();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0x13, 0x10, 0x12), KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
               storyboard.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, sender as Grid);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
            }
            _Storyboards.Add(storyboard);
            storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
            storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);

            Dispatcher.Invoke(() =>
            {
               SourceManager.Instance.Sources[index].Status = "...working...";
               UpdateStatusBar(index, false);
               SourceManager.Instance.Query(index);
            });
         }
         else
         {
            IsShowing = false;

            _StoryboardHide = new Storyboard();
            {
               var myDoubleAnimation = new DoubleAnimation();
               myDoubleAnimation.BeginTime = TimeSpan.FromSeconds(0.2f);
               myDoubleAnimation.To = 0.0;
               myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
               _StoryboardHide.Children.Add(myDoubleAnimation);
               Storyboard.SetTarget(myDoubleAnimation, RootFrame);
               Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
            }
            {
               var flashAnimation = new ColorAnimationUsingKeyFrames();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0x13, 0x10, 0x12), KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
               _StoryboardHide.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, sender as Grid);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
            }
            _StoryboardHide.Completed += (sender, e) => { if (_StoryboardHide != null) _StoryboardHide.Remove(this); _StoryboardHide = null; Close(); };
            _StoryboardHide.Begin(this);

            Dispatcher.Invoke(() => SourceManager.Instance.Sources[index].HandleClick());
         }
      }

      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);
         SourceManager.Instance.SourceChanged -= Instance_SourceChanged;
      }

      private void Window_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.Key == Key.Return)
         {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = System.IO.Directory.GetCurrentDirectory() + "\\Source.json";
            startInfo.UseShellExecute = true;
            process.StartInfo = startInfo;
            process.Start();
            AnimateClose();
         }
      }

      private void Instance_SourceChanged(object? sender, SourceManager.SourceEventArgs args)
      {
         Dispatcher.Invoke(() => { UpdateStatusBar(args.Index, args.CacheChanged); });
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
            storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
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

         if (_HasTimer)
         {
            if (_HideTimer != null)
            {
               _HideTimer.Stop();
               _HideTimer.Dispose();
            }
            _HideTimer = new Timer();
            _HideTimer.Interval = 2500;
            _HideTimer.Elapsed += (s, e) =>
            {
               _HideTimer.Stop();
               _HideTimer.Dispose();
               _HideTimer = null;
               Dispatcher.Invoke(() => AnimateClose());
            };
            _HideTimer.Start();
         }
      }
      public void ClearTimer()
      {
         if (_HideTimer == null)
            return;
         _HasTimer = false;
         _HideTimer.Stop();
         _HideTimer.Dispose();
         _HideTimer = null;
      }

      System.Timers.Timer? _HideTimer = null;

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
