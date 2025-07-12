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
         InitializeComponent();

         // Bind event
         SourceManager.Instance.SourceChanged += Instance_SourceChanged;
         SourceManager.Instance.SourceQuery += Instance_SourceQuery;

         //
         ExpandBox.PreviewMouseDown += ExpandBox_PreviewMouseDown;
         ExpandBox.MouseEnter += (s, e) => Grid_MouseEnter(s, e, -1);
         ExpandBox.MouseLeave += (s, e) => Grid_MouseLeave(s, e, -1);

         // Spawn feed controls
         FeedPanel.Children.RemoveRange(0, FeedPanel.Children.Count - 1);
         PopulateFeedBar(true);

         // Spawn status controls
         {
            // Clear out old or preview controls
            StatusPanel.Children.RemoveRange(0, StatusPanel.Children.Count);

            // Now spawn new ones for each non-feed source.
            for (int i = 0; i < SourceManager.Instance.Sources.Count; i++)
            {
               if (SourceManager.Instance.Sources[i].IsFeed)
                  continue;

               // insert to the gui
               DataTemplate? template = Resources["StatusBox"] as DataTemplate;
               ContentControl content = new ContentControl { ContentTemplate = template };
               StatusPanel.Children.Add(content);
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();

               FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
               {
                  int sourceIndex = i;
                  grid.PreviewMouseDown += (s, e) => Grid_PreviewMouseDown(s, e, sourceIndex);
                  grid.MouseEnter += (s, e) => Grid_MouseEnter(s, e, sourceIndex);
                  grid.MouseLeave += (s, e) => Grid_MouseLeave(s, e, sourceIndex);
               }

               // Track gui to source linkage
               StatusDisplayIndex.Add(i);

               // Do an initial refresh.
               UpdatePanel(i, false);
            }
         }

         // Fade in.
         _HasTimer = hasTimer;
         AnimateShow();

         if (_HasTimer)
            App.PlayBeep(App.BeepSound.Notify);
         else
            App.PlayBeep(App.BeepSound.Wake);

         Focus();
      }

      double TargetHeight = -1.0f;
      protected override Size ArrangeOverride(Size arrangeBounds)
      {
         Size result = base.ArrangeOverride(arrangeBounds);
         Top = App.GetBarTop(0.0) + 62.0 - (0.5 * result.Height);
         AnimateResize();
         return result;
      }

      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
         Focus();
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
            PopulateFeedBar(false);
         });
      }

      Panel HoverElement;
      private void Grid_MouseEnter(object sender, MouseEventArgs e, int sourceIndex)
      {
         HoverElement = sender as Panel;

         int guiIndex = DisplayIndex.IndexOf(sourceIndex);
         if (guiIndex != -1)
            Panel.SetZIndex(FeedPanel.Children[guiIndex], 1);

         Source? src = sourceIndex != -1 ? SourceManager.Instance.Sources[sourceIndex] : null;
         Color color = src == null ? Extension.FromArgb(0xFF042508) : src.ColorBack;
         color = new Color { R = (byte)(color.R << 1), G = (byte)(color.G << 1), B = (byte)(color.B << 1), A = 255 };
         TextBlock article = (sender as Panel).FindElementByName<TextBlock>("Article");

         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            flashAnimation.To = color;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Panel);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }
         if (sourceIndex != -1)
         {
            {
               var flashAnimation = new DoubleAnimation();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4f));
               flashAnimation.To = src.IsFeed ? 140.0f : 42.0f;
               flashAnimation.EasingFunction = new ExponentialEase { Exponent = 8.0, EasingMode = EasingMode.EaseOut };
               storyboard.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, sender as Panel);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.HeightProperty));
            }
            if (src.IsFeed)
            {
               {
                  var flashAnimation = new DoubleAnimation();
                  flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.4f));
                  flashAnimation.To = 160.0f;
                  flashAnimation.EasingFunction = new ExponentialEase { Exponent = 8.0, EasingMode = EasingMode.EaseOut };
                  storyboard.Children.Add(flashAnimation);
                  Storyboard.SetTarget(flashAnimation, sender as Panel);
                  Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.WidthProperty));
               }
               {
                  var animation = new ThicknessAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(0.39f));
                  double inset = (_ShowAll ? 120 : 140) - 160;
                  animation.To = new Thickness(1 + (0.5f * inset), 1, 1 + (0.5f * inset), 1);
                  animation.EasingFunction = new ExponentialEase { Exponent = 8.0, EasingMode = EasingMode.EaseOut };
                  storyboard.Children.Add(animation);
                  Storyboard.SetTarget(animation, sender as Panel);
                  Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
               }
               {
                  var flashAnimation = new DoubleAnimation();
                  flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.01f));
                  flashAnimation.To = 160.0f - 13.0f;
                  flashAnimation.EasingFunction = new ExponentialEase { Exponent = 8.0, EasingMode = EasingMode.EaseOut };
                  storyboard.Children.Add(flashAnimation);
                  Storyboard.SetTarget(flashAnimation, article);
                  Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.WidthProperty));
               }
            }
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      private void Grid_MouseLeave(object sender, MouseEventArgs e, int sourceIndex)
      {
         if (HoverElement == sender)
         {
            int guiIndex = DisplayIndex.IndexOf(sourceIndex);
            if (guiIndex != -1)
               Panel.SetZIndex(FeedPanel.Children[guiIndex], 0);
            HoverElement = null;
         }

         Source? src = sourceIndex != -1 ? SourceManager.Instance.Sources[sourceIndex] : null;
         Color color = src == null ? Extension.FromArgb(0xFF042508) : src.ColorBack;
         TextBlock article = (sender as Panel).FindElementByName<TextBlock>("Article");

         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new ColorAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.3f));
            flashAnimation.To = color;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, sender as Panel);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }
         if (sourceIndex != -1)
         {
            {
               var flashAnimation = new DoubleAnimation();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
               flashAnimation.To = src.IsFeed ? 100.0f : 24.0f;
               flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
               storyboard.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, sender as Panel);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.HeightProperty));
            }
            if (src.IsFeed)
            {
               {
                  var flashAnimation = new DoubleAnimation();
                  flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
                  flashAnimation.To = _ShowAll ? 120 : 140;
                  flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
                  storyboard.Children.Add(flashAnimation);
                  Storyboard.SetTarget(flashAnimation, sender as Panel);
                  Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.WidthProperty));
               }
               {
                  var animation = new ThicknessAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(0.51f));
                  animation.To = new Thickness(1, 1, 1, 1);
                  animation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
                  storyboard.Children.Add(animation);
                  Storyboard.SetTarget(animation, sender as Panel);
                  Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
               }
               {
                  var flashAnimation = new DoubleAnimation();
                  flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.01f));
                  flashAnimation.To = (_ShowAll ? 120 : 140) - 13.0f;
                  flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
                  storyboard.Children.Add(flashAnimation);
                  Storyboard.SetTarget(flashAnimation, article);
                  Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.WidthProperty));
               }
            }
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      private void Grid_PreviewMouseDown(object sender, MouseEventArgs e, int sourceIndex)
      {
         Source? src = sourceIndex != -1 ? SourceManager.Instance.Sources[sourceIndex] : null;
         Color color = src == null ? Extension.FromArgb(0xFF042508) : src.ColorBack;

         if (e.RightButton == MouseButtonState.Pressed)
         {
            Storyboard storyboard = new Storyboard();
            {
               var flashAnimation = new ColorAnimationUsingKeyFrames();
               flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25f));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(color, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
               storyboard.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, sender as Panel);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
            }
            _Storyboards.Add(storyboard);
            storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
            storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);

            Dispatcher.Invoke(() =>
            {
               SourceManager.Instance.Query(sourceIndex, clearCache: true);
               UpdatePanel(sourceIndex, false);
            });
         }
         else
         {
            IsShowing = false;

            if (_StoryboardHide != null)
            {
               _StoryboardHide.Stop(this);
               _StoryboardHide.Remove(this);
            }

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

            Dispatcher.Invoke(() => src.HandleClick());

            App.PlayBeep(App.BeepSound.Accept);
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
            App.PlayBeep(App.BeepSound.Accept);
         }
         else if (e.Key == Key.Escape)
         {
            AnimateClose();
            App.PlayBeep(App.BeepSound.Cancel);
         }
      }

      private void Instance_SourceChanged(object? sender, SourceManager.SourceEventArgs args)
      {
         Dispatcher.Invoke(() => UpdatePanel(args.Index, args.CacheChanged));
      }
      private void Instance_SourceQuery(object? sender, int e)
      {
         Dispatcher.Invoke(() =>
         {
            //if (SourceManager.Instance.Sources[e].Ephemeral)
            //   return;
            PopulateFeedBar(false);
            UpdatePanel(e, false);
         });
      }

      List<int> StatusDisplayIndex = new List<int>();
      List<int> DisplayIndex = new List<int>();
      List<Source> DisplaySource = new List<Source>();
      Dictionary<int, DateTime> DisplayShow = new Dictionary<int, DateTime>();
      HashSet<int> DisplayNew = new HashSet<int>();
      HashSet<int> DisplayShown = new HashSet<int>();
      protected void AddFeedElement(int sourceIndex, int guiIndex)
      {
         // insert to the gui
         DataTemplate? template = Resources["FeedBox"] as DataTemplate;
         ContentControl content = new ContentControl { ContentTemplate = template };
         FeedPanel.Children.Insert(guiIndex, content);
         Panel.SetZIndex(content, -guiIndex);
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
         UpdatePanel(sourceIndex, false);
      }
      protected void RemoveFeedElement(int sourceIndex, double pause)
      {
         int guiIndex = DisplayIndex.IndexOf(sourceIndex);
         if (guiIndex == -1)
            return;

         // instantly clear the shown state
         DisplayShown.Remove(sourceIndex);

         // animate disappearance -- this will clear everything else when done.
         _AnimateHide(guiIndex, pause);
      }
      protected void PopulateFeedBar(bool initial)
      {
         // Start by sorting.
         List<int> sourceList = new List<int>();
         for (int sourceIndex = 0; sourceIndex < SourceManager.Instance.Sources.Count; sourceIndex++)
            if (SourceManager.Instance.Sources[sourceIndex].IsFeed)
               sourceList.Add(sourceIndex);
         sourceList.Sort((a, b) => SourceManager.Instance.Sources[a].Order.CompareTo(SourceManager.Instance.Sources[b].Order));

         //
         int showCount = 0;
         int hideCount = 0;
         int nextGuiIndex = 0;
         foreach (int sourceIndex in sourceList)
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
               else if (source.IsQuerying || SourceManager.Instance.NeedRefresh(source))
                  showThis = true;
               else if (!source.Important)
                  showThis = false;
            }

            // Track how many guis are visible (even if fading out).
            int prevGuiIndex = DisplayIndex.IndexOf(sourceIndex);
            bool isVisible = prevGuiIndex != -1;
            if (isVisible || showThis)
               nextGuiIndex++;
            bool isFadeIn = DisplayShown.Contains(sourceIndex);

            // Check if we're not showing it.
            if (!showThis)
            {
               // If it wanted to be shown, clear it out.
               if (DisplayShown.Contains(sourceIndex))
               {
                  RemoveFeedElement(sourceIndex, 0.05 * Math.Sqrt((double)hideCount));
                  hideCount++;
               }
               continue;
            }

            // We want to show.  Check if it's altogether missing, and insert if so.
            if (!isVisible)
               AddFeedElement(sourceIndex, nextGuiIndex - 1);
            // If it was showing, make sure it's in the right order.
            else if (prevGuiIndex != nextGuiIndex - 1)
            {
               // We know prevGuiIndex is greater than nextGuiIndex - 1, because we've already checked for all the
               //  indices before nextGuiIndex - 1 and know they match what they're supposed to.
               DisplayIndex.RemoveAt(prevGuiIndex);
               DisplaySource.RemoveAt(prevGuiIndex);
               DisplayIndex.Insert(nextGuiIndex - 1, sourceIndex);
               DisplaySource.Insert(nextGuiIndex - 1, SourceManager.Instance.Sources[sourceIndex]);

               // change the gui order
               ContentControl content = FeedPanel.Children[prevGuiIndex] as ContentControl;
               FeedPanel.Children.RemoveAt(prevGuiIndex);
               FeedPanel.Children.Insert(nextGuiIndex - 1, content);

               // TODO: animated movement transition
            }

            // If it was either missing or in the process of fading out, do the appear animation.
            if (!isFadeIn)
            {
               if (!initial)
               {
                  _AnimateShow(nextGuiIndex - 1, 0.03 * Math.Sqrt((double)showCount));
                  showCount++;
               }
               DisplayShown.Add(sourceIndex);
            }
            else if (!initial)
               _AnimateResize(nextGuiIndex - 1);
         }
      }

      private List<Storyboard> _Storyboards = new List<Storyboard>();
      public void UpdatePanel(int sourceIndex, bool fromUpdate)
      {
         // Look up the source
         Source src = SourceManager.Instance.Sources[sourceIndex];

         // If it's a feed, handle using the normal feed code.
         Panel grid;
         Grid? progressGrid = null;
         if (src.IsFeed)
         {
            int guiIndex = DisplaySource.IndexOf(src);

            // Make sure to show it at least 5 seconds after an update.
            if (fromUpdate)
            {
               DisplayShow[sourceIndex] = DateTime.Now + TimeSpan.FromSeconds(5);
               DisplayNew.Add(sourceIndex);
            }

            // If there was none, either add it (for an update), or ignore.
            if (guiIndex == -1)
            {
               if (!fromUpdate)
                  return;
               PopulateFeedBar(false);
               guiIndex = DisplaySource.IndexOf(src);
            }

            // Grab the controls.
            ContentControl content = FeedPanel.Children[guiIndex] as ContentControl;
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            grid = VisualTreeHelper.GetChild(presenter, 0) as Panel;
            TextBlock title = grid.FindElementByName<TextBlock>("Title");
            Grid divider = grid.FindElementByName<Grid>("Divider");
            TextBlock article = grid.FindElementByName<TextBlock>("Article");
            TextBlock status = grid.FindElementByName<TextBlock>("Status");
            TextBlock isnew = grid.FindElementByName<TextBlock>("New");

            // Update.
            // TODO: animate.
            title.Text = src.Subtitle;
            status.Text = src.Status;
            article.Text = src.Article;
            article.Margin = new Thickness(article.Margin.Left, article.Margin.Top, article.Margin.Right, String.IsNullOrEmpty(src.Status) ? 0 : 17);
            title.Foreground = new SolidColorBrush(src.ColorDim);
            divider.Background = new SolidColorBrush(src.ColorDim);
            status.Foreground = new SolidColorBrush(src.ColorDim);
            article.Foreground = new SolidColorBrush(src.Color);
            isnew.Foreground = new SolidColorBrush(src.Color);
            isnew.Visibility = DisplayNew.Contains(sourceIndex) ? Visibility.Visible : Visibility.Collapsed;
         }
         else
         {
            int guiIndex = StatusDisplayIndex.IndexOf(sourceIndex);

            // Grab the controls.
            ContentControl content = StatusPanel.Children[guiIndex] as ContentControl;
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            grid = VisualTreeHelper.GetChild(presenter, 0) as Panel;
            TextBlock title = grid.FindElementByName<TextBlock>("Title");
            Grid divider = grid.FindElementByName<Grid>("Divider");
            TextBlock article = grid.FindElementByName<TextBlock>("Article");
            TextBlock status = grid.FindElementByName<TextBlock>("Status");
            progressGrid = grid.FindElementByName<Grid>("ProgressGrid");

            // Update.
            title.Text = src.Subtitle;
            status.Text = src.Status;
            article.Text = src.Article;
            title.Foreground = new SolidColorBrush(src.ColorDim);
            divider.Background = new SolidColorBrush(src.ColorDim);
            status.Foreground = new SolidColorBrush(src.ColorDim);
            article.Foreground = new SolidColorBrush(src.Color);

            title.VerticalAlignment = string.IsNullOrEmpty(src.Status) ? VerticalAlignment.Center : VerticalAlignment.Top;
            title.Margin = new Thickness(title.Margin.Left, string.IsNullOrEmpty(src.Status) ? 0 : 6, title.Margin.Right, title.Margin.Bottom);
         }

         // These are common to all types
         grid.Background = new SolidColorBrush(src.ColorBack);

         Ellipse progress = grid.FindElementByName<Ellipse>("Progress");
         Shape.Arc progressArc = grid.FindElementByName<Shape.Arc>("ProgressArc");
         progress.Stroke = new SolidColorBrush(src.ColorDim);
         {
            GradientStopCollection collection = new GradientStopCollection(2);
            collection.Add((progressArc.Stroke as LinearGradientBrush).GradientStops[0]);
            collection.Add(new GradientStop(src.Color, 1.0));
            collection.Freeze();
            LinearGradientBrush oldBrush = progressArc.Stroke as LinearGradientBrush;
            progressArc.Stroke = new LinearGradientBrush(collection, oldBrush.StartPoint, oldBrush.EndPoint);
         }

         // Progress animation.
         bool pendQuery = src.IsQuerying || SourceManager.Instance.NeedRefresh(src);
         if (progressGrid != null)
            progressGrid.Visibility = pendQuery ? Visibility.Visible : Visibility.Collapsed;
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
               flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(src.ColorBack, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
               storyboard.Children.Add(flashAnimation);
               Storyboard.SetTarget(flashAnimation, grid);
               Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
            }
            _Storyboards.Add(storyboard);
            storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
            storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
         }
      }

      protected void _AnimateShow(int guiIndex, double showPause)
      {
         //
         ContentControl content = FeedPanel.Children[guiIndex] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
         TextBlock article = grid.FindElementByName<TextBlock>("Article");

         //
         Storyboard storyboard = new Storyboard();
         {
            grid.Opacity = 0.0f;
            var flashAnimation = new DoubleAnimation(0.0f, 1.0f, new Duration(TimeSpan.FromSeconds(0.45f))) { BeginTime = TimeSpan.FromSeconds(showPause) };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.BeginTime = TimeSpan.FromSeconds(showPause);
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            grid.Width = 0.0f;
            flashAnimation.From = 0.0f;
            flashAnimation.To = _ShowAll ? 120 : 140;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.0f));
            flashAnimation.To = (_ShowAll ? 120 : 140) - 13.0f;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, article);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.WidthProperty));
         }
         {
            var animation = new ThicknessAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(showPause);
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
      protected void _AnimateHide(int guiIndex, double pause)
      {
         //
         ContentControl content = FeedPanel.Children[guiIndex] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;

         //
         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.BeginTime = TimeSpan.FromSeconds(pause);
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            flashAnimation.To = 0.0f;
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.BeginTime = TimeSpan.FromSeconds(pause);
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            flashAnimation.To = 0.0f;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var animation = new ThicknessAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(pause);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            animation.To = new Thickness(0, 0, 0, 0);
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
            FeedPanel.Children.RemoveAt(_guiIndex);
            DisplayIndex.RemoveAt(_guiIndex);
            DisplaySource.RemoveAt(_guiIndex);
         };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      protected void _AnimateReorder(int oldGuiIndex, int newGuiIndex)
      {
         // TODO: implement this
         ContentControl content = FeedPanel.Children[newGuiIndex] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
      }
      protected void _AnimateResize(int guiIndex)
      {
         //
         ContentControl content = FeedPanel.Children[guiIndex] as ContentControl;
         ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
         presenter.ApplyTemplate();
         FrameworkElement grid = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
         TextBlock article = grid.FindElementByName<TextBlock>("Article");

         if (grid == HoverElement)
            return;

         //
         Storyboard storyboard = new Storyboard();
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.45f));
            flashAnimation.To = _ShowAll ? 120 : 140;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, grid);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Grid.WidthProperty));
         }
         {
            var flashAnimation = new DoubleAnimation();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.1f));
            flashAnimation.To = (_ShowAll ? 120 : 140) - 13.0f;
            flashAnimation.EasingFunction = new ExponentialEase { Exponent = 8.0, EasingMode = EasingMode.EaseOut };
            storyboard.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, article);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath(Panel.WidthProperty));
         }
         _Storyboards.Add(storyboard);
         storyboard.Completed += (sender, e) => { _Storyboards.Remove(storyboard); storyboard.Remove(this); };
         storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      public bool IsShowing { get; protected set; }
      Storyboard _StoryboardHide;
      Storyboard _StoryboardResize;
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
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = new Thickness(-10, 20, -10, 20);
            animation.To = new Thickness(-10, 10, -10, 10);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn };
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         {
            var animation = new ColorAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.0f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.To = Extension.FromArgb(0xFF021204);
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Fill.Color"));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 8.0f;
            animation.To = 2.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 8.0f;
            animation.To = 2.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         /*{
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
            Border.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.OpacityProperty));
         }*/
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
            Inner.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Inner);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
            animation.To = 1.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardHide.Completed += (sender, e) => { if (_StoryboardHide != null) _StoryboardHide.Remove(this); _StoryboardHide = null; };
         _StoryboardHide.Begin(this);

         AnimateResize(open: true);

         if (_HasTimer)
            SetTimer();
      }

      System.Timers.Timer? _HideTimer = null;

      public void SetTimer()
      {
         // Clear the old
         if (_HideTimer != null)
         {
            _HideTimer.Stop();
            _HideTimer.Dispose();
         }

         // Start the new.
         _HideTimer = new Timer();
         _HideTimer.Interval = 2500;
         _HideTimer.Elapsed += (s, e) =>
         {
            _HideTimer.Stop();
            _HideTimer.Dispose();
            _HideTimer = null;
            Dispatcher.Invoke(() => AnimateClose());
            App.PlayBeep(App.BeepSound.Cancel);
         };
         _HideTimer.Start();
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
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = new Thickness(-10, 20, -10, 20);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         /*
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.OpacityProperty));
         }
         */
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, Inner);
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
            animation.BeginTime = TimeSpan.FromSeconds(0.25f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardHide.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardHide.Completed += (sender, e) => { if (_StoryboardHide != null) _StoryboardHide.Remove(this); _StoryboardHide = null; Close(); };
         _StoryboardHide.Begin(this);

         AnimateResize(close: true);
      }

      public void AnimateResize(bool open = false, bool close = false)
      {
         if (!IsShowing && !close)
            return;

         double targetHeight = close ? 46.0f : Inner.DesiredSize.Height;
         if (targetHeight == TargetHeight)
            return;

         if (TargetHeight <= 0)
            open = true;
         TargetHeight = targetHeight;

         if (_StoryboardResize != null)
         {
            _StoryboardResize.Stop(this);
            _StoryboardResize.Remove(this);
         }

         _StoryboardResize = new Storyboard();
         if (open)
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            Content.Height = 46.0f;
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(46.0f, TimeSpan.FromSeconds(0)));
            //animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height / 2, TimeSpan.FromSeconds(0.2f), new ExponentialEase { Exponent = 2.0, EasingMode = EasingMode.EaseIn }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(TargetHeight, TimeSpan.FromSeconds(0.33f), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut }));
            _StoryboardResize.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         else if (close)
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 46.0f;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut };
            _StoryboardResize.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         else
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = TargetHeight;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut };
            _StoryboardResize.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }

         _StoryboardResize.Completed += (sender, e) => { if (_StoryboardResize != null) _StoryboardResize.Remove(this); _StoryboardResize = null; };
         _StoryboardResize.Begin(this);
      }
   }
}
