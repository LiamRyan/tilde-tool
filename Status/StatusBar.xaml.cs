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
      protected bool _ShowAll = false;
      public StatusBar(bool hasTimer)
      {
         Width = System.Windows.SystemParameters.PrimaryScreenWidth;

         // Initialize
         InitializeComponent();

         // Bind event
         SourceManager.Instance.SourceChanged += Instance_SourceChanged;

         //
         ExpandBox.PreviewMouseDown += ExpandBox_PreviewMouseDown;
         ExpandBox.MouseEnter += Grid_MouseEnter;
         ExpandBox.MouseLeave += Grid_MouseLeave;

         // Spawn controls
         PopulateStatusBar();

         // Do an initial refresh of the controls.
         for (int i = 0; i < DisplayIndex.Count; i++)
            UpdateStatusBar(DisplayIndex[i], false);

         // Fade in.
         RootFrame.Opacity = 0;
         _HasTimer = hasTimer;
         AnimateShow();
      }

      private void ExpandBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
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
         storyboard.Begin(this);

         Dispatcher.Invoke(() =>
         {
            _ShowAll = !_ShowAll;
            ExpandText.Text = _ShowAll ? "-" : "+";

            // Spawn controls
            PopulateStatusBar();

            // Do an initial refresh of the controls.
            for (int i = 0; i < DisplayIndex.Count; i++)
               UpdateStatusBar(DisplayIndex[i], false);
         });
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
               DisplaySource[index].Status = "...working...";
               UpdateStatusBar(DisplayIndex[index], false);
               SourceManager.Instance.Query(DisplayIndex[index]);
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

            Dispatcher.Invoke(() => DisplaySource[index].HandleClick());
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
         Dispatcher.Invoke(() => UpdateStatusBar(args.Index, args.CacheChanged));
      }

      List<int> DisplayIndex = new List<int>();
      List<Source> DisplaySource = new List<Source>();
      protected void AddStatusElement()
      {
         DataTemplate? template = Resources["StatusBox"] as DataTemplate;
         ContentControl content = new ContentControl { ContentTemplate = template };
         StatusPanel.Children.Insert(StatusPanel.Children.Count - 1, content);
         content.ApplyTemplate();
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();

         int index = StatusPanel.Children.Count - 2;
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
         grid.PreviewMouseDown += (s, e) => Grid_PreviewMouseDown(s, e, index);
         grid.MouseEnter += Grid_MouseEnter;
         grid.MouseLeave += Grid_MouseLeave;
      }
      protected void PopulateStatusBar()
      {
         //
         List<int> oldList = DisplayIndex;
         List<int> newList = new List<int>();
         DisplayIndex = new List<int>();
         DisplaySource.Clear();
         for (int i = 0; i < SourceManager.Instance.Sources.Count; i++)
         {
            Source source = SourceManager.Instance.Sources[i];
            if (!_ShowAll)
            {
               if (source.State == Source.StateType.Inactive)
                  continue;
            }

            DisplayIndex.Add(i);
            DisplaySource.Add(source);

            if (_ShowAll)
               if (!oldList.Contains(i))
                  newList.Add(i);
         }

         //
         StatusPanel.Children.RemoveRange(0, StatusPanel.Children.Count - 1);
         {
            while (StatusPanel.Children.Count > DisplaySource.Count + 1)
               StatusPanel.Children.RemoveAt(StatusPanel.Children.Count - 2);
            while (StatusPanel.Children.Count < DisplaySource.Count + 1)
               AddStatusElement();
         }

         foreach (int index in newList)
            _AnimateShow(index);
      }

      private List<Storyboard> _Storyboards = new List<Storyboard>();
      public void UpdateStatusBar(int sourceIndex, bool fromUpdate)
      {
         // Look up which display index this corresponds to.
         Source src = SourceManager.Instance.Sources[sourceIndex];
         int index = DisplaySource.IndexOf(src);

         // If there was none, either add it (for an update), or ignore.
         if (index == -1)
         {
            if (!fromUpdate)
               return;
            index = DisplaySource.Count;
            DisplayIndex.Add(sourceIndex);
            DisplaySource.Add(src);
            AddStatusElement();
         }

         // Grab the controls.
         ContentControl content = StatusPanel.Children[index] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
         TextBlock title = grid.FindElementByName<TextBlock>("Title");
         Grid divider = grid.FindElementByName<Grid>("Divider");
         TextBlock subtitle = grid.FindElementByName<TextBlock>("Subtitle");
         TextBlock status = grid.FindElementByName<TextBlock>("Status");

         // Update.
         title.Text = DisplaySource[index].Title;
         subtitle.Text = DisplaySource[index].Subtitle;
         status.Text = DisplaySource[index].Status;
         status.Margin = new Thickness(0, String.IsNullOrEmpty(DisplaySource[index].Subtitle) ? 22 : 42, 0, 0);
         title.Foreground = new SolidColorBrush(DisplaySource[index].ColorDim);
         divider.Background = new SolidColorBrush(DisplaySource[index].ColorDim);
         subtitle.Foreground = new SolidColorBrush(DisplaySource[index].Color);
         status.Foreground = new SolidColorBrush(DisplaySource[index].Color);

         // Flash animation
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

      protected void _AnimateShow(int guiIndex)
      {
         //
         ContentControl content = StatusPanel.Children[guiIndex] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;

         //
         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4f));
            flashAnimation.From = 0.0f;
            flashAnimation.To = 1.0f;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.6f));
            flashAnimation.From = 0.0f;
            flashAnimation.To = 120.0f;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.WidthProperty));
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
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
