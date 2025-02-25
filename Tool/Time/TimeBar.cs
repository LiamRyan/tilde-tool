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
   public abstract class TimeBar
   {
      public Timekeep Parent;
      public TimeBar(Timekeep parent)
      {
         Parent = parent;
      }

      #region Population

      public int MinHour = 6;
      public int MaxHour = 23;

      protected DateTime DayBegin;  // new DateTime(day.Year, day.Month, day.Day, 0, 0, 0)
      protected DateTime WeekBegin; // dayBegin.AddDays(-(int)day.DayOfWeek)

      #endregion
      #region Population Data

      public class TimeBlockRow
      {
         public string RowName;
         public bool IsHighlight;
         public bool IsGray;

         public bool HasDate;
         public bool ShowNowLine;
         public double NightLength = -1.0;
         public double TotalMinutes;

         public DateTime DayBeginUtc;

         public List<TimeBlock> Blocks;
      }

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

         public bool IsActiveCell = false;
         public double PeriodMinutes = -1.0;
         public string CellTitle = null;
         public string CellProject = null;
         public SolidColorBrush ColorGrid = null;
         public SolidColorBrush ColorBack = null;
         public SolidColorBrush ColorFore = null;

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

      #endregion
      #region Population

      List<TimeBlockRow> Populate()
      {
         MinHour = 6;
         MaxHour = 23;

         List<TimeBlockRow> projectPeriods = CollectTimeBlocks();
         foreach (TimeBlockRow periods in projectPeriods)
            _organizePeriod(periods.Blocks);
         foreach (TimeBlockRow periods in projectPeriods)
            for (int i = periods.Blocks.Count - 1; i >= 0; i--)
            {
               if (periods.Blocks[i].StartTime.ToLocalTime().TimeOfDay.TotalHours > MaxHour)
                  periods.Blocks.RemoveAt(i);
               else if (periods.Blocks[i].EndTime.ToLocalTime().TimeOfDay.TotalHours < MinHour)
                  periods.Blocks.RemoveAt(i);
            }

         return projectPeriods;
      }

      public abstract List<TimeBlockRow> CollectTimeBlocks();

      protected void _organizePeriod(List<TimeBlock> periods)
      {
         // Sort by time.
         periods.Sort((a, b) => a.StartTime != b.StartTime ? a.StartTime.CompareTo(b.StartTime) : a.EndTime.CompareTo(b.EndTime));

         // Handle time periods
         TimeBlock? prevPeriod = null;
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

      #endregion
      #region Master Refresh

      public void Refresh()
      {
         Parent.DailyContent.Visibility = Visibility.Visible;

         DateTime dailyDay = Parent.DailyDay;
         DayBegin = new DateTime(dailyDay.Year, dailyDay.Month, dailyDay.Day, 0, 0, 0);
         if (DayBegin > DateTime.Now.AddDays(7))
            DayBegin = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0).AddDays(7);
         WeekBegin = DayBegin.AddDays(-(int)DayBegin.DayOfWeek);

         List<TimeBlockRow> rows = Populate();
         RefreshTimeHeader();
         RefreshPane(rows);

         Parent.IndicatorPanel.Visibility = Visibility.Collapsed;
         Parent.NightLength.Visibility = Visibility.Collapsed;
         Parent.ScheduleGrid.Visibility = Visibility.Collapsed;
         Parent.NowDividerGrid.Visibility = Visibility.Collapsed;
         SubRefresh();
      }

      public virtual void SubRefresh() { }

      #endregion
      #region Subcontrol

      public void RefreshTimeHeader()
      {
         // Figure out the time ranges
         int[] showHours = Enumerable.Range(MinHour + 1, MaxHour - (MinHour + 1)).Where(h => (h % 3) == 0).Prepend(MinHour).Append(MaxHour).ToArray();
         Parent.HeaderRow.ColumnDefinitions.Clear();
         for (int i = 0; i < showHours.Length - 1; i++)
            Parent.HeaderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(showHours[i + 1] - showHours[i], GridUnitType.Star) });

         // Fill in time headers
         DataTemplate? templateHeaderCell = Parent.Resources["DailyHeaderCell"] as DataTemplate;
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

         DataTemplate? templateDivider = Parent.Resources["DailyDivider"] as DataTemplate;
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
      }

      #endregion

      protected void _populate(Panel parent, DataTemplate template, int count)
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

      public List<List<TimeBlock>> GetWeeklySchedule()
      {
         List<List<TimeBlock>> weeklySchedule = new List<List<TimeBlock>>();

         DateTime weekEnd = WeekBegin.AddDays(7);
         TimeBlock[] weekEvents = TimeManager.Instance.QueryTimeEvent(WeekBegin, weekEnd).Select(s => TimeBlock.FromTimeEvent(s)).ToArray();

         for (int i = 0; i < 7; i++)
         {
            if (Parent.CurDailyMode == Timekeep.DailyMode.Today && (DayOfWeek)i != DayBegin.DayOfWeek)
            {
               weeklySchedule.Add(null);
               continue;
            }
            DateTime todayS = WeekBegin.AddDays(i).ToUniversalTime();
            DateTime todayE = WeekBegin.AddDays(i + 1).ToUniversalTime();

            List<TimeBlock> block = new List<TimeBlock>();
            block.AddRange(TimeManager.Instance.ScheduleByDayOfWeek[i].Select(s => TimeBlock.FromWeeklySchedule(s, todayS)));
            block.AddRange(weekEvents.Where(b => b.EndTime >= todayS && b.StartTime <= todayE));
            weeklySchedule.Add(block);
         }

         return weeklySchedule;
      }

      #region Pane Refresh

      protected void RefreshPane(List<TimeBlockRow> projectPeriods)
      {
         // Fill in rows
         DataTemplate? templateRow = Parent.Resources["DailyRow"] as DataTemplate;
         DataTemplater.Populate<TimeBlockRow, TimeRow>(Parent.DailyRows, templateRow, projectPeriods, RefreshRow);

         double sumMinutes = projectPeriods.Sum(r => r.TotalMinutes);
         Parent.SumTimeH.Text = ((int)sumMinutes / 60).ToString();
         Parent.SumTimeM.Text = $"{((int)sumMinutes % 60):D2}";
      }

      class TimeRow : DataTemplater
      {
         public TextBlock HeaderName;
         public TextBlock HeaderDate;
         public TextBlock HeaderNightH;
         public TextBlock HeaderNightM;
         public TextBlock HeaderTimeH;
         public TextBlock HeaderTimeM;

         public Grid RowNowGrid;

         public Grid DailyCells;

         public TimeRow(FrameworkElement root) : base(root) { }
      }

      void RefreshRow(TimeRow ui, int index, TimeBlockRow row)
      {
         DateTime thisDateBegin = row.DayBeginUtc.AddHours(MinHour);
         DateTime thisDateEnd = row.DayBeginUtc.AddHours(MaxHour);
         DateTime thisDateBeginLocal = row.DayBeginUtc.AddHours(MinHour).ToLocalTime();

         bool today = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) == 0;
         bool postToday = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) > 0;
         bool preToday = !today && !postToday;

         RefreshRowHeader(ui, index, row);

         // Merge time blocks that are adjacent.
         List<TimeBlock> periodsFilter = new List<TimeBlock>(row.Blocks.Count);
         {
            TimeBlock pendingBlock = null;
            for (int r = 0; r < row.Blocks.Count; r++)
            {
               if (pendingBlock != null && pendingBlock.CanMerge(row.Blocks[r]))
                  pendingBlock = pendingBlock.Merge(row.Blocks[r]);
               else
               {
                  if (pendingBlock != null)
                     periodsFilter.Add(pendingBlock);
                  pendingBlock = row.Blocks[r];
               }
            }
            if (pendingBlock != null)
               periodsFilter.Add(pendingBlock);
         }

         // Populate
         DateTime lastDate = thisDateBegin;
         ui.DailyCells.ColumnDefinitions.Clear();

         DataTemplate? templateCell = Parent.Resources["DailyCell"] as DataTemplate;
         DataTemplater.Populate<TimeBlock, TimeCell>(ui.DailyCells, templateCell, periodsFilter, (subui, subindex, subdata) =>
         {
            RefreshCell(subui, subindex, subdata);

            // Show the current time.
            DateTime periodStartTime = subdata.StartTime > lastDate ? subdata.StartTime : lastDate;
            DateTime periodEndTime = subdata.EndTime < thisDateEnd ? subdata.EndTime : thisDateEnd;
            if (periodEndTime > periodStartTime)
            {
               ui.DailyCells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((periodStartTime - lastDate).TotalHours, GridUnitType.Star) });
               ui.DailyCells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((periodEndTime - periodStartTime).TotalHours, GridUnitType.Star) });
               lastDate = periodEndTime;
               Grid.SetColumn(subui.Content, (subindex * 2) + 1);
            }
         });
         ui.DailyCells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((thisDateEnd - lastDate).TotalHours, GridUnitType.Star) });
      }

      void RefreshRowHeader(TimeRow ui, int index, TimeBlockRow row)
      {
         DateTime thisDateBeginLocal = row.DayBeginUtc.AddHours(MinHour).ToLocalTime();

         // Show the current time.
         ui.RowNowGrid.Visibility = row.ShowNowLine ? Visibility.Visible : Visibility.Collapsed;
         if (row.ShowNowLine)
         {
            double hourProgress = Math.Min(1.0, (DateTime.Now - thisDateBeginLocal).TotalHours / (double)(MaxHour - MinHour));
            ui.RowNowGrid.ColumnDefinitions.Clear();
            ui.RowNowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourProgress, GridUnitType.Star) });
            ui.RowNowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - hourProgress, GridUnitType.Star) });
         }

         // Night length
         {
            double thisNightLengthHour = row.NightLength;
            ui.HeaderNightH.Visibility = thisNightLengthHour > 0.0 ? Visibility.Visible : Visibility.Collapsed;
            ui.HeaderNightM.Visibility = thisNightLengthHour > 0.0 ? Visibility.Visible : Visibility.Collapsed;
            if (thisNightLengthHour > 0.0)
            {
               int thisNightLengthMin = (int)Math.Round(thisNightLengthHour * 60.0);
               ui.HeaderNightH.Text = $"{thisNightLengthMin / 60}";
               ui.HeaderNightM.Text = $"{thisNightLengthMin % 60:D2}";
            }
         }

         // Update the text
         Grid grid = ui.Root as Grid;
         if (row.IsHighlight)
         {
            grid.Background = new SolidColorBrush(Extension.FromArgb((uint)0x404A6030));
            ui.HeaderName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFFC3F1AF));
            ui.HeaderDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFFC3F1AF));
         }
         else if (row.IsGray)
         {
            grid.Background = new SolidColorBrush(Extension.FromArgb((uint)((index % 2) == 0 ? 0x80202020 : 0x80282828)));
            ui.HeaderName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF606060));
            ui.HeaderDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF606060));
         }
         else
         {
            grid.Background = new SolidColorBrush(Extension.FromArgb((uint)((index % 2) == 0 ? 0x00042508 : 0x38042508)));
            ui.HeaderName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF449637));
            ui.HeaderDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF449637));
         }

         ui.HeaderName.Text = row.RowName;
         ui.HeaderDate.Visibility = row.HasDate ? Visibility.Visible : Visibility.Collapsed;
         if (row.HasDate)
            ui.HeaderDate.Text = thisDateBeginLocal.ToString("yy/MM/dd");

         if (row.IsGray)
         {
            ui.HeaderTimeH.Foreground = new SolidColorBrush(Extension.FromRgb((uint)0x606060));
            ui.HeaderTimeM.Foreground = new SolidColorBrush(Extension.FromRgb((uint)0x606060));
         }
         else
         {
            ui.HeaderTimeH.Foreground = new SolidColorBrush(Extension.FromRgb((uint)((row.TotalMinutes > 0) ? 0xC3F1AF : 0x449637)));
            ui.HeaderTimeM.Foreground = new SolidColorBrush(Extension.FromRgb((uint)((row.TotalMinutes > 0) ? 0xC3F1AF : 0x449637)));
         }
         ui.HeaderTimeH.Text = ((int)row.TotalMinutes / 60).ToString();
         ui.HeaderTimeM.Text = $"{((int)row.TotalMinutes % 60):D2}";
      }

      class TimeCell : DataTemplater
      {
         public TextBlock StartTime;
         public TextBlock EndTime;
         public TextBlock CellTimeH;
         public TextBlock CellTimeM;
         public TextBlock ProjectName;
         public Grid ActiveGlow;

         public TimeCell(FrameworkElement root) : base(root) { }
      }

      void RefreshCell(TimeCell subui, int subindex, TimeBlock subdata)
      {
         Grid grid = subui.Root as Grid;
         grid.Background = subdata.ColorGrid;

         subui.ActiveGlow.Visibility = subdata.IsActiveCell ? Visibility.Visible : Visibility.Collapsed;

         subui.ProjectName.Visibility = !string.IsNullOrEmpty(subdata.CellProject) ? Visibility.Visible : Visibility.Collapsed;
         if (!string.IsNullOrEmpty(subdata.CellProject))
         {
            subui.ProjectName.Foreground = subdata.ColorBack;
            subui.ProjectName.Text = subdata.CellProject;
         }

         subui.CellTimeH.Visibility = (subdata.PeriodMinutes >= 10.0) ? Visibility.Visible : Visibility.Hidden;
         subui.CellTimeM.Visibility = (subdata.PeriodMinutes >= 10.0 || !string.IsNullOrEmpty(subdata.CellTitle)) ? Visibility.Visible : Visibility.Hidden;
         if (subdata.PeriodMinutes >= 10.0)
         {
            subui.CellTimeH.Foreground = subdata.ColorFore;
            subui.CellTimeM.Foreground = subdata.ColorFore;
            subui.CellTimeH.Text = ((int)subdata.PeriodMinutes / 60).ToString();
            subui.CellTimeM.Text = $"{((int)subdata.PeriodMinutes % 60):D2}";
         }
         else if (!string.IsNullOrEmpty(subdata.CellTitle))
         {
            subui.CellTimeM.Foreground = subdata.ColorBack;
            subui.CellTimeM.Text = subdata.CellTitle;
         }

         subui.StartTime.Visibility = (subdata.PeriodMinutes >= 45) ? Visibility.Visible : Visibility.Collapsed;
         subui.EndTime.Visibility = (subdata.PeriodMinutes >= 45) ? Visibility.Visible : Visibility.Collapsed;
         if (subdata.PeriodMinutes >= 45)
         {
            subui.StartTime.Foreground = subdata.ColorBack;
            subui.EndTime.Foreground = subdata.ColorBack;
            subui.StartTime.Text = $"{subdata.StartTime.ToLocalTime().Hour:D2}{subdata.StartTime.ToLocalTime().Minute:D2}";
            subui.EndTime.Text = $"{subdata.EndTime.ToLocalTime().Hour:D2}{subdata.EndTime.ToLocalTime().Minute:D2}";
         }
      }

      #endregion
      #region Mouse Input

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

         List<string> idents = TimeManager.Instance.ProjectIdentToId.Keys.ToList();
         Parent.TimekeepTextEditor.Show((text) =>
         {
            int projectId = TimeManager.Instance.AddProject(text);
            TimeManager.Instance.AddHistoryLine(new TimePeriod() { DbId = projectId, Ident = text, StartTime = periodBegin, EndTime = periodEnd });

            Refresh();
         }, idents);
      }

      #endregion
   }
}
