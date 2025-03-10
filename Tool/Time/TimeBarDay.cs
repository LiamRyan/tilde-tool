using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tildetool.Time.Serialization;
using Tildetool.WPF;

namespace Tildetool.Time
{
   public class TimeBarDay : TimeBar
   {
      public TimeBarDay(Timekeep parent) : base(parent) { }

      List<List<TimeBlock>> WeeklySchedule;
      public override List<TimeBlockRow> CollectTimeBlocks()
      {
         List<TimeBlockRow> projectPeriods = new();

         WeeklySchedule = GetWeeklySchedule();
         _organizePeriod(WeeklySchedule[(int)DayBegin.DayOfWeek]);

         for (int i = 0; i < TimeManager.Instance.Data.Length + 1; i++)
         {
            Project? project = i < TimeManager.Instance.Data.Length ? TimeManager.Instance.Data[i] : null;
            projectPeriods.Add(new TimeBlockRow()
            {
               Blocks = new(),
               RowName = project?.Name ?? "",
               Day = new DateOnly(DayBegin.Year, DayBegin.Month, DayBegin.Day),

               IsHighlight = project == Parent.ProjectBar.InitialProject,
               IsGray = false,

               HasDate = false
            });
         }
         Dictionary<string, int> identToIndex = Enumerable.Range(0, TimeManager.Instance.Data.Length).ToDictionary(i => TimeManager.Instance.Data[i].Ident, i => i);

         DateTime todayS = new DateTime(DayBegin.Year, DayBegin.Month, DayBegin.Day, 0, 0, 0).ToUniversalTime();
         DateTime todayE = todayS.AddDays(1);
         List<TimeBlock> periods = TimeManager.Instance.QueryTimePeriod(todayS, todayE).Select(p => TimeBlock.FromTimePeriod(p)).ToList();

         foreach (TimeBlock block in periods)
         {
            int index = 0;
            if (block.Project == null || !identToIndex.TryGetValue(block.Project.Ident, out index))
               index = projectPeriods.Count - 1;

            block.ColorGrid = new SolidColorBrush(block.Color);
            block.ColorBack = new(Extension.FromArgb(block.Project != null ? 0xFF449637 : 0xFF517F65));
            block.ColorFore = new(Extension.FromArgb(block.Project != null ? 0xFFC3F1AF : 0xFF69A582));

            block.IsActiveCell = TimeManager.Instance.CurrentTimePeriod == block.DbId;
            block.PeriodMinutes = (block.EndTime - block.StartTime).TotalMinutes;

            if (index >= TimeManager.Instance.Data.Length)
               block.CellProject = block.Name;

            projectPeriods[index].Blocks.Add(block);
         }

         for (int i = 0; i < projectPeriods.Count; i++)
            projectPeriods[i].TotalMinutes = projectPeriods[i].Blocks.Sum(p => (p.EndTime - p.StartTime).TotalMinutes);

         return projectPeriods;
      }

      class IndicatorCtrl : DataTemplater
      {
         public TextBlock Text;
         public TextBlock Icon;
         public IndicatorCtrl(FrameworkElement root) : base(root) { }
      }

      public override void SubRefresh()
      {
         Parent.DailyDate.Text = DayBegin.ToString("yy/MM/dd ddd");
         RefreshNowLine();
         RefreshSchedule();
         RefreshIndicatorPanel();
         RefreshNightLength();
      }

      void RefreshNowLine()
      {
         // Show the current time.
         bool showNowLine = DayBegin <= DateTime.Now && DateTime.Now < DayBegin.AddDays(1);
         Parent.NowDividerGrid.Visibility = showNowLine ? Visibility.Visible : Visibility.Collapsed;
         if (showNowLine)
         {
            double hourProgress = Math.Min(1.0, (DateTime.Now - DayBegin.AddHours(MinHour)).TotalHours / (double)(MaxHour - MinHour));
            Parent.NowDividerGrid.ColumnDefinitions.Clear();
            Parent.NowDividerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourProgress, GridUnitType.Star) });
            Parent.NowDividerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0 - hourProgress, GridUnitType.Star) });
         }
      }

      void RefreshSchedule()
      {
         // Scheduled events
         Parent.ScheduleGrid.Visibility = Visibility.Visible;
         DateTime dayBeginUtc = DayBegin.ToUniversalTime();

         List<TimeBlock> block = WeeklySchedule[(int)DayBegin.DayOfWeek];

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

         DataTemplate? templateSchedule = Parent.Resources["ScheduleEntry"] as DataTemplate;
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

      void RefreshIndicatorPanel()
      {
         Parent.IndicatorPanel.Visibility = Visibility.Visible;

         DateTime todayLocalS = new DateTime(DayBegin.Year, DayBegin.Month, DayBegin.Day, 0, 0, 0);
         DateTime todayLocalE = todayLocalS.AddDays(1);
         DateTime todayS = todayLocalS.ToUniversalTime();
         DateTime todayE = todayS.AddDays(1);
         List<TimeIndicator> indicators = TimeManager.Instance.QueryTimeIndicator(todayS, todayE);
         HashSet<IndicatorValue> already = new HashSet<IndicatorValue>();

         DataTemplate? templateIndicator = Parent.Resources["Indicator"] as DataTemplate;
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

      void RefreshNightLength()
      {
         double nightLengthHour = TimeManager.Instance.QueryNightLength(DayBegin);
         Parent.NightLength.Visibility = nightLengthHour > 0.0 ? Visibility.Visible : Visibility.Collapsed;
         if (nightLengthHour > 0.0)
         {
            (int h, int m) = Math.DivRem((int)Math.Round(nightLengthHour * 60.0), 60);
            Parent.NightLengthH.Text = $"{h}";
            Parent.NightLengthM.Text = $"{m:D2}";
         }
      }
   }
}
