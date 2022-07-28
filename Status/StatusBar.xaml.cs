using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

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
         SourceManager.Instance.SourceQuery += Instance_SourceQuery;

         //
         ExpandBox.PreviewMouseDown += ExpandBox_PreviewMouseDown;
         ExpandBox.MouseEnter += (s, e) => Grid_MouseEnter(s, e, -1);
         ExpandBox.MouseLeave += (s, e) => Grid_MouseLeave(s, e, -1);

         // Spawn controls
         StatusPanel.Children.RemoveRange(0, StatusPanel.Children.Count - 1);
         PopulateStatusBar(true);

         // Fade in.
         _HasTimer = hasTimer;
         AnimateShow();
      }

      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
      }

      private void ExpandBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
      {
         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFF042508), KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
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
            PopulateStatusBar(false);
         });
      }

      private void Grid_MouseEnter(object sender, MouseEventArgs e, int sourceIndex)
      {
         Source? src = sourceIndex != -1 ? SourceManager.Instance.Sources[sourceIndex] : null;
         int index = DisplaySource.IndexOf(src);
         Color color = index == -1 ? Extension.FromArgb(0xFF042508) : DisplaySource[index].ColorBack;
         color = new Color { R = (byte)(color.R << 1), G = (byte)(color.G << 1), B = (byte)(color.B << 1), A = 255 };

         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            flashAnimation.To = color;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }
         if (sourceIndex != -1)
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4f));
            flashAnimation.To = 120.0f;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 8.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.HeightProperty));
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      private void Grid_MouseLeave(object sender, MouseEventArgs e, int sourceIndex)
      {
         Source? src = sourceIndex != -1 ? SourceManager.Instance.Sources[sourceIndex] : null;
         int index = DisplaySource.IndexOf(src);
         Color color = index == -1 ? Extension.FromArgb(0xFF042508) : DisplaySource[index].ColorBack;

         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.3f));
            flashAnimation.To = color;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }
         if (sourceIndex != -1)
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            flashAnimation.To = 80.0f;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.HeightProperty));
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      private void Grid_PreviewMouseDown(object sender, MouseEventArgs e, int sourceIndex)
      {
         Source? src = sourceIndex != -1 ? SourceManager.Instance.Sources[sourceIndex] : null;
         int index = DisplaySource.IndexOf(src);
         Color color = index == -1 ? Extension.FromArgb(0xFF042508) : DisplaySource[index].ColorBack;

         if (e.RightButton == MouseButtonState.Pressed)
         {
            Storyboard storyboard = new Storyboard();
            {
               var flashAnimation = new ColorAnimationUsingKeyFrames();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(color, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
               storyboard.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, sender as Grid);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
            }
            _Storyboards.Add(storyboard);
            storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
            storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);

            Dispatcher.Invoke(() =>
            {
               SourceManager.Instance.Query(DisplayIndex[index]);
               UpdateStatusBar(DisplayIndex[index], false);
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
               Storyboard.SetTarget(myDoubleAnimation, this);
               Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Window.OpacityProperty));
            }
            {
               var flashAnimation = new ColorAnimationUsingKeyFrames();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(color, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
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
         SourceManager.Instance.SourceQuery -= Instance_SourceQuery;
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
      private void Instance_SourceQuery(object? sender, int e)
      {
         Dispatcher.Invoke(() =>
         {
            if (SourceManager.Instance.Sources[e].Ephemeral)
               return;
            PopulateStatusBar(false);
            UpdateStatusBar(e, false);
         });
      }

      List<int> DisplayIndex = new List<int>();
      List<Source> DisplaySource = new List<Source>();
      Dictionary<int, DateTime> DisplayShow = new Dictionary<int, DateTime>();
      HashSet<int> DisplayShown = new HashSet<int>();
      protected void AddStatusElement(int sourceIndex, int guiIndex)
      {
         // insert to the gui
         DataTemplate? template = Resources["StatusBox"] as DataTemplate;
         ContentControl content = new ContentControl { ContentTemplate = template };
         StatusPanel.Children.Insert(guiIndex, content);
         content.ApplyTemplate();
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();

         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
         {
            int i = sourceIndex;
            grid.PreviewMouseDown += (s, e) => Grid_PreviewMouseDown(s, e, i);
            grid.MouseEnter += (s, e) => Grid_MouseEnter(s, e, i);
            grid.MouseLeave += (s, e) => Grid_MouseLeave(s, e, i);
         }

         // track our variables
         DisplayShown.Add(sourceIndex);
         DisplayIndex.Insert(guiIndex, sourceIndex);
         DisplaySource.Insert(guiIndex, SourceManager.Instance.Sources[sourceIndex]);

         // Do an initial refresh.
         UpdateStatusBar(sourceIndex, false);
      }
      protected void RemoveStatusElement(int sourceIndex)
      {
         int guiIndex = DisplayIndex.IndexOf(sourceIndex);
         if (guiIndex == -1)
            return;

         // instantly clear the shown state
         DisplayShown.Remove(sourceIndex);

         // animate disappearance -- this will clear everything else when done.
         _AnimateHide(guiIndex);
      }
      protected void PopulateStatusBar(bool initial)
      {
         //
         int nextGuiIndex = 0;
         for (int sourceIndex = 0; sourceIndex < SourceManager.Instance.Sources.Count; sourceIndex++)
         {
            // Decide whether to show this source.
            bool showThis = true;
            Source source = SourceManager.Instance.Sources[sourceIndex];
            if (!_ShowAll)
            {
               DateTime showUntil;
               if (DisplayShow.TryGetValue(sourceIndex, out showUntil))
               {
                  if (DateTime.Now >= showUntil)
                     DisplayShow.Remove(sourceIndex);
               }
               else
                  showUntil = DateTime.Now;

               if (DateTime.Now < showUntil)
                  showThis = true;
               else if (!source.Ephemeral && (source.IsQuerying || SourceManager.Instance.NeedRefresh(source)))
                  showThis = true;
               else if (source.State == Source.StateType.Inactive)
                  showThis = false;
            }

            // Track how many guis are visible (even if fading out).
            bool isVisible = DisplayIndex.Contains(sourceIndex);
            if (isVisible || showThis)
               nextGuiIndex++;
            bool isFadeIn = DisplayShown.Contains(sourceIndex);

            // Check if we're not showing it.
            if (!showThis)
            {
               // If it wanted to be shown, clear it out.
               if (DisplayShown.Contains(sourceIndex))
                  RemoveStatusElement(sourceIndex);
               continue;
            }

            // We want to show.  Check if it's altogether missing, and insert if so.
            if (!isVisible)
               AddStatusElement(sourceIndex, nextGuiIndex - 1);

            // If it was either missing or in the process of fading out, do the appear animation.
            if (!isFadeIn)
            {
               if (!initial)
                  _AnimateShow(sourceIndex);
               DisplayShown.Add(sourceIndex);
            }
         }
      }

      private List<Storyboard> _Storyboards = new List<Storyboard>();
      public void UpdateStatusBar(int sourceIndex, bool fromUpdate)
      {
         // Look up which display index this corresponds to.
         Source src = SourceManager.Instance.Sources[sourceIndex];
         int guiIndex = DisplaySource.IndexOf(src);

         // Make sure to show it at least 5 seconds after an update.
         if (fromUpdate)
            DisplayShow[sourceIndex] = DateTime.Now + TimeSpan.FromSeconds(5);

         // If there was none, either add it (for an update), or ignore.
         if (guiIndex == -1)
         {
            if (!fromUpdate)
               return;
            PopulateStatusBar(false);
            guiIndex = DisplaySource.IndexOf(src);
         }

         // Grab the controls.
         ContentControl content = StatusPanel.Children[guiIndex] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;
         TextBlock title = grid.FindElementByName<TextBlock>("Title");
         Grid divider = grid.FindElementByName<Grid>("Divider");
         TextBlock article = grid.FindElementByName<TextBlock>("Article");
         TextBlock status = grid.FindElementByName<TextBlock>("Status");
         Ellipse progress = grid.FindElementByName<Ellipse>("Progress");
         Shape.Arc progressArc = grid.FindElementByName<Shape.Arc>("ProgressArc");

         // Update.
         title.Text = DisplaySource[guiIndex].Subtitle;
         status.Text = DisplaySource[guiIndex].Status;
         article.Text = DisplaySource[guiIndex].Article;
         article.Margin = new Thickness(article.Margin.Left, article.Margin.Top, article.Margin.Right, String.IsNullOrEmpty(DisplaySource[guiIndex].Status) ? 0 : 17);
         grid.Background = new SolidColorBrush(DisplaySource[guiIndex].ColorBack);
         title.Foreground = new SolidColorBrush(DisplaySource[guiIndex].ColorDim);
         divider.Background = new SolidColorBrush(DisplaySource[guiIndex].ColorDim);
         status.Foreground = new SolidColorBrush(DisplaySource[guiIndex].ColorDim);
         article.Foreground = new SolidColorBrush(DisplaySource[guiIndex].Color);
         progress.Stroke = new SolidColorBrush(DisplaySource[guiIndex].ColorDim);
         {
            GradientStopCollection collection = new GradientStopCollection(2);
            collection.Add((progressArc.Stroke as LinearGradientBrush).GradientStops[0]);
            collection.Add(new GradientStop(DisplaySource[guiIndex].Color, 1.0));
            collection.Freeze();
            LinearGradientBrush oldBrush = progressArc.Stroke as LinearGradientBrush;
            progressArc.Stroke = new LinearGradientBrush(collection, oldBrush.StartPoint, oldBrush.EndPoint);
         }

         // Progress animation.
         bool pendQuery = !src.Ephemeral && (src.IsQuerying || SourceManager.Instance.NeedRefresh(src));
         progress.Visibility = pendQuery ? Visibility.Visible : Visibility.Collapsed;
         progressArc.Visibility = pendQuery ? Visibility.Visible : Visibility.Collapsed;
         if (pendQuery)
         {
            Storyboard storyboard = new Storyboard();
            {
               var flashAnimation = new DoubleAnimation();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(1.0f));
               flashAnimation.To = 360.0f;
               flashAnimation.RepeatBehavior = RepeatBehavior.Forever;
               storyboard.Children.Add(flashAnimation);
               progressArc.RenderTransform = new RotateTransform(0, 0, 0);
               Storyboard.SetTarget(flashAnimation, progressArc);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("RenderTransform.Angle"));
            }
            _Storyboards.Add(storyboard);
            storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
            storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
         }

         // Flash animation
         if (fromUpdate)
         {
            ClearTimer();

            Storyboard storyboard = new Storyboard();
            {
               var flashAnimation = new ColorAnimationUsingKeyFrames();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(DisplaySource[guiIndex].ColorBack, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
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
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            flashAnimation.From = 0.0f;
            flashAnimation.To = 1.0f;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            flashAnimation.From = 0.0f;
            flashAnimation.To = 120.0f;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            animation.To = new Thickness(1, 1, 1, 1);
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, grid);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      protected void _AnimateHide(int guiIndex)
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
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            flashAnimation.To = 0.0f;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            flashAnimation.To = 0.0f;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            animation.To = new Thickness(0,0,0,0);
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, grid);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         _Storyboards.Add(storyboard);

         int sourceIndex = DisplayIndex[guiIndex];
         storyboard.Completed += (sender, e) =>
         {
            _Storyboards.Remove(storyboard);
            storyboard.Remove(this);

            // finalize the removal
            int _guiIndex = DisplayIndex.IndexOf(sourceIndex);
            StatusPanel.Children.RemoveAt(_guiIndex);
            DisplayIndex.RemoveAt(_guiIndex);
            DisplaySource.RemoveAt(_guiIndex);
         };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      public bool IsShowing { get; protected set; }
      Storyboard _StoryboardHide;
      public void AnimateShow()
      {
         IsShowing = true;

         if (_StoryboardHide != null)
         {
            _StoryboardHide.Stop(this);
            _StoryboardHide.Remove(this);
         }

         //
         _StoryboardHide = new Storyboard();
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 0.0f;
            animation.To = Width;
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.From = 8.0f;
            animation.To = 2.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.From = 8.0f;
            animation.To = 2.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.From = 46.0f;
            animation.To = Height;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.3f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            Border.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.3f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            StatusPanel.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, StatusPanel);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
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
            _StoryboardHide.Stop(this);
            _StoryboardHide.Remove(this);
         }

         //
         _StoryboardHide = new Storyboard();
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.17f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.To = 0.0f;
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn };
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            animation.To = 0.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            animation.To = 0.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, StatusPanel);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 8.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 8.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 46.0f;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.35f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardHide.Completed += (sender, e) => { if (_StoryboardHide != null) _StoryboardHide.Remove(this); _StoryboardHide = null; Close(); };
         _StoryboardHide.Begin(this);
      }
   }
}
