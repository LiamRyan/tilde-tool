using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tildetool.Time.Serialization;
using Tildetool.WPF;

namespace Tildetool.Time
{
   public class TimeBar
   {
      public Timekeep Parent;
      public TimeBar(Timekeep parent)
      {
         Parent = parent;
      }

      public int MinHour = 6;
      public int MaxHour = 23;

      public class TimeBlock
      {
         public enum Style
         {
            TimePeriod,
            WeeklySchedule,
            TimeEvent
         }
         public string Name;
         public DateTime StartTime; //utc
         public DateTime EndTime; //utc
         public Color Color;
         public Style CurStyle;
         public int Priority;

         public Project? Project = null;
         public long DbId = -1;

         public static TimeBlock FromTimePeriod(TimePeriod period)
         {
            Project? project;
            TimeManager.Instance.IdentToProject.TryGetValue(period.Ident, out project);
            return new TimeBlock
            {
               Priority = 0,
               CurStyle = Style.TimePeriod,
               Name = project?.Name ?? period.Ident,
               StartTime = period.StartTime,
               EndTime = period.EndTime,
               Color = project != null ? Extension.FromArgb(0xFF143518) : Extension.FromArgb(0xFF0D211D),
               Project = project,
               DbId = period.DbId
            };
         }
         public static TimeBlock FromWeeklySchedule(WeeklySchedule schedule, DateTime today)
         {
            return new TimeBlock { Priority = 1, CurStyle = Style.WeeklySchedule, Name = schedule.Name, StartTime = today.AddHours(schedule.HourBegin), EndTime = today.AddHours(schedule.HourEnd), Color = Extension.FromArgb(0xD0A8611F) };
         }
         public static TimeBlock FromTimeEvent(TimeEvent evt)
         {
            return new TimeBlock { Priority = 2, CurStyle = Style.TimeEvent, Name = evt.Name, StartTime = evt.StartTime, EndTime = evt.EndTime, Color = Extension.FromArgb(0xD0E0411F) };
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

      class IndicatorCtrl : DataTemplater
      {
         public TextBlock Text;
         public TextBlock Icon;
         public IndicatorCtrl(FrameworkElement root) : base(root) { }
      }

      public void Refresh(DateTime day)
      {
         Parent.DailyContent.Visibility = (Parent.CurDailyMode != Timekeep.DailyMode.Indicators) ? Visibility.Visible : Visibility.Collapsed;
         if (Parent.CurDailyMode == Timekeep.DailyMode.Indicators)
            return;

         if (day > DateTime.Now.AddDays(7))
            day = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0).AddDays(7);

         DateTime dayBegin = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0);
         DateTime weekBegin = dayBegin.AddDays(-(int)day.DayOfWeek);
         if (Parent.CurDailyMode == Timekeep.DailyMode.Today)
            Parent.DailyDate.Text = day.ToString("yy/MM/dd ddd");
         else if (Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress)
            Parent.DailyDate.Text = "Progress";
         else
            Parent.DailyDate.Text = "Schedule";

         DataTemplate? templateRow = Parent.Resources["DailyRow"] as DataTemplate;
         DataTemplate? templateIndicator = Parent.Resources["Indicator"] as DataTemplate;
         DataTemplate? templateHeaderCell = Parent.Resources["DailyHeaderCell"] as DataTemplate;
         DataTemplate? templateCell = Parent.Resources["DailyCell"] as DataTemplate;
         DataTemplate? templateDivider = Parent.Resources["DailyDivider"] as DataTemplate;
         DataTemplate? templateSchedule = Parent.Resources["ScheduleEntry"] as DataTemplate;

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

         MinHour = 6;
         MaxHour = 23;

         void _organizePeriod(List<TimeBlock> periods)
         {
            // Sort by time.
            periods.Sort((a, b) => a.StartTime != b.StartTime ? a.StartTime.CompareTo(b.StartTime) : a.EndTime.CompareTo(b.EndTime));

            // Handle time periods
            TimeBlock prevPeriod = null;
            foreach (TimeBlock period in periods)
            {
               // Increase or decrease our total time range.
               if (period.StartTime.ToLocalTime().TimeOfDay.TotalHours < MinHour)
                  MinHour = period.StartTime.ToLocalTime().Hour;
               if (period.EndTime.ToLocalTime().TimeOfDay.TotalHours > MaxHour)
                  MaxHour = period.EndTime.ToLocalTime().Hour + 1;

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
         if (Parent.CurDailyMode == Timekeep.DailyMode.Today || Parent.CurDailyMode == Timekeep.DailyMode.WeekSchedule)
         {
            DateTime weekEnd = weekBegin.AddDays(7);
            TimeBlock[] weekEvents = TimeManager.Instance.QueryTimeEvent(weekBegin, weekEnd).Select(s => TimeBlock.FromTimeEvent(s)).ToArray();

            for (int i = 0; i < 7; i++)
            {
               if (Parent.CurDailyMode == Timekeep.DailyMode.Today && (DayOfWeek)i != day.DayOfWeek)
               {
                  weeklySchedule.Add(null);
                  continue;
               }
               DateTime todayS = weekBegin.AddDays(i).ToUniversalTime();
               DateTime todayE = weekBegin.AddDays(i + 1).ToUniversalTime();

               List<TimeBlock> block = new List<TimeBlock>();
               block.AddRange(TimeManager.Instance.ScheduleByDayOfWeek[i].Select(s => TimeBlock.FromWeeklySchedule(s, todayS)));
               block.AddRange(weekEvents.Where(b => b.EndTime >= todayS && b.StartTime <= todayE));
               _organizePeriod(block);
               weeklySchedule.Add(block);
            }
         }

         List<List<TimeBlock>> projectPeriods = new();
         {
            if (Parent.CurDailyMode == Timekeep.DailyMode.Today)
            {
               for (int i = 0; i < TimeManager.Instance.Data.Length + 1; i++)
                  projectPeriods.Add(new List<TimeBlock>());
               Dictionary<string, int> identToIndex = Enumerable.Range(0, TimeManager.Instance.Data.Length).ToDictionary(i => TimeManager.Instance.Data[i].Ident, i => i);

               DateTime todayS = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0).ToUniversalTime();
               DateTime todayE = todayS.AddDays(1);
               List<TimeBlock> periods = TimeManager.Instance.QueryTimePeriod(todayS, todayE).Select(p => TimeBlock.FromTimePeriod(p)).ToList();

               foreach (TimeBlock block in periods)
               {
                  int index = 0;
                  if (block.Project == null || !identToIndex.TryGetValue(block.Project.Ident, out index))
                     index = projectPeriods.Count - 1;
                  projectPeriods[index].Add(block);
               }
            }
            else
               for (int i = 0; i < 7; i++)
               {
                  DateTime todayS = weekBegin.AddDays(i).ToUniversalTime();
                  DateTime todayE = weekBegin.AddDays(i + 1).ToUniversalTime();
                  if (Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress)
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
                  if (periods[i].StartTime.ToLocalTime().TimeOfDay.TotalHours > MaxHour)
                     periods.RemoveAt(i);
                  else if (periods[i].EndTime.ToLocalTime().TimeOfDay.TotalHours < MinHour)
                     periods.RemoveAt(i);
               }
         }

         // Figure out length of night.
         double nightLengthHour = -1.0;
         if (Parent.CurDailyMode == Timekeep.DailyMode.Today)
         {
            TimeSpan midnight = new(3, 0, 0);
            DateTime? earliest = projectPeriods.SelectMany(p => p).Select<TimeBlock, DateTime?>(p => p.StartTime).Where(p => p?.ToLocalTime().TimeOfDay >= midnight).DefaultIfEmpty(null).Min();
            if (earliest != null)
            {
               List<TimeBlock> preperiods = TimeManager.Instance.QueryTimePeriod(earliest.Value.AddHours(-24), earliest.Value).Select(p => TimeBlock.FromTimePeriod(p)).ToList();
               DateTime? latest = preperiods.Select<TimeBlock, DateTime?>(p => p.EndTime).Where(p => p < earliest).DefaultIfEmpty(null).Max();
               if (latest != null)
                  nightLengthHour = (earliest.Value - latest.Value).TotalHours;
            }
         }

         // Figure out the time ranges
         int[] showHours = Enumerable.Range(MinHour + 1, MaxHour - (MinHour + 1)).Where(h => (h % 3) == 0).Prepend(MinHour).Append(MaxHour).ToArray();
         Parent.HeaderRow.ColumnDefinitions.Clear();
         for (int i = 0; i < showHours.Length - 1; i++)
            Parent.HeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(showHours[i + 1] - showHours[i], GridUnitType.Star) });

         // Fill in time headers
         _populate(Parent.HeaderRow, templateHeaderCell, showHours.Length);
         for (int i = 0; i < showHours.Length; i++)
         {
            ContentControl content = Parent.HeaderRow.Children[i] as ContentControl;
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            TextBlock text = VisualTreeHelper.GetChild(presenter, 0) as TextBlock;

            Grid.SetColumn(content, Math.Min(i, showHours.Length - 2));
            text.Text = $"{showHours[i]:D2}00";
            text.HorizontalAlignment = (i + 1 == showHours.Length) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            text.Margin = (i + 1 == showHours.Length) ? new Thickness(0, 0, -50, 0) : new Thickness(-50, 0, 0, 0);
         }

         // Hour dividers
         int hourCount = MaxHour - MinHour;
         Parent.DailyDividers.ColumnDefinitions.Clear();
         for (int i = 0; i < hourCount * 2; i++)
            Parent.DailyDividers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

         _populate(Parent.DailyDividers, templateDivider, (hourCount * 2) + 1);
         for (int i = 0; i < (hourCount * 2) + 1; i++)
         {
            ContentControl content = Parent.DailyDividers.Children[i] as ContentControl;
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;

            Grid.SetColumn(content, Math.Min(i, (hourCount * 2) - 1));
            grid.Background = new SolidColorBrush(Extension.FromArgb((uint)((i % 2) == 0 ? 0x40449637 : 0x60042508)));
            grid.HorizontalAlignment = (i == hourCount * 2) ? HorizontalAlignment.Right : HorizontalAlignment.Left;
         }

         // Show the current time.
         bool showNowLine = (Parent.CurDailyMode == Timekeep.DailyMode.Today) && dayBegin <= DateTime.Now && DateTime.Now < dayBegin.AddDays(1);
         Parent.NowDividerGrid.Visibility = showNowLine ? Visibility.Visible : Visibility.Collapsed;
         if (showNowLine)
         {
            double hourProgress = Math.Min(1.0, (DateTime.Now - dayBegin.AddHours(MinHour)).TotalHours / (double)(MaxHour - MinHour));
            Parent.NowDividerGrid.ColumnDefinitions.Clear();
            Parent.NowDividerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourProgress, GridUnitType.Star) });
            Parent.NowDividerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - hourProgress, GridUnitType.Star) });
         }

         // Scheduled events
         Parent.ScheduleGrid.Visibility = (Parent.CurDailyMode == Timekeep.DailyMode.Today) ? Visibility.Visible : Visibility.Collapsed;
         if (Parent.CurDailyMode == Timekeep.DailyMode.Today)
         {
            DateTime dayBeginUtc = dayBegin.ToUniversalTime();
            List<TimeBlock> block = weeklySchedule[(int)day.DayOfWeek];

            Parent.ScheduleGrid.ColumnDefinitions.Clear();
            double lastHour = MinHour;
            for (int i = 0; i < block.Count; i++)
            {
               double hourBegin = (block[i].StartTime - dayBeginUtc).TotalHours;
               double hourEnd = (block[i].EndTime - dayBeginUtc).TotalHours;
               Parent.ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(hourBegin - lastHour, 0), GridUnitType.Star) });
               Parent.ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourEnd - hourBegin, GridUnitType.Star) });
               lastHour = hourEnd;
            }
            Parent.ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MaxHour - lastHour, GridUnitType.Star) });

            _populate(Parent.ScheduleGrid, templateSchedule, block.Count);
            for (int i = 0; i < block.Count; i++)
            {
               ContentControl content = Parent.ScheduleGrid.Children[i] as ContentControl;
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

         // Night length
         Parent.NightLength.Visibility = nightLengthHour > 0.0 ? Visibility.Visible : Visibility.Collapsed;
         if (nightLengthHour > 0.0)
         {
            (int h, int m) = Math.DivRem((int)Math.Round(nightLengthHour * 60.0), 60);
            Parent.NightLengthH.Text = $"{h}";
            Parent.NightLengthM.Text = $"{m:D2}";
         }

         // Indicators
         Parent.IndicatorPanel.Visibility = (Parent.CurDailyMode == Timekeep.DailyMode.Today) ? Visibility.Visible : Visibility.Collapsed;
         if (Parent.CurDailyMode == Timekeep.DailyMode.Today)
         {
            DateTime todayLocalS = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0);
            DateTime todayLocalE = todayLocalS.AddDays(1);
            DateTime todayS = todayLocalS.ToUniversalTime();
            DateTime todayE = todayS.AddDays(1);
            List<TimeIndicator> indicators = TimeManager.Instance.QueryTimeIndicator(todayS, todayE);
            HashSet<IndicatorValue> already = new HashSet<IndicatorValue>();

            DataTemplater.Populate(Parent.Indicators, templateIndicator, indicators, (content, root, _, entry) =>
            {
               double pct = ((entry.Time.ToLocalTime() - todayLocalS).TotalHours - MinHour) / (MaxHour - MinHour);
               StackPanelShift.SetAlong(content, pct);

               IndicatorCtrl ctrl = new IndicatorCtrl(root);
               IndicatorValue value = TimeManager.Instance.GetIndicatorValue(entry.Category, entry.Value);
               (root as Panel).Background = new SolidColorBrush(value.GetColorBack(0x58));
               ctrl.Icon.Foreground = new SolidColorBrush(value.GetColorFore());
               ctrl.Text.Foreground = new SolidColorBrush(value.GetColorBack());
               ctrl.Icon.Text = value.Icon;

               bool isNew = already.Add(value);
               ctrl.Text.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;
               if (isNew)
                  ctrl.Text.Text = value.Name;
            });
         }

         // Fill in rows
         double sumMinutes = 0;
         _populate(Parent.DailyRows, templateRow, projectPeriods.Count);
         for (int i = 0; i < projectPeriods.Count; i++)
         {
            DateTime thisDateBeginLocal = (Parent.CurDailyMode != Timekeep.DailyMode.Today ? weekBegin.AddDays(i) : dayBegin).AddHours(MinHour);
            DateTime thisDateBegin = (Parent.CurDailyMode != Timekeep.DailyMode.Today ? weekBegin.AddDays(i) : dayBegin).ToUniversalTime().AddHours(MinHour);
            DateTime thisDateEnd = thisDateBegin.AddHours(MaxHour - MinHour);

            bool today = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) == 0;
            bool postToday = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) > 0;
            bool preToday = !today && !postToday;

            List<TimeBlock> periods = projectPeriods[i];
            double totalMinutes;
            {
               IEnumerable<TimeBlock> periodsForThis;
               if (Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress && Parent.ProjectBar.DailyFocus != null)
                  periodsForThis = periods.Where(p => p.Project == Parent.ProjectBar.DailyFocus);
               else
                  periodsForThis = periods.AsEnumerable();
               totalMinutes = periodsForThis.Sum(p => (p.EndTime - p.StartTime).TotalMinutes);
            }
            sumMinutes += totalMinutes;

            // Figure out length of night.
            double thisNightLengthHour = -1.0;
            if (Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress)
            {
               DateTime? earliest = projectPeriods.SelectMany(p => p).Select<TimeBlock, DateTime?>(p => p.StartTime).Where(p => p >= thisDateBeginLocal).DefaultIfEmpty(null).Min();
               if (earliest != null)
               {
                  List<TimeBlock> preperiods = TimeManager.Instance.QueryTimePeriod(earliest.Value.AddHours(-24), earliest.Value).Select(p => TimeBlock.FromTimePeriod(p)).ToList();
                  DateTime? latest = preperiods.Select<TimeBlock, DateTime?>(p => p.EndTime).Where(p => p < earliest).DefaultIfEmpty(null).Max();
                  if (latest != null)
                     thisNightLengthHour = (earliest.Value - latest.Value).TotalHours;
               }
            }

            Grid cellParent;
            {
               // Pick the right control.
               ContentControl content = Parent.DailyRows.Children[i] as ContentControl;
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
               Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;

               TextBlock headerName = grid.FindElementByName<TextBlock>("HeaderName");
               TextBlock headerDate = grid.FindElementByName<TextBlock>("HeaderDate");
               TextBlock headerNightH = grid.FindElementByName<TextBlock>("HeaderNightH");
               TextBlock headerNightM = grid.FindElementByName<TextBlock>("HeaderNightM");
               TextBlock headerTimeH = grid.FindElementByName<TextBlock>("HeaderTimeH");
               TextBlock headerTimeM = grid.FindElementByName<TextBlock>("HeaderTimeM");
               cellParent = grid.FindElementByName<Grid>("DailyCells");

               // Show the current time.
               bool showNowLineRow = (Parent.CurDailyMode != Timekeep.DailyMode.Today) && thisDateBeginLocal <= DateTime.Now;
               Grid rowNowGrid = grid.FindElementByName<Grid>("RowNowGrid");
               rowNowGrid.Visibility = showNowLineRow ? Visibility.Visible : Visibility.Collapsed;
               if (showNowLineRow)
               {
                  double hourProgress = Math.Min(1.0, (DateTime.Now - thisDateBeginLocal).TotalHours / (double)(MaxHour - MinHour));
                  rowNowGrid.ColumnDefinitions.Clear();
                  rowNowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourProgress, GridUnitType.Star) });
                  rowNowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - hourProgress, GridUnitType.Star) });
               }

               //
               headerNightH.Visibility = thisNightLengthHour > 0.0 ? Visibility.Visible : Visibility.Collapsed;
               headerNightM.Visibility = thisNightLengthHour > 0.0 ? Visibility.Visible : Visibility.Collapsed;
               if (thisNightLengthHour > 0.0)
               {
                  int thisNightLengthMin = (int)Math.Round(thisNightLengthHour * 60.0);
                  headerNightH.Text = $"{thisNightLengthMin / 60}";
                  headerNightM.Text = $"{thisNightLengthMin % 60:D2}";
               }

               // Update the text
               Project? project = i < TimeManager.Instance.Data.Length ? TimeManager.Instance.Data[i] : null;
               if ((Parent.CurDailyMode == Timekeep.DailyMode.Today && project == Parent.ProjectBar.InitialProject) || (Parent.CurDailyMode != Timekeep.DailyMode.Today && today))
               {
                  grid.Background = new SolidColorBrush(Extension.FromArgb((uint)0x404A6030));
                  headerName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFFC3F1AF));
                  headerDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFFC3F1AF));
               }
               else if ((Parent.CurDailyMode == Timekeep.DailyMode.WeekSchedule && preToday) || (Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress && postToday))
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

               headerName.Text = Parent.CurDailyMode != Timekeep.DailyMode.Today ? thisDateBegin.ToString("ddd") : (project?.Name ?? "");
               headerDate.Visibility = Parent.CurDailyMode != Timekeep.DailyMode.Today ? Visibility.Visible : Visibility.Collapsed;
               headerDate.Text = thisDateBegin.ToString("yy/MM/dd");

               if ((Parent.CurDailyMode == Timekeep.DailyMode.WeekSchedule && preToday) || (Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress && postToday))
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
            for (int o = 0; o < periodsFilter.Count; o++)
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
               TextBlock projectName = grid.FindElementByName<TextBlock>("ProjectName");
               Grid activeGlow = grid.FindElementByName<Grid>("ActiveGlow");

               if (Parent.CurDailyMode == Timekeep.DailyMode.WeekSchedule || Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress)
               {
                  activeGlow.Visibility = Visibility.Collapsed;
                  cellTimeH.Visibility = Visibility.Collapsed;
                  startTime.Visibility = Visibility.Collapsed;
                  endTime.Visibility = Visibility.Collapsed;
                  cellTimeM.Visibility = Visibility.Visible;
                  projectName.Visibility = Visibility.Collapsed;
                  bool thisProject = Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress && periodsFilter[o].Project == Parent.ProjectBar.DailyFocus;
                  if (Parent.CurDailyMode == Timekeep.DailyMode.WeekProgress && Parent.ProjectBar.DailyFocus != null && !thisProject)
                  {
                     grid.Background = new SolidColorBrush(Extension.FromArgb(0xFF383838));
                     cellTimeM.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF909090));
                  }
                  else if (Parent.CurDailyMode == Timekeep.DailyMode.WeekSchedule && preToday)
                  {
                     grid.Background = new SolidColorBrush(periodsFilter[o].Color.Lerp(Extension.FromArgb(0xFF202020), 0.8f));
                     cellTimeM.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF808080));
                  }
                  else
                  {
                     grid.Background = new SolidColorBrush(periodsFilter[o].Color);
                     SolidColorBrush colorBack = periodsFilter[o].CurStyle == TimeBlock.Style.TimePeriod && periodsFilter[o].Project == null
                        ? new(Extension.FromArgb(0xFF69A582))
                        : new(periodsFilter[o].Color.Lerp(Extension.FromArgb(0xFFC3F1AF), thisProject ? 1.0f : 0.75f));
                     cellTimeM.Foreground = colorBack;
                  }
                  cellTimeM.Text = periodsFilter[o].Name;
               }
               else
               {
                  grid.Background = new SolidColorBrush(periodsFilter[o].Color);
                  SolidColorBrush colorBack = new(Extension.FromArgb(periodsFilter[o].Project != null ? 0xFF449637 : 0xFF517F65));
                  SolidColorBrush colorFore = new(Extension.FromArgb(periodsFilter[o].Project != null ? 0xFFC3F1AF : 0xFF69A582));

                  bool isActiveCell = TimeManager.Instance.CurrentTimePeriod == periodsFilter[o].DbId;
                  activeGlow.Visibility = isActiveCell ? Visibility.Visible : Visibility.Collapsed;

                  double periodMinutes = (periodsFilter[o].EndTime - periodsFilter[o].StartTime).TotalMinutes;
                  cellTimeH.Visibility = (periodMinutes >= 10.0) ? Visibility.Visible : Visibility.Hidden;
                  cellTimeM.Visibility = (periodMinutes >= 10.0) ? Visibility.Visible : Visibility.Hidden;
                  cellTimeH.Foreground = colorFore;
                  cellTimeM.Foreground = colorFore;
                  cellTimeH.Text = ((int)periodMinutes / 60).ToString();
                  cellTimeM.Text = $"{((int)periodMinutes % 60):D2}";

                  projectName.Visibility = (i >= TimeManager.Instance.Data.Length) ? Visibility.Visible : Visibility.Collapsed;
                  projectName.Foreground = colorBack;
                  if (i >= TimeManager.Instance.Data.Length)
                     projectName.Text = periodsFilter[o].Name;

                  startTime.Visibility = (periodMinutes >= 45) ? Visibility.Visible : Visibility.Collapsed;
                  endTime.Visibility = (periodMinutes >= 45) ? Visibility.Visible : Visibility.Collapsed;
                  startTime.Foreground = colorBack;
                  endTime.Foreground = colorBack;
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
         Parent.SumTimeH.Text = ((int)sumMinutes / 60).ToString();
         Parent.SumTimeM.Text = $"{((int)sumMinutes % 60):D2}";
      }

      double? DailyRow_Pct1 = null;
      public void TimeAreaHotspot_MouseEnter(object sender, MouseEventArgs e)
      {
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
            return;
         Parent.DailyRowHover.Visibility = Visibility.Visible;
         DailyRow_Pct1 = null;
         TimeAreaHotspot_MouseMove(sender, e);
      }

      public void TimeAreaHotspot_MouseLeave(object sender, MouseEventArgs e)
      {
         Parent.DailyRowHover.Visibility = Visibility.Collapsed;
      }

      public void TimeAreaHotspot_MouseMove(object sender, MouseEventArgs e)
      {
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
            return;

         Point pos = e.GetPosition(Parent.TimeAreaHotspot);
         double pctX = pos.X / Parent.TimeAreaHotspot.RenderSize.Width;
         if (DailyRow_Pct1 == null)
         {
            FreeGrid.SetLeft(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Percent, pctX));
            FreeGrid.SetWidth(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Pixel, 1));
         }
         else
         {
            double minPct = (pctX < DailyRow_Pct1.Value) ? pctX : DailyRow_Pct1.Value;
            double maxPct = (pctX >= DailyRow_Pct1.Value) ? pctX : DailyRow_Pct1.Value;
            FreeGrid.SetLeft(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Percent, minPct));
            FreeGrid.SetWidth(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Percent, maxPct - minPct));
         }
      }

      public void TimeAreaHotspot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
            return;

         Point pos = e.GetPosition(Parent.TimeAreaHotspot);
         DailyRow_Pct1 = pos.X / Parent.TimeAreaHotspot.RenderSize.Width;
         TimeAreaHotspot_MouseMove(sender, e);
      }

      public void TimeAreaHotspot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
      {
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
            return;
         if (DailyRow_Pct1 == null)
            return;

         DateTime periodBegin;
         DateTime periodEnd;
         {
            Point pos = e.GetPosition(Parent.TimeAreaHotspot);
            double pct1 = DailyRow_Pct1.Value;
            double pct2 = pos.X / Parent.TimeAreaHotspot.RenderSize.Width;

            DateTime dayBegin = new DateTime(Parent.DailyDay.Year, Parent.DailyDay.Month, Parent.DailyDay.Day, 0, 0, 0);
            if (pct2 < pct1)
            {
               double pct = pct2;
               pct2 = pct1;
               pct1 = pct;
            }
            periodBegin = dayBegin.AddHours(Parent.TimeBar.MinHour + (pct1 * (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour))).ToUniversalTime();
            periodEnd = dayBegin.AddHours(Parent.TimeBar.MinHour + (pct2 * (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour))).ToUniversalTime();

            DateTime utcNow = DateTime.UtcNow;
            if (periodBegin > utcNow)
               periodBegin = utcNow;
            if (periodEnd > utcNow)
               periodEnd = utcNow;

            List<TimePeriod> results = TimeManager.Instance.QueryTimePeriod(periodBegin, periodEnd);
            if (results.Count > 0)
            {
               periodBegin = results.Select(p => p.EndTime).Where(t => t < periodEnd).Append(periodBegin).Max();
               periodEnd = results.Select(p => p.StartTime).Where(t => t > periodBegin).Append(periodEnd).Min();
            }
         }

         DailyRow_Pct1 = null;
         Parent.DailyRowHover.Visibility = Visibility.Collapsed;

         if ((periodEnd - periodBegin).TotalMinutes < 2.0)
            return;

         Parent.TimekeepTextEditor.Show((text) =>
         {
            int projectId = TimeManager.Instance.AddProject(text);
            TimeManager.Instance.AddHistoryLine(new TimePeriod() { DbId = projectId, Ident = text, StartTime = periodBegin, EndTime = periodEnd });

            Refresh(Parent.DailyDay);
         });
      }
   }
}
