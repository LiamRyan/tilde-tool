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

      public int MinHour = 7;
      public int MaxHour = 22;

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

         public DateOnly Day;

         public Project Project;
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
         public bool OnComputer;
         public Color Color;
         public Style CurStyle;
         public int Priority;

         public Project? Project = null;
         public long DbId = -1;

         public bool IsActiveCell = false;
         public string CellTitle = null;
         public string CellProject = null;
         public SolidColorBrush ColorGrid = null;
         public SolidColorBrush ColorBack = null;
         public SolidColorBrush ColorFore = null;

         public double PeriodMinutes => (EndTime - StartTime).TotalMinutes;

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
               OnComputer = period.OnComputer,
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
            if (OnComputer != next.OnComputer)
               return false;
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
            TimeBlock result = (TimeBlock)MemberwiseClone();
            result.EndTime = next.EndTime;
            result.DbId = next.DbId;
            return result;
         }
      }

      #endregion
      #region Population

      List<TimeBlockRow> Populate()
      {
         MinHour = 7;
         MaxHour = 22;

         List<TimeBlockRow> projectPeriods = CollectTimeBlocks();
         foreach (TimeBlockRow periods in projectPeriods)
            _organizePeriod(periods.Blocks);

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
            double padding = period.CurStyle == TimeBlock.Style.TimePeriod ? 0.5 : 0.0;
            // Increase or decrease our total time range.
            if (period.StartTime.ToLocalTime().TimeOfDay.TotalHours - padding < MinHour)
               MinHour = Math.Max(0, (int)Math.Floor(period.StartTime.ToLocalTime().Hour - padding));
            if (period.EndTime.ToLocalTime().TimeOfDay.TotalHours + padding > MaxHour)
               MaxHour = Math.Min(24, (int)Math.Ceiling(period.EndTime.ToLocalTime().Hour + 1.0 + padding));

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

      protected class TimeRow : DataTemplater
      {
         public TextBlock HeaderName;
         public TextBlock HeaderNameR;
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
         DateTime thisDateBeginLocal = row.Day.ToDateTime(new TimeOnly(MinHour, 0));
         DateTime thisDateBegin = row.Day.ToDateTime(new TimeOnly(MinHour, 0)).ToUniversalTime();
         DateTime thisDateEnd = row.Day.ToDateTime(new TimeOnly(MaxHour % 24, 0)).AddDays(MaxHour / 24).ToUniversalTime();

         bool today = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) == 0;
         bool postToday = thisDateBeginLocal.Date.CompareTo(DateTime.Now.Date) > 0;
         bool preToday = !today && !postToday;

         // Set to defaults
         {
            (ui.Root as Grid).Height = 35;
            ui.HeaderName.FontSize = 16;
            ui.HeaderTimeH.FontSize = 16;
            ui.HeaderTimeM.FontSize = 12;
            ui.HeaderName.Foreground = new SolidColorBrush(Extension.FromRgb(0xFF449637));
         }

         RefreshRowHeader(ui, index, row);
         _RefreshRow(ui, index, row);

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
            {
               DateTime periodStartTime = pendingBlock.StartTime > thisDateBegin ? pendingBlock.StartTime : thisDateBegin;
               DateTime periodEndTime = pendingBlock.EndTime < thisDateEnd ? pendingBlock.EndTime : thisDateEnd;
               if (periodEndTime > periodStartTime)
                  periodsFilter.Add(pendingBlock);
            }
         }

         // Populate
         DateTime lastDate = thisDateBegin;
         ui.DailyCells.ColumnDefinitions.Clear();

         DataTemplate? templateCell = Parent.Resources["DailyCell"] as DataTemplate;
         DataTemplater.Populate<TimeBlock, TimeCell>(ui.DailyCells, templateCell, periodsFilter, (subui, subindex, subdata) =>
         {
            (subui.Root as Grid).Height = 20;

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

            _RefreshCell(subui, subindex, subdata);
         });
         ui.DailyCells.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength((thisDateEnd - lastDate).TotalHours, GridUnitType.Star) });
      }

      protected virtual void _RefreshRow(TimeRow ui, int index, TimeBlockRow row) { }

      protected virtual void _RefreshCell(TimeCell subui, int subindex, TimeBlock subdata) { }

      void RefreshRowHeader(TimeRow ui, int index, TimeBlockRow row)
      {
         DateTime thisDateBeginLocal = row.Day.ToDateTime(new TimeOnly(MinHour, 0));

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
            grid.Background = new SolidColorBrush(Extension.FromArgb((uint)((index % 2) == 0 ? 0x00042508 : 0x60042508)));
            ui.HeaderName.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF449637));
            ui.HeaderDate.Foreground = new SolidColorBrush(Extension.FromArgb(0xFF449637));
         }
         ui.HeaderNameR.Visibility = Visibility.Collapsed;

         ui.HeaderName.Text = row.RowName;
         ui.HeaderNameR.Text = row.RowName;
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

      protected class TimeCell : DataTemplater
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

         subui.ProjectName.Visibility = !string.IsNullOrEmpty(subdata.CellProject) && !subdata.OnComputer ? Visibility.Visible : Visibility.Collapsed;
         if (!string.IsNullOrEmpty(subdata.CellProject) && !subdata.OnComputer)
         {
            subui.ProjectName.Foreground = subdata.ColorBack;
            subui.ProjectName.Text = subdata.CellProject;
         }

         subui.CellTimeH.Visibility = (subdata.PeriodMinutes >= 10.0 && string.IsNullOrEmpty(subdata.CellTitle)) ? Visibility.Visible : Visibility.Hidden;
         subui.CellTimeM.Visibility = (subdata.PeriodMinutes >= 10.0 || string.IsNullOrEmpty(subdata.CellTitle)) ? Visibility.Visible : Visibility.Hidden;
         if (!string.IsNullOrEmpty(subdata.CellTitle))
         {
            subui.CellTimeM.Foreground = subdata.ColorBack;
            subui.CellTimeM.Text = subdata.CellTitle;
         }
         else if (subdata.PeriodMinutes >= 10.0)
         {
            subui.CellTimeH.Foreground = subdata.ColorFore;
            subui.CellTimeM.Foreground = subdata.ColorFore;
            subui.CellTimeH.Text = ((int)subdata.PeriodMinutes / 60).ToString();
            subui.CellTimeM.Text = $"{((int)subdata.PeriodMinutes % 60):D2}";
         }

         subui.StartTime.Visibility = (subdata.PeriodMinutes >= 45 && string.IsNullOrEmpty(subdata.CellTitle)) ? Visibility.Visible : Visibility.Collapsed;
         subui.EndTime.Visibility = (subdata.PeriodMinutes >= 45 && string.IsNullOrEmpty(subdata.CellTitle)) ? Visibility.Visible : Visibility.Collapsed;
         if (subdata.PeriodMinutes >= 45 && string.IsNullOrEmpty(subdata.CellTitle))
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
         DailyRow_Pct1 = null;
         TimeAreaHotspot_MouseMove(sender, e);
      }

      public void TimeAreaHotspot_MouseLeave(object sender, MouseEventArgs e)
      {
         Parent.DailyRowHover.Visibility = Visibility.Collapsed;
         Parent.DailyRowHoverL.Visibility = Visibility.Collapsed;
         Parent.DailyRowHoverR.Visibility = Visibility.Collapsed;
      }

      public void TimeAreaHotspot_MouseMove(object sender, MouseEventArgs e)
      {
         Point pos = e.GetPosition(Parent.TimeAreaHotspot);
         double pctX = pos.X / Parent.TimeAreaHotspot.RenderSize.Width;
         if (DailyRow_Pct1 == null)
         {
            FreeGrid.SetLeft(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Percent, pctX));
            FreeGrid.SetWidth(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Pixel, 1));

            DateTime dayBegin = new DateTime(Parent.DailyDay.Year, Parent.DailyDay.Month, Parent.DailyDay.Day, 0, 0, 0);
            DateTime periodBegin = dayBegin.AddHours(Parent.TimeBar.MinHour + (pctX * (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour))).ToUniversalTime();

            Parent.DailyRowHover.Visibility = Visibility.Visible;
            Parent.DailyRowHoverR.Visibility = Visibility.Collapsed;
            Parent.DailyRowHoverL.Visibility = Visibility.Visible;
            FreeGrid.SetLeft(Parent.DailyRowHoverL, new PercentValue(PercentValue.ModeType.Percent, pctX));
            FreeGrid.SetWidth(Parent.DailyRowHoverL, new PercentValue(PercentValue.ModeType.Pixel, 50));
            Parent.DailyRowHoverL.Margin = new Thickness(-50, -28, 0, 0);
            Parent.DailyRowHoverL.HorizontalAlignment = HorizontalAlignment.Center;
            Parent.DailyRowHoverL.Text = $"{periodBegin.ToLocalTime().Hour:D2}{periodBegin.ToLocalTime().Minute:D2}";
         }
         else
         {
            GetDragTime(DailyRow_Pct1.Value, pctX, out DateTime periodBegin, out DateTime periodEnd);
            DateTime dayBegin = new DateTime(Parent.DailyDay.Year, Parent.DailyDay.Month, Parent.DailyDay.Day, 0, 0, 0);
            double pctA = ((periodBegin.ToLocalTime() - dayBegin).TotalHours - Parent.TimeBar.MinHour) / (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour);
            double pctB = ((periodEnd.ToLocalTime() - dayBegin).TotalHours - Parent.TimeBar.MinHour) / (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour);
            double pctMin = (pctA < pctB) ? pctA : pctB;
            double pctMax = (pctA > pctB) ? pctA : pctB;
            DateTime periodMin = (pctA < pctB) ? periodBegin : periodEnd;
            DateTime periodMax = (pctA > pctB) ? periodBegin : periodEnd;

            FreeGrid.SetLeft(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Percent, pctMin));
            FreeGrid.SetWidth(Parent.DailyRowHover, new PercentValue(PercentValue.ModeType.Percent, pctMax - pctMin));

            bool isSchedule = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            bool tooShort = (periodEnd - periodBegin).TotalMinutes < 1.0;
            Parent.DailyRowHover.Background = new SolidColorBrush(Extension.FromArgb(
               isSchedule ? (uint)0x60282485
               : tooShort ? (uint)0x60852428
               : (uint)0x60248528));

            Parent.DailyRowHover.Visibility = Visibility.Visible;
            Parent.DailyRowHoverL.Visibility = Visibility.Visible;
            Parent.DailyRowHoverR.Visibility = Visibility.Visible;
            FreeGrid.SetLeft(Parent.DailyRowHoverL, new PercentValue(PercentValue.ModeType.Pixel, 0));
            FreeGrid.SetWidth(Parent.DailyRowHoverL, new PercentValue(PercentValue.ModeType.Percent, Math.Max(pctMin, 0)));
            Parent.DailyRowHoverL.HorizontalAlignment = HorizontalAlignment.Right;
            Parent.DailyRowHoverL.Margin = new Thickness(0, -28, 0, 0);
            FreeGrid.SetLeft(Parent.DailyRowHoverR, new PercentValue(PercentValue.ModeType.Percent, pctMax));
            Parent.DailyRowHoverL.Text = $"{periodMin.ToLocalTime().Hour:D2}{periodMin.ToLocalTime().Minute:D2}";
            Parent.DailyRowHoverR.Text = $"{periodMax.ToLocalTime().Hour:D2}{periodMax.ToLocalTime().Minute:D2}";
         }
      }

      List<DateTime> GapsBegin;
      List<DateTime> GapsEnd;
      void GetDragTime(double pct1, double pct2, out DateTime periodBegin, out DateTime periodEnd)
      {
         if (pct2 < pct1)
         {
            double pct = pct2;
            pct2 = pct1;
            pct1 = pct;
         }

         DateTime dayBegin = new DateTime(Parent.DailyDay.Year, Parent.DailyDay.Month, Parent.DailyDay.Day, 0, 0, 0);
         periodBegin = dayBegin.AddHours(Parent.TimeBar.MinHour + (pct1 * (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour))).ToUniversalTime();
         periodEnd = dayBegin.AddHours(Parent.TimeBar.MinHour + (pct2 * (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour))).ToUniversalTime();

         if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            return;

         DateTime utcNow = DateTime.UtcNow;
         if (periodBegin > utcNow)
            periodBegin = utcNow;
         if (periodEnd > utcNow)
            periodEnd = utcNow;

         if (GapsBegin.Count > 0)
         {
            DateTime periodBeginFull = periodBegin;
            DateTime periodEndFull = periodEnd;
            double bestLengthHour = -1.0;
            for (int i = 0; i < GapsBegin.Count; i++)
            {
               DateTime periodBeginThis = (periodBeginFull < GapsBegin[i]) ? GapsBegin[i] : periodBeginFull;
               DateTime periodEndThis = (periodEndFull > GapsEnd[i]) ? GapsEnd[i] : periodEndFull;
               if ((periodEndThis - periodBeginThis).TotalHours > bestLengthHour)
               {
                  periodBegin = periodBeginThis;
                  periodEnd = periodEndThis;
                  bestLengthHour = (periodEndThis - periodBeginThis).TotalHours;
               }
            }

            if (bestLengthHour < 0.0)
            {
               periodEnd = periodBeginFull;
               periodBegin = periodEndFull;
            }
         }
      }

      public void TimeAreaHotspot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
            return;

         DateTime dayBeginUtc = new DateTime(Parent.DailyDay.Year, Parent.DailyDay.Month, Parent.DailyDay.Day, 0, 0, 0).ToUniversalTime();

         GapsBegin = new();
         GapsEnd = new();
         List<TimePeriod> timePeriods = TimeManager.Instance.QueryTimePeriod(dayBeginUtc, dayBeginUtc.AddDays(1));
         timePeriods.OrderBy(p => p.StartTime);
         if (timePeriods.Count > 0)
         {
            // The period from day begin until the first start time is always a gap.
            GapsBegin.Add(dayBeginUtc);
            GapsEnd.Add(timePeriods[0].StartTime);

            DateTime lastEnd = timePeriods[0].EndTime;
            for (int i = 1; i < timePeriods.Count; i++)
            {
               // Once we find a time period that begins after the greatest end up till now,
               //  this is a gap from that end until this begin!
               if (timePeriods[i].StartTime > lastEnd)
               {
                  GapsBegin.Add(lastEnd);
                  GapsEnd.Add(timePeriods[i].StartTime);
               }

               // Keep track of the greatest end up till now.
               if (timePeriods[i].EndTime > lastEnd)
                  lastEnd = timePeriods[i].EndTime;
            }

            // The period from the last end time until the day end is also a gap.
            GapsBegin.Add(lastEnd);
            GapsEnd.Add(dayBeginUtc.AddDays(1));
         }

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

         double pct1 = DailyRow_Pct1.Value;
         Point pos = e.GetPosition(Parent.TimeAreaHotspot);
         double pct2 = pos.X / Parent.TimeAreaHotspot.RenderSize.Width;
         GetDragTime(pct1, pct2, out DateTime periodBegin, out DateTime periodEnd);

         DailyRow_Pct1 = null;
         GapsBegin = null;
         GapsEnd = null;
         TimeAreaHotspot_MouseMove(sender, e);

         if ((periodEnd - periodBegin).TotalMinutes < 1.0)
            return;

         bool isSchedule = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
         Parent.TimekeepTextEditor.Show((text) =>
         {
            if (isSchedule)
               TimeManager.Instance.AddTimeEvent(new() { Description = text, StartTime = periodBegin.ToLocalTime(), EndTime = periodEnd.ToLocalTime() });
            else
            {
               int projectId = TimeManager.Instance.AddProject(text);
               TimeManager.Instance.AddHistoryLine(new TimePeriod() { DbId = projectId, Ident = text, StartTime = periodBegin, EndTime = periodEnd, OnComputer = false });
            }

            Refresh();
         }, TimeManager.Instance.ProjectIdentAutoSuggest);
      }

      #endregion
   }
}
