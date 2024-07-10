using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Tildetool.Shape;
using Tildetool.Time.Serialization;

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

      Timer? _Timer;
      Storyboard? _StoryboardArc;
      public Timekeep()
      {
         Width = System.Windows.SystemParameters.PrimaryScreenWidth;
         InitializeComponent();
         Top = App.GetBarTop(124.0);

         InitialProject = TimeManager.Instance.CurrentProject;
         DailyDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
         RebuildList();
         Refresh();
         RefreshDaily();

         _AnimateIn();

         _Timer = new Timer { Interval = 1000 };
         _Timer.Elapsed += (o, e) => { Dispatcher.Invoke(() => Refresh()); };
         _Timer.Start();

         if (TimeManager.Instance.CurrentProject != null)
            _AnimateTimer((int)(DateTime.UtcNow - TimeManager.Instance.CurrentStartTime).TotalSeconds % 60);
      }
      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);

         _Timer.Stop();
         _Timer.Dispose();
         _Timer = null;
         if (_CancelTimer != null)
         {
            _CancelTimer.Stop();
            _CancelTimer.Dispose();
            _CancelTimer = null;
         }
      }

      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
         App.Clickthrough(this);
      }

      Timer? _CancelTimer;
      public void ScheduleCancel()
      {
         _CancelTimer = new Timer();
         _CancelTimer = new Timer { Interval = 4000 };
         _CancelTimer.Elapsed += (o, e) => { _CancelTimer.Stop(); Dispatcher.Invoke(() => Cancel()); };
         _CancelTimer.Start();
      }

      bool _Finished = false;
      public void Cancel()
      {
         if (_Finished)
            return;
         _AnimateFadeOut();
         _Finished = true;
         OnFinish?.Invoke(this);
      }

      const string _Number = "0123456789";
      private void Window_KeyDown(object sender, KeyEventArgs e)
      {
         if (_Finished)
            return;

         // Handle escape
         switch (e.Key)
         {
            case Key.Escape:
               e.Handled = true;
               Cancel();
               return;

            case Key.Left:
               DailyDay = DailyDay.AddDays(CurDailyMode == DailyMode.Today ? -1 : -7);
               RefreshDaily();
               e.Handled = true;
               return;

            case Key.Right:
               DailyDay = DailyDay.AddDays(CurDailyMode == DailyMode.Today ? 1 : 7);
               RefreshDaily();
               e.Handled = true;
               return;

            case Key.Up:
               DailyDay = DailyDay.AddDays(-7);
               RefreshDaily();
               e.Handled = true;
               return;

            case Key.Down:
               DailyDay = DailyDay.AddDays(7);
               RefreshDaily();
               e.Handled = true;
               return;

            case Key.Tab:
               CurDailyMode = (DailyMode)((int)(CurDailyMode + 1) % (int)DailyMode.COUNT);
               RefreshDaily();
               e.Handled = true;
               return;
         }

         // Handle key entry.
         if (e.Key >= Key.A && e.Key <= Key.Z)
            SetActiveTime(e.Key.ToString());
         else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            SetActiveTime(_Number[e.Key - Key.D0].ToString());
         else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            SetActiveTime(_Number[e.Key - Key.NumPad0].ToString());
         else if (e.Key == Key.Return)
         {
            try
            {
               Process process = new Process();
               ProcessStartInfo startInfo = new ProcessStartInfo();
               startInfo.FileName = System.IO.Directory.GetCurrentDirectory() + "\\TimekeepCache.json";
               startInfo.UseShellExecute = true;
               process.StartInfo = startInfo;
               process.Start();
            }
            catch(Exception ex)
            {
               MessageBox.Show(ex.ToString());
               App.WriteLog(ex.Message);
            }
            Cancel();
            e.Handled = true;
            return;
         }
      }

      private Storyboard? _StoryboardRefresh2;
      void SetActiveTime(string key)
      {
         // Make sure we actually changed to a valid project.
         Project? project = null;
         if (!TimeManager.Instance.HotkeyToProject.TryGetValue(key, out project))
            return;

         if (CurDailyMode != DailyMode.Today)
         {
            if (DailyFocus == project)
               DailyFocus = null;
            else
               DailyFocus = project;
            RefreshDaily();
            return;
         }

         //
         if (project == TimeManager.Instance.CurrentProject)
            return;

         // Switch
         Project oldProject = TimeManager.Instance.CurrentProject;
         if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            TimeManager.Instance.AlterProject(project);
         else
            TimeManager.Instance.SetProject(project);

         // Update the display.
         Refresh();
         if (project != null)
            _AnimateCommand(GuiToProject.IndexOf(project));

         // Turn off a scheduled cancel since we'll finish on our own soon.
         if (_CancelTimer != null)
         {
            _CancelTimer.Stop();
            _CancelTimer.Dispose();
            _CancelTimer = null;
         }

         //
         if (_StoryboardRefresh != null)
         {
            _StoryboardRefresh.Stop(this);
            _StoryboardRefresh.Remove(this);
            _StoryboardRefresh = null;
         }
         _StoryboardRefresh = new Storyboard();

         // Update the coloring of the text.
         for (int i = 0; i < GuiToProject.Count; i++)
         {
            Grid area = ProjectGui[i].FindElementByName<Grid>("Area");
            TextBlock hotkey = ProjectGui[i].FindElementByName<TextBlock>("Hotkey");
            TextBlock text = ProjectGui[i].FindElementByName<TextBlock>("Text");
            bool isCurrent = GuiToProject[i] == TimeManager.Instance.CurrentProject;
            {
               var animation = new DoubleAnimation();
               animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
               animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
               animation.To = isCurrent ? 54 : 40;
               _StoryboardRefresh.Children.Add(animation);
               Storyboard.SetTarget(animation, area);
               Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
            }
            {
               var animation = new ColorAnimation();
               animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
               hotkey.Foreground = new SolidColorBrush((hotkey.Foreground as SolidColorBrush).Color);
               animation.To = isCurrent ? (Resources["ColorTextFore"] as SolidColorBrush).Color : (Resources["ColorTextBack"] as SolidColorBrush).Color;
               _StoryboardRefresh.Children.Add(animation);
               Storyboard.SetTarget(animation, hotkey);
               Storyboard.SetTargetProperty(animation, new PropertyPath("Foreground.Color"));
            }
            {
               var animation = new ColorAnimation();
               animation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
               text.Foreground = new SolidColorBrush((text.Foreground as SolidColorBrush).Color);
               animation.To = isCurrent ? (Resources["ColorTextFore"] as SolidColorBrush).Color : (Resources["ColorTextBack"] as SolidColorBrush).Color;
               _StoryboardRefresh.Children.Add(animation);
               Storyboard.SetTarget(animation, text);
               Storyboard.SetTargetProperty(animation, new PropertyPath("Foreground.Color"));
            }
            {
               var animation = new DoubleAnimation();
               animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
               animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
               animation.To = isCurrent ? 20 : 12;
               _StoryboardRefresh.Children.Add(animation);
               Storyboard.SetTarget(animation, text);
               Storyboard.SetTargetProperty(animation, new PropertyPath(TextBlock.FontSizeProperty));
            }
         }
         CurrentTimeH.Foreground = Resources["ColorTextBack"] as SolidColorBrush;
         CurrentTimeM.Foreground = Resources["ColorTextBack"] as SolidColorBrush;

         _StoryboardRefresh.Completed += (sender, e) => { if (_StoryboardRefresh != null) _StoryboardRefresh.Remove(this); _StoryboardRefresh = null; };
         _StoryboardRefresh.Begin(this, HandoffBehavior.SnapshotAndReplace);

         //
         if (_StoryboardRefresh2 != null)
         {
            _StoryboardRefresh2.Stop(this);
            _StoryboardRefresh2.Remove(this);
            _StoryboardRefresh2 = null;
         }
         if (CurDailyMode == DailyMode.Today)
         {
            _StoryboardRefresh2 = new Storyboard();

            for (int i = 0; i < TimeManager.Instance.Data.Length; i++)
            {
               ContentControl content = DailyRows.Children[i] as ContentControl;
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;
               TextBlock headerName = grid.FindElementByName<TextBlock>("HeaderName");
               TextBlock headerDate = grid.FindElementByName<TextBlock>("HeaderDate");

               bool isCurrent = TimeManager.Instance.Data[i] == TimeManager.Instance.CurrentProject;
               {
                  var animation = new ColorAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(isCurrent ? 0.2f : 0.33f));
                  animation.To = Extension.FromArgb((uint)(isCurrent ? 0x404A6030 : ((i % 2) == 0 ? 0x00042508 : 0x38042508)));
                  _StoryboardRefresh2.Children.Add(animation);
                  Storyboard.SetTarget(animation, grid);
                  Storyboard.SetTargetProperty(animation, new PropertyPath("Background.Color"));
               }
               {
                  var animation = new ColorAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(isCurrent ? 0.2f : 0.33f));
                  animation.To = Extension.FromArgb(isCurrent ? 0xFFC3F1AF : 0xFF449637);
                  _StoryboardRefresh2.Children.Add(animation);
                  Storyboard.SetTarget(animation, headerName);
                  Storyboard.SetTargetProperty(animation, new PropertyPath("Foreground.Color"));
               }
               {
                  var animation = new ColorAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
                  animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
                  animation.To = Extension.FromArgb(isCurrent ? 0xFFC3F1AF : 0xFF449637);
                  _StoryboardRefresh2.Children.Add(animation);
                  Storyboard.SetTarget(animation, headerDate);
                  Storyboard.SetTargetProperty(animation, new PropertyPath("Foreground.Color"));
               }
            }

            _StoryboardRefresh2.Completed += (sender, e) => { if (_StoryboardRefresh2 != null) _StoryboardRefresh2.Remove(this); _StoryboardRefresh2 = null; };
            _StoryboardRefresh2.Begin(this, HandoffBehavior.SnapshotAndReplace);
         }

         // Stop the timer.
         if (_StoryboardArc != null)
         {
            _StoryboardArc.Stop(this);
            _StoryboardArc.Remove(this);
            _StoryboardArc = null;
         }
      }
      public string IntervalTime(int totalTimeMin)
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
      void Refresh()
      {
         // Refresh current
         if (TimeManager.Instance.CurrentProject != null)
         {
            int mins = (int)(DateTime.UtcNow - TimeManager.Instance.CurrentStartTime).TotalMinutes;
            int hours = mins / 60;
            CurrentTimeH.Text = hours.ToString();
            CurrentTimeM.Text = String.Format("{0:00}", mins % 60);
         }
      }

      Project? InitialProject;
      List<Project> GuiToProject;
      List<Panel> ProjectGui;
      private Storyboard? _StoryboardRefresh;
      void RebuildList()
      {
         // Sort with the current first, then in time spent today descending.
         GuiToProject = TimeManager.Instance.Data.Where(p => p != InitialProject).ToList();
         GuiToProject.Sort((a, b) => -a.TimeTodaySec.CompareTo(b.TimeTodaySec));
         if (InitialProject != null)
            GuiToProject.Insert(0, InitialProject);

         // Add or remove to get the right quantity.
         DataTemplate? template = Resources["CommandOption"] as DataTemplate;
         void _populate(Panel grid, int count)
         {
            while (grid.Children.Count > count)
               grid.Children.RemoveAt(grid.Children.Count - 1);
            while (grid.Children.Count < count)
            {
               ContentControl content = new ContentControl { ContentTemplate = template };
               grid.Children.Add(content);
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
            }
         }

         //
         int preCount = (GuiToProject.Count - 1) / 2;
         int postCount = (GuiToProject.Count - 1) - preCount;
         _populate(GridPre, preCount);
         _populate(GridPost, postCount);

         // Add the data
         ProjectGui = new List<Panel>(GuiToProject.Count);
         int increment = 0;
         for (int i = 0; i < GuiToProject.Count; i++)
         {
            bool isCurrent = GuiToProject[i] == TimeManager.Instance.CurrentProject;

            // Pick the right control.
            ContentControl content;
            if (isCurrent)
            {
               content = CurrentOption;
               increment++;
            }
            else if (((i - increment) % 2) == 0)
               content = GridPost.Children[(i - increment) / 2] as ContentControl;
            else
               content = GridPre.Children[preCount - 1 - ((i - increment) / 2)] as ContentControl;

            // Find it
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;
            ProjectGui.Add(grid);

            Grid area = grid.FindElementByName<Grid>("Area");
            TextBlock hotkey = grid.FindElementByName<TextBlock>("Hotkey");
            TextBlock text = grid.FindElementByName<TextBlock>("Text");

            // Update the text
            hotkey.Text = GuiToProject[i].Hotkey;
            text.Text = GuiToProject[i].Name;

            // Some special handling for the current one
            area.Height = isCurrent ? 54 : 40;
            hotkey.Foreground = isCurrent ? (Resources["ColorTextFore"] as SolidColorBrush) : (Resources["ColorTextBack"] as SolidColorBrush);
            text.Foreground = isCurrent ? (Resources["ColorTextFore"] as SolidColorBrush) : (Resources["ColorTextBack"] as SolidColorBrush);
            text.FontSize = isCurrent ? 20 : 12;
         }
      }

      enum DailyMode
      {
         Today = 0,
         WeekProgress,
         WeekSchedule,
         COUNT
      }
      class TimeBlock
      {
         public string Name;
         public DateTime StartTime;
         public DateTime EndTime;
         public Color Color;
         public int Priority;

         public Project? Project = null;
         public long DbId = -1;

         public static TimeBlock FromTimePeriod(TimePeriod period)
         {
            Project? project;
            TimeManager.Instance.IdentToProject.TryGetValue(period.Ident, out project);
            return new TimeBlock { Priority = 0, Name = project?.Name ?? period.Ident, StartTime = period.StartTime, EndTime = period.EndTime, Color = Extension.FromArgb(0xFF143518),
                                   Project = project, DbId = period.DbId };
         }
         public static TimeBlock FromWeeklySchedule(WeeklySchedule schedule, DateTime today)
         {
            return new TimeBlock { Priority = 1, Name = schedule.Name, StartTime = today.AddHours(schedule.HourBegin), EndTime = today.AddHours(schedule.HourEnd), Color = Extension.FromArgb(0xD0A8611F) };
         }
         public static TimeBlock FromTimeEvent(TimeEvent evt)
         {
            return new TimeBlock { Priority = 2, Name = evt.Name, StartTime = evt.StartTime, EndTime = evt.EndTime, Color = Extension.FromArgb(0xD0E0411F) };
         }

         public bool CanMerge(TimeBlock next)
         {
            if (Project != next.Project)
               return false;
            if (Project == null && Name.CompareTo(next.Name) != 0)
               return false;

            if (EndTime < next.StartTime)
               return false;
            return (EndTime - next.StartTime).TotalMinutes <= 1.0;
         }
         public TimeBlock Merge(TimeBlock next)
         {
            return new TimeBlock { Name = Name, StartTime = StartTime, EndTime = next.EndTime, Color = Color, Priority = Priority, Project = Project, DbId = next.DbId };
         }
      }
      DailyMode CurDailyMode;
      DateTime DailyDay;
      Project DailyFocus;
      void RefreshDaily()
      {
         if (_StoryboardRefresh2 != null)
         {
            _StoryboardRefresh2.Stop(this);
            _StoryboardRefresh2.Remove(this);
            _StoryboardRefresh2 = null;
         }

         if (DailyDay > DateTime.Now.AddDays(7))
            DailyDay = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0).AddDays(7);

         DateTime dayBegin = new DateTime(DailyDay.Year, DailyDay.Month, DailyDay.Day, 0, 0, 0);
         DateTime weekBegin = dayBegin.AddDays(-(int)DailyDay.DayOfWeek);
         if (CurDailyMode == DailyMode.Today)
            DailyDate.Text = DailyDay.ToString("yy/MM/dd ddd");
         else if (CurDailyMode == DailyMode.WeekProgress)
            DailyDate.Text = "Progress";
         else
            DailyDate.Text = "Schedule";

         DataTemplate? templateRow = Resources["DailyRow"] as DataTemplate;
         DataTemplate? templateHeaderCell = Resources["DailyHeaderCell"] as DataTemplate;
         DataTemplate? templateCell = Resources["DailyCell"] as DataTemplate;
         DataTemplate? templateDivider = Resources["DailyDivider"] as DataTemplate;
         DataTemplate? templateSchedule = Resources["ScheduleEntry"] as DataTemplate;

         // Add or remove to get the right quantity.
         void _populate(Panel parent, DataTemplate template, int count)
         {
            while (parent.Children.Count > count)
               parent.Children.RemoveAt(parent.Children.Count - 1);
            while (parent.Children.Count < count)
            {
               ContentControl content = new ContentControl { ContentTemplate = template };
               parent.Children.Add(content);
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
            }
         }

         int minHour = 8;
         int maxHour = 21;

         void _organizePeriod(List<TimeBlock> periods)
         {
            // Sort by time.
            periods.Sort((a, b) => a.StartTime != b.StartTime ? a.StartTime.CompareTo(b.StartTime) : a.EndTime.CompareTo(b.EndTime));

            // Handle time periods
            TimeBlock prevPeriod = null;
            foreach (TimeBlock period in periods)
            {
               // Increase or decrease our total time range.
               if (period.StartTime.ToLocalTime().TimeOfDay.TotalHours < minHour)
                  minHour = period.StartTime.ToLocalTime().Hour;
               if (period.EndTime.ToLocalTime().TimeOfDay.TotalHours > maxHour)
                  maxHour = period.EndTime.ToLocalTime().Hour + 1;

               // check if there's an overlap and shrink the lower priority one
               if (prevPeriod != null && prevPeriod.EndTime > period.StartTime)
               {
                  if (period.Priority > prevPeriod.Priority)
                     prevPeriod.EndTime = period.StartTime;
                  else
                     period.StartTime = prevPeriod.EndTime;
               }
               prevPeriod = period;
            }

            // Filter to periods with a new that have a positive period.
            for (int i = periods.Count - 1; i >= 0; i--)
               if (string.IsNullOrEmpty(periods[i].Name) || periods[i].EndTime <= periods[i].StartTime)
                  periods.Remove(periods[i]);
         }

         List<List<TimeBlock>> weeklySchedule = new List<List<TimeBlock>>();
         if (CurDailyMode == DailyMode.Today || CurDailyMode == DailyMode.WeekSchedule)
            for (int i = 0; i < 7; i++)
            {
               if (CurDailyMode == DailyMode.Today && (DayOfWeek)i != DailyDay.DayOfWeek)
               {
                  weeklySchedule.Add(null);
                  continue;
               }
               DateTime todayS = weekBegin.AddDays(i).ToUniversalTime();
               DateTime todayE = weekBegin.AddDays(i + 1).ToUniversalTime();

               List<TimeBlock> block = new List<TimeBlock>();
               block.AddRange(TimeManager.Instance.ScheduleByDayOfWeek[i].Select(s => TimeBlock.FromWeeklySchedule(s, todayS)));
               block.AddRange(TimeManager.Instance.QueryTimeEvent(todayS.ToLocalTime(), todayE.ToLocalTime()).Select(s => TimeBlock.FromTimeEvent(s)));
               _organizePeriod(block);
               weeklySchedule.Add(block);
            }

         List<List<TimeBlock>> projectPeriods = new List<List<TimeBlock>>();
         {
            if (CurDailyMode == DailyMode.Today)
            {
               DateTime todayS = new DateTime(DailyDay.Year, DailyDay.Month, DailyDay.Day, 0, 0, 0).ToUniversalTime();
               DateTime todayE = todayS.AddDays(1);
               for (int i = 0; i < TimeManager.Instance.Data.Length; i++)
                  projectPeriods.Add(TimeManager.Instance.QueryTimePeriod(TimeManager.Instance.Data[i], todayS, todayE).Select(p => TimeBlock.FromTimePeriod(p)).ToList());
            }
            else
               for (int i = 0; i < 7; i++)
               {
                  DateTime todayS = weekBegin.AddDays(i).ToUniversalTime();
                  DateTime todayE = weekBegin.AddDays(i + 1).ToUniversalTime();
                  if (CurDailyMode == DailyMode.WeekProgress)
                     projectPeriods.Add(TimeManager.Instance.QueryTimePeriod(todayS, todayE).Select(p => TimeBlock.FromTimePeriod(p)).ToList());
                  else
                     projectPeriods.Add(weeklySchedule[i]);
               }

            foreach (List<TimeBlock> periods in projectPeriods)
               _organizePeriod(periods);
         }
         {
            foreach (List<TimeBlock> periods in projectPeriods)
               for (int i = periods.Count - 1; i >= 0; i--)
               {
                  if (periods[i].StartTime.ToLocalTime().TimeOfDay.TotalHours > maxHour)
                     periods.RemoveAt(i);
                  else if (periods[i].EndTime.ToLocalTime().TimeOfDay.TotalHours < minHour)
                     periods.RemoveAt(i);
               }
         }

         // Figure out the time ranges
         int[] showHours = new int[] { minHour, 12, 15, 18, maxHour };
         HeaderRow.ColumnDefinitions.Clear();
         for (int i = 0; i < showHours.Length - 1; i++)
            HeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(showHours[i + 1] - showHours[i], GridUnitType.Star) });

         // Fill in time headers
         _populate(HeaderRow, templateHeaderCell, showHours.Length);
         for (int i = 0; i < showHours.Length; i ++)
         {
            ContentControl content = HeaderRow.Children[i] as ContentControl;
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            TextBlock text = VisualTreeHelper.GetChild(presenter, 0) as TextBlock;

            Grid.SetColumn(content, Math.Min(i, showHours.Length - 2));
            text.Text = $"{showHours[i]:D2}00";
            text.HorizontalAlignment = (i + 1 == showHours.Length) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            text.Margin = (i + 1 == showHours.Length) ? new Thickness(0,0,-50,0) : new Thickness(-50, 0, 0, 0);
         }

         // Hour dividers
         int hourCount = maxHour - minHour;
         DailyDividers.ColumnDefinitions.Clear();
         for (int i = 0; i < hourCount * 2; i++)
            DailyDividers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

         _populate(DailyDividers, templateDivider, (hourCount * 2) + 1);
         for (int i = 0; i < (hourCount * 2) + 1; i++)
         {
            ContentControl content = DailyDividers.Children[i] as ContentControl;
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;

            Grid.SetColumn(content, Math.Min(i, (hourCount * 2) - 1));
            grid.Background = new SolidColorBrush(Extension.FromArgb((uint)((i % 2) == 0 ? 0x40449637 : 0x60042508)));
            grid.HorizontalAlignment = (i == hourCount * 2) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
         }

         // Show the current time.
         bool showNowLine = (CurDailyMode == DailyMode.Today) && dayBegin <= DateTime.Now && DateTime.Now < dayBegin.AddDays(1);
         NowDividerGrid.Visibility = showNowLine ? Visibility.Visible : Visibility.Collapsed;
         if (showNowLine)
         {
            double hourProgress = Math.Min(1.0, (DateTime.Now - dayBegin.AddHours(minHour)).TotalHours / (double)(maxHour - minHour));
            NowDividerGrid.ColumnDefinitions.Clear();
            NowDividerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourProgress, GridUnitType.Star) });
            NowDividerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - hourProgress, GridUnitType.Star) });
         }

         // Scheduled events
         ScheduleGrid.Visibility = (CurDailyMode == DailyMode.Today) ? Visibility.Visible : Visibility.Collapsed;
         if (CurDailyMode == DailyMode.Today)
         {
            DateTime dayBeginUtc = dayBegin.ToUniversalTime();
            List<TimeBlock> block = weeklySchedule[(int)DailyDay.DayOfWeek];

            ScheduleGrid.ColumnDefinitions.Clear();
            double lastHour = minHour;
            for (int i = 0; i < block.Count; i++)
            {
               double hourBegin = (block[i].StartTime - dayBeginUtc).TotalHours;
               double hourEnd = (block[i].EndTime - dayBeginUtc).TotalHours;
               ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(hourBegin - lastHour, 0), GridUnitType.Star) });
               ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourEnd - hourBegin, GridUnitType.Star) });
               lastHour = hourEnd;
            }
            ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(maxHour - lastHour, GridUnitType.Star) });

            _populate(ScheduleGrid, templateSchedule, block.Count);
            for (int i = 0; i < block.Count; i++)
            {
               ContentControl content = ScheduleGrid.Children[i] as ContentControl;
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();

               Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;
               TextBlock scheduleTextCtrl = grid.FindElementByName<TextBlock>("ScheduleText");

               Grid.SetColumn(content, (i * 2) + 1);
               grid.Background = new SolidColorBrush(block[i].Color.Alpha(0x20));
               scheduleTextCtrl.Foreground = new SolidColorBrush(block[i].Color.Lerp(Extension.FromArgb(0xFFC3F1AF), 0.75f));
               scheduleTextCtrl.Text = block[i].Name;
            }
         }

         // Fill in rows
         double sumMinutes = 0;
         _populate(DailyRows, templateRow, projectPeriods.Count);
         for (int i = 0; i < projectPeriods.Count; i++)
         {
            DateTime thisDateBeginLocal = (CurDailyMode != DailyMode.Today ? weekBegin.AddDays(i) : dayBegin).AddHours(minHour);
            DateTime thisDateBegin = (CurDailyMode != DailyMode.Today ? weekBegin.AddDays(i) : dayBegin).ToUniversalTime().AddHours(minHour);
            DateTime thisDateEnd = thisDateBegin.AddHours(maxHour - minHour);

            bool today = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) == 0;
            bool postToday = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) > 0;
            bool preToday = !today && !postToday;

            List<TimeBlock> periods = projectPeriods[i];
            double totalMinutes;
            {
               IEnumerable<TimeBlock> periodsForThis;
               if (CurDailyMode == DailyMode.WeekProgress && DailyFocus != null)
                  periodsForThis = periods.Where(p => p.Project == DailyFocus);
               else
                  periodsForThis = periods.AsEnumerable();
               totalMinutes = periodsForThis.Sum(p => (p.EndTime - p.StartTime).TotalMinutes);
            }
            sumMinutes += totalMinutes;

            Grid cellParent;
            {
               // Pick the right control.
               ContentControl content = DailyRows.Children[i] as ContentControl;
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
               Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;

               TextBlock headerName = grid.FindElementByName<TextBlock>("HeaderName");
               TextBlock headerDate = grid.FindElementByName<TextBlock>("HeaderDate");
               TextBlock headerTimeH = grid.FindElementByName<TextBlock>("HeaderTimeH");
               TextBlock headerTimeM = grid.FindElementByName<TextBlock>("HeaderTimeM");
               cellParent = grid.FindElementByName<Grid>("DailyCells");

               // Show the current time.
               bool showNowLineRow = (CurDailyMode != DailyMode.Today) && thisDateBeginLocal <= DateTime.Now;
               Grid rowNowGrid = grid.FindElementByName<Grid>("RowNowGrid");
               rowNowGrid.Visibility = showNowLineRow ? Visibility.Visible : Visibility.Collapsed;
               if (showNowLineRow)
               {
                  double hourProgress = Math.Min(1.0, (DateTime.Now - thisDateBeginLocal).TotalHours / (double)(maxHour - minHour));
                  rowNowGrid.ColumnDefinitions.Clear();
                  rowNowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourProgress, GridUnitType.Star) });
                  rowNowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - hourProgress, GridUnitType.Star) });
               }

               // Update the text
               if ((CurDailyMode == DailyMode.Today && TimeManager.Instance.Data[i] == InitialProject) || (CurDailyMode != DailyMode.Today && today))
               {
                  grid.Background = new SolidColorBrush(Extension.FromArgb((uint)0x404A6030));
                  headerName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFFC3F1AF));
                  headerDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFFC3F1AF));
               }
               else if ((CurDailyMode == DailyMode.WeekSchedule && preToday) || (CurDailyMode == DailyMode.WeekProgress && postToday))
               {
                  grid.Background = new SolidColorBrush(Extension.FromArgb((uint)((i % 2) == 0 ? 0x80202020 : 0x80282828)));
                  headerName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF606060));
                  headerDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF606060));
               }
               else
               {
                  grid.Background = new SolidColorBrush(Extension.FromArgb((uint)((i % 2) == 0 ? 0x00042508 : 0x38042508)));
                  headerName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF449637));
                  headerDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF449637));
               }

               headerName.Text = CurDailyMode != DailyMode.Today ? thisDateBegin.ToString("ddd") : TimeManager.Instance.Data[i].Name;
               headerDate.Visibility = CurDailyMode != DailyMode.Today ? Visibility.Visible : Visibility.Collapsed;
               headerDate.Text = thisDateBegin.ToString("yy/MM/dd");

               if ((CurDailyMode == DailyMode.WeekSchedule && preToday) || (CurDailyMode == DailyMode.WeekProgress && postToday))
               {
                  headerTimeH.Foreground = new SolidColorBrush(Extension.FromRgb((uint)0x606060));
                  headerTimeM.Foreground = new SolidColorBrush(Extension.FromRgb((uint)0x606060));
               }
               else
               {
                  headerTimeH.Foreground = new SolidColorBrush(Extension.FromRgb((uint)((totalMinutes > 0) ? 0xC3F1AF : 0x449637)));
                  headerTimeM.Foreground = new SolidColorBrush(Extension.FromRgb((uint)((totalMinutes > 0) ? 0xC3F1AF : 0x449637)));
               }
               headerTimeH.Text = ((int)totalMinutes / 60).ToString();
               headerTimeM.Text = $"{((int)totalMinutes % 60):D2}";
            }

            // Merge time blocks that are adjacent.
            List<TimeBlock> periodsFilter = new List<TimeBlock>(periods.Count);
            TimeBlock pendingBlock = null;
            for (int r = 0; r < periods.Count; r++)
            {
               if (pendingBlock != null && pendingBlock.CanMerge(periods[r]))
                  pendingBlock = pendingBlock.Merge(periods[r]);
               else
               {
                  if (pendingBlock != null)
                     periodsFilter.Add(pendingBlock);
                  pendingBlock = periods[r];
               }
            }
            if (pendingBlock != null)
               periodsFilter.Add(pendingBlock);

            _populate(cellParent, templateCell, periodsFilter.Count);

            DateTime lastDate = thisDateBegin;
            cellParent.ColumnDefinitions.Clear();
            for (int o=0; o < periodsFilter.Count; o++)
            {
               // Pick the right control.
               ContentControl content = cellParent.Children[o] as ContentControl;
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
               Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;

               TextBlock startTime = grid.FindElementByName<TextBlock>("StartTime");
               TextBlock endTime = grid.FindElementByName<TextBlock>("EndTime");
               TextBlock cellTimeH = grid.FindElementByName<TextBlock>("CellTimeH");
               TextBlock cellTimeM = grid.FindElementByName<TextBlock>("CellTimeM");
               Grid activeGlow = grid.FindElementByName<Grid>("ActiveGlow");

               if (CurDailyMode == DailyMode.WeekSchedule || CurDailyMode == DailyMode.WeekProgress)
               {
                  activeGlow.Visibility = Visibility.Collapsed;
                  cellTimeH.Visibility = Visibility.Collapsed;
                  startTime.Visibility = Visibility.Collapsed;
                  endTime.Visibility = Visibility.Collapsed;
                  cellTimeM.Visibility = Visibility.Visible;
                  bool thisProject = CurDailyMode == DailyMode.WeekProgress && periodsFilter[o].Project == DailyFocus;
                  if (CurDailyMode == DailyMode.WeekProgress && DailyFocus != null && !thisProject)
                  {
                     grid.Background = new SolidColorBrush(Extension.FromArgb(0xFF383838));
                     cellTimeM.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF909090));
                  }
                  else if (CurDailyMode == DailyMode.WeekSchedule && preToday)
                  {
                     grid.Background = new SolidColorBrush(periodsFilter[o].Color.Lerp(Extension.FromArgb(0xFF202020), 0.8f));
                     cellTimeM.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF808080));
                  }
                  else
                  {
                     grid.Background = new SolidColorBrush(periodsFilter[o].Color);
                     cellTimeM.Foreground = new SolidColorBrush(periodsFilter[o].Color.Lerp(Extension.FromArgb(0xFFC3F1AF), thisProject ? 1.0f : 0.75f));
                  }
                  cellTimeM.Text = periodsFilter[o].Name;
               }
               else
               {
                  grid.Background = new SolidColorBrush(periodsFilter[o].Color);

                  bool isActiveCell = TimeManager.Instance.CurrentTimePeriod == periodsFilter[o].DbId;
                  activeGlow.Visibility = isActiveCell ? Visibility.Visible : Visibility.Collapsed;

                  double periodMinutes = (periodsFilter[o].EndTime - periodsFilter[o].StartTime).TotalMinutes;
                  cellTimeH.Visibility = (periodMinutes >= 10.0) ? Visibility.Visible : Visibility.Hidden;
                  cellTimeM.Visibility = (periodMinutes >= 10.0) ? Visibility.Visible : Visibility.Hidden;
                  cellTimeM.Foreground = new SolidColorBrush(Extension.FromArgb(0xFFC3F1AF));
                  cellTimeH.Text = ((int)periodMinutes / 60).ToString();
                  cellTimeM.Text = $"{((int)periodMinutes % 60):D2}";

                  startTime.Visibility = (periodMinutes >= 45) ? Visibility.Visible : Visibility.Collapsed;
                  endTime.Visibility = (periodMinutes >= 45) ? Visibility.Visible : Visibility.Collapsed;
                  if (periodMinutes >= 45)
                  {
                     startTime.Text = $"{periodsFilter[o].StartTime.ToLocalTime().Hour:D2}{periodsFilter[o].StartTime.ToLocalTime().Minute:D2}";
                     endTime.Text = $"{periodsFilter[o].EndTime.ToLocalTime().Hour:D2}{periodsFilter[o].EndTime.ToLocalTime().Minute:D2}";
                  }
               }

               // Show the current time.
               DateTime periodStartTime = periodsFilter[o].StartTime > lastDate ? periodsFilter[o].StartTime : lastDate;
               DateTime periodEndTime = periodsFilter[o].EndTime < thisDateEnd ? periodsFilter[o].EndTime : thisDateEnd;
               if (periodEndTime > periodStartTime)
               {
                  cellParent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((periodStartTime - lastDate).TotalHours, GridUnitType.Star) });
                  cellParent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((periodEndTime - periodStartTime).TotalHours, GridUnitType.Star) });
                  lastDate = periodEndTime;
                  Grid.SetColumn(content, (o * 2) + 1);
               }
            }
            cellParent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((thisDateEnd - lastDate).TotalHours, GridUnitType.Star) });
         }
         SumTimeH.Text = ((int)sumMinutes / 60).ToString();
         SumTimeM.Text = $"{((int)sumMinutes % 60):D2}";
      }

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

      private Storyboard? _StoryboardCommand;
      void _AnimateCommand(int guiIndex)
      {
         if (_Finished)
            return;
         _Finished = true;

         Panel grid = ProjectGui[guiIndex];
         Grid area = grid.FindElementByName<Grid>("Area");

         _StoryboardCommand = new Storyboard();
         {
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.8f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), TimeSpan.FromSeconds(0.125f), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFF042508), TimeSpan.FromSeconds(0.25f), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            _StoryboardCommand.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, area);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }

         _StoryboardCommand.Completed += (sender, e) =>
         {
            _StoryboardCommand.Remove(this);
            _AnimateFadeOut();
            OnFinish?.Invoke(this);
         };
         _StoryboardCommand.Begin(this, HandoffBehavior.SnapshotAndReplace);

         App.PlayBeep(App.BeepSound.Accept);
      }

      private Storyboard? _StoryboardCancel;
      void _AnimateFadeOut()
      {
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

      void _AnimateTimer(float timeIn)
      {
      }
   }
}
