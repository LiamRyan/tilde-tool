using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Tildetool.Time
{
   /// <summary>
   /// Interaction logic for Timekeep.xaml
   /// </summary>
   public partial class Timekeep : Window
   {
      #region Events

      public delegate void PopupEvent(object sender);
      public event PopupEvent? OnFinish;

      #endregion

      public TimeBar TimeBar;
      public ProjectBar ProjectBar;
      public IndicatorBar IndicatorBar;
      public IndicatorGraphPane IndicatorGraphPane;
      public SummaryPane SummaryPane;
      public TimekeepTextEditor TimekeepTextEditor;

      public Timekeep()
      {
         Width = System.Windows.SystemParameters.PrimaryScreenWidth;
         InitializeComponent();
         Top = App.GetBarTop(124.0);

         IndicatorHover.Visibility = Visibility.Collapsed;
         DailyRowHover.Visibility = Visibility.Collapsed;
         DailyRowHoverL.Visibility = Visibility.Collapsed;
         DailyRowHoverR.Visibility = Visibility.Collapsed;
         TextEditorPane.Visibility = Visibility.Collapsed;

         ProjectBar = new(this);
         IndicatorBar = new(this);
         IndicatorGraphPane = new(this);
         SummaryPane = new(this);
         TimekeepTextEditor = new(this);

         DailyDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
         ProjectBar.RebuildList();
         RefreshTime();
         Refresh();

         _AnimateIn();

         StartTick();
      }

      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);

         StopTick();
         UnscheduleCancel();
      }

      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
      }

      #region Second Ticker

      Timer? _Timer;
      void StartTick()
      {
         _Timer = new Timer { Interval = 1000 };
         _Timer.Elapsed += (o, e) => { Dispatcher.Invoke(() => RefreshTime()); };
         _Timer.Start();
      }

      void StopTick()
      {
         if (_Timer != null)
         {
            _Timer.Stop();
            _Timer.Dispose();
            _Timer = null;
         }
      }

      #endregion
      #region Cancel Timer

      Timer? _CancelTimer;
      public void ScheduleCancel(int timeMs = 4000)
      {
         UnscheduleCancel();

         _CancelTimer = new Timer();
         _CancelTimer = new Timer { Interval = timeMs };
         _CancelTimer.Elapsed += (o, e) => { _CancelTimer.Stop(); Dispatcher.Invoke(() => Cancel()); };
         _CancelTimer.Start();
      }

      public void UnscheduleCancel()
      {
         if (_CancelTimer != null)
         {
            _CancelTimer.Stop();
            _CancelTimer.Dispose();
            _CancelTimer = null;
         }
      }

      #endregion

      public bool _Finished = false;
      public void Cancel()
      {
         if (_Finished)
            return;
         _AnimateFadeOut();
      }

      private void Window_KeyDown(object sender, KeyEventArgs e)
      {
         if (_Finished)
            return;

         if (TimekeepTextEditor.HandleKeyDown(sender, e))
            return;

         if (IndicatorBar.HandleKeyDown(sender, e))
         {
            e.Handled = true;
            return;
         }
         if (ProjectBar.HandleKeyDown(sender, e))
         {
            e.Handled = true;
            return;
         }

         // Handle escape
         switch (e.Key)
         {
            case Key.Escape:
               e.Handled = true;
               Cancel();
               return;

            case Key.Return:
               string target;
               if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                  target = "TimekeepCache.json";
               else
                  target = "TimekeepHistory.db";
               try
               {
                  Process process = new Process();
                  ProcessStartInfo startInfo = new ProcessStartInfo();
                  startInfo.FileName = $"{System.IO.Directory.GetCurrentDirectory()}\\{target}";
                  startInfo.UseShellExecute = true;
                  process.StartInfo = startInfo;
                  process.Start();
               }
               catch (Exception ex)
               {
                  MessageBox.Show(ex.ToString());
                  App.WriteLog(ex.Message);
               }
               e.Handled = true;
               Cancel();
               return;

            case Key.Left:
               DailyDay = DailyDay.AddDays(CurDailyMode == DailyMode.Today ? -1 : -7);
               Refresh();
               e.Handled = true;
               return;

            case Key.Right:
               DailyDay = DailyDay.AddDays(CurDailyMode == DailyMode.Today ? 1 : 7);
               Refresh();
               e.Handled = true;
               return;

            case Key.Up:
               DailyDay = DailyDay.AddDays(-7 * (CurDailyMode == Timekeep.DailyMode.Summary ? -1 : 1));
               Refresh();
               e.Handled = true;
               return;

            case Key.Down:
               DailyDay = DailyDay.AddDays(7 * (CurDailyMode == Timekeep.DailyMode.Summary ? -1 : 1));
               Refresh();
               e.Handled = true;
               return;

            case Key.Tab:
               CurDailyMode = (DailyMode)((int)(CurDailyMode + 1) % (int)DailyMode.COUNT);
               Refresh();
               e.Handled = true;
               return;
         }
      }

      public static string IntervalTime(int totalTimeMin)
      {
         int totalTimeHour = totalTimeMin / 60;
         int totalTimeDay = totalTimeHour / 8;
         int totalTimeWeek = totalTimeDay / 5;
         StringBuilder stringBuilder = new StringBuilder();
         if (totalTimeWeek > 0)
            stringBuilder.Append(totalTimeWeek.ToString() + "w ");
         if (totalTimeHour > 12)
         {
            stringBuilder.Append((totalTimeDay % 5).ToString() + "d ");
            stringBuilder.Append((totalTimeHour % 8).ToString() + ":" + String.Format("{0:00}", totalTimeMin % 60));
         }
         else
            stringBuilder.Append(totalTimeHour.ToString() + ":" + String.Format("{0:00}", totalTimeMin % 60));
         return stringBuilder.ToString();
      }

      #region Refresh

      public void RefreshTime()
      {
         // Refresh current time
         if (TimeManager.Instance.CurrentProject != null)
         {
            int mins = (int)(DateTime.UtcNow - TimeManager.Instance.CurrentStartTime).TotalMinutes;
            int hours = mins / 60;
            CurrentTimeH.Text = hours.ToString();
            CurrentTimeM.Text = String.Format("{0:00}", mins % 60);
         }
      }

      public DateTime DailyDay;
      public enum DailyMode
      {
         Today = 0,
         WeekProgress,
         WeekSchedule,
         Summary,
         Indicators,
         COUNT
      }
      public DailyMode CurDailyMode;

      void Refresh()
      {
         Type? dailyType = CurDailyMode switch
         {
            DailyMode.Today => typeof(TimeBarDay),
            DailyMode.WeekProgress => typeof(TimeBarWeek),
            DailyMode.WeekSchedule => typeof(TimeBarPlan),
            _ => null
         };
         if (dailyType == null)
            TimeBar = null;
         else if (TimeBar == null || TimeBar.GetType() != dailyType)
            TimeBar = (TimeBar)Activator.CreateInstance(dailyType, this);

         if (TimeBar != null)
            TimeBar.Refresh();
         else
            DailyContent.Visibility = Visibility.Collapsed;

         IndicatorBar.Refresh();
         IndicatorGraphPane.Refresh(DailyDay);
         SummaryPane.Refresh(DailyDay);
      }

      #endregion
      #region Animations

      private Storyboard? _StoryboardAppear;
      void _AnimateIn()
      {
         _StoryboardAppear = new Storyboard();
         {
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = new Thickness(-10, 10, -10, 10);
            animation.To = new Thickness(-10, 0, -10, 0);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn };
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         {
            var animation = new ColorAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.0f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.To = Extension.FromArgb(0xFF021204);
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Fill.Color"));
         }
         {
            var animation = new ColorAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.0f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.To = Extension.FromArgb(0xFF021204);
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, BackfillDaily);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Background.Color"));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 16.0f;
            animation.To = 2.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 16.0f;
            animation.To = 2.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            Content.Height = 6.0f;
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(6.0f, TimeSpan.FromSeconds(0)));
            //animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height / 2, TimeSpan.FromSeconds(0.2f), new ExponentialEase { Exponent = 2.0, EasingMode = EasingMode.EaseIn }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(124.0f, TimeSpan.FromSeconds(0.33f), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut }));
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
            Daily.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Daily);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
            Border.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardAppear.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }

         _StoryboardAppear.Completed += (sender, e) => { if (_StoryboardAppear != null) _StoryboardAppear.Remove(this); _StoryboardAppear = null; };
         _StoryboardAppear.Begin(this);
      }

      private Storyboard? _StoryboardCancel;
      public void _AnimateFadeOut()
      {
         _Finished = true;
         OnFinish?.Invoke(this);

         _StoryboardCancel = new Storyboard();
         {
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = new Thickness(-10, 10, -10, 10);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Daily);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 16.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 16.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 6.0f;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut };
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.25f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardCancel.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }

         _StoryboardCancel.Completed += (sender, e) => { _StoryboardCancel.Remove(this); Dispatcher.Invoke(Close); };
         _StoryboardCancel.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      #endregion
      #region Sub-elements

      private void IndicatorPanel_MouseEnter(object sender, MouseEventArgs e)
         => IndicatorBar.IndicatorPanel_MouseEnter(sender, e);
      private void IndicatorPanel_MouseLeave(object sender, MouseEventArgs e)
         => IndicatorBar.IndicatorPanel_MouseLeave(sender, e);
      private void IndicatorPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
         => IndicatorBar.IndicatorPanel_MouseLeftButtonDown(sender, e);
      private void IndicatorPanel_MouseMove(object sender, MouseEventArgs e)
         => IndicatorBar.IndicatorPanel_MouseMove(sender, e);

      private void IndicatorSlider_MouseEnter(object sender, MouseEventArgs e)
         => IndicatorBar.IndicatorSlider_MouseEnter(sender, e);
      private void IndicatorSlider_MouseLeave(object sender, MouseEventArgs e)
         => IndicatorBar.IndicatorSlider_MouseLeave(sender, e);
      private void IndicatorSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
         => IndicatorBar.IndicatorSlider_MouseLeftButtonDown(sender, e);
      private void IndicatorSlider_MouseMove(object sender, MouseEventArgs e)
         => IndicatorBar.IndicatorSlider_MouseMove(sender, e);

      private void TimeAreaHotspot_MouseEnter(object sender, MouseEventArgs e)
         => TimeBar?.TimeAreaHotspot_MouseEnter(sender, e);
      private void TimeAreaHotspot_MouseLeave(object sender, MouseEventArgs e)
         => TimeBar?.TimeAreaHotspot_MouseLeave(sender, e);
      private void TimeAreaHotspot_MouseMove(object sender, MouseEventArgs e)
         => TimeBar?.TimeAreaHotspot_MouseMove(sender, e);
      private void TimeAreaHotspot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
         => TimeBar?.TimeAreaHotspot_MouseLeftButtonDown(sender, e);
      private void TimeAreaHotspot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
         => TimeBar?.TimeAreaHotspot_MouseLeftButtonUp(sender, e);

      private void TextEditor_KeyDown(object sender, KeyEventArgs e)
         => TimekeepTextEditor.TextEditor_KeyDown(sender, e);
      private void TextEditor_TextChanged(object sender, TextChangedEventArgs e)
         => TimekeepTextEditor.TextEditor_TextChanged(sender, e);

      private void SummaryBlockT_MouseEnter(object sender, MouseEventArgs e)
         => SummaryPane?.SummaryBlockT_MouseEnter(sender, e);
      private void SummaryBlockT_MouseLeave(object sender, MouseEventArgs e)
         => SummaryPane?.SummaryBlockT_MouseLeave(sender, e);

      #endregion
   }
}
