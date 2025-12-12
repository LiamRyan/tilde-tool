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
         bool showNowLine = DayBegin <= DateTime.Now && DateTime.Now < DayBegin.AddDays(1);
         if (showNowLine && DateTime.Now.AddMinutes(-30).Hour < MinHour)
            MinHour = DateTime.Now.AddMinutes(-30).Hour;
         if (showNowLine && DateTime.Now.AddMinutes(30).Hour > MaxHour)
            MaxHour = DateTime.Now.AddMinutes(30).Hour;

         List<TimeBlockRow> projectPeriods = new();

         WeeklySchedule = GetWeeklySchedule();
         _organizePeriod(WeeklySchedule[(int)DayBegin.DayOfWeek]);

         Dictionary<string, int> identToIndex = new();
         Dictionary<string, int> nameToIndex = new();

         if (Parent.ProjectBar.InitialProject != null)
         {
            projectPeriods.Add(new TimeBlockRow()
            {
               Project = Parent.ProjectBar.InitialProject,
               Blocks = new(),
               RowName = Parent.ProjectBar.InitialProject.Name,
               Day = new DateOnly(DayBegin.Year, DayBegin.Month, DayBegin.Day),

               IsHighlight = true,
               IsGray = false,

               HasDate = false
            });
            identToIndex[Parent.ProjectBar.InitialProject.Ident] = 0;
         }

         DateTime todayS = new DateTime(DayBegin.Year, DayBegin.Month, DayBegin.Day, 0, 0, 0).ToUniversalTime();
         DateTime todayE = todayS.AddDays(1);
         List<TimeBlock> periods = TimeManager.Instance.QueryTimePeriod(todayS, todayE).Select(p => TimeBlock.FromTimePeriod(p)).ToList();

         foreach (TimeBlock block in periods)
         {
            int index;
            if (block.Project == null || !identToIndex.TryGetValue(block.Project.Ident, out index))
               if (!nameToIndex.TryGetValue(block.Name, out index))
               {
                  index = projectPeriods.Count;
                  projectPeriods.Add(new TimeBlockRow()
                  {
                     Project = block.Project,
                     Blocks = new(),
                     RowName = block.Project?.Name ?? block.Name,
                     Day = new DateOnly(DayBegin.Year, DayBegin.Month, DayBegin.Day),

                     IsHighlight = block.Project == Parent.ProjectBar.InitialProject,
                     IsGray = false,

                     HasDate = false
                  });
                  if (block.Project != null)
                     identToIndex[block.Project.Ident] = index;
                  else
                     nameToIndex[block.Name] = index;
               }

            block.ColorGrid = new SolidColorBrush(block.Color);
            block.ColorBack = new(Extension.FromArgb(block.OnComputer ? 0xFF449637 : 0xFF517F65));
            block.ColorFore = new(Extension.FromArgb(block.OnComputer ? 0xFFC3F1AF : 0xFF69A582));

            block.IsActiveCell = TimeManager.Instance.CurrentTimePeriod == block.DbId;

            if (block.Project == null)
               block.CellProject = block.Name;

            projectPeriods[index].Blocks.Add(block);
         }

         for (int i = 0; i < projectPeriods.Count; i++)
            projectPeriods[i].TotalMinutes = projectPeriods[i].Blocks.Sum(p => (p.EndTime - p.StartTime).TotalMinutes);
         projectPeriods.Sort((a, b) =>
         {
            bool onComputerA = a.Blocks.Any(b => b.OnComputer);
            bool onComputerB = b.Blocks.Any(b => b.OnComputer);
            if (onComputerA != onComputerB)
               return onComputerB ? 1 : -1;
            if ((a.Project != null) != (b.Project != null))
               return b.Project != null ? 1 : -1;
            //if (a.Project != null)
            return b.TotalMinutes.CompareTo(a.TotalMinutes);
            //return a.Blocks[0].StartTime.CompareTo(b.Blocks[0].StartTime);
         });

         return projectPeriods;
      }

      protected override void _RefreshRow(TimeRow ui, int index, TimeBlockRow row)
      {
         bool isOnComputer = row.Blocks.Any(b => b.OnComputer);
         ui.HeaderNameR.Visibility = isOnComputer ? Visibility.Collapsed : Visibility.Visible;
         if (!isOnComputer)
         {
            (ui.Root as Grid).Height = 17;
            ui.HeaderName.FontSize = 12;
            ui.HeaderTimeH.FontSize = 12;
            ui.HeaderTimeM.FontSize = 10;
            ui.HeaderName.Foreground = new SolidColorBrush(Extension.FromRgb(0xFF416F55));
            ui.HeaderTimeH.Foreground = new SolidColorBrush(Extension.FromRgb(0xFF80B080));
            ui.HeaderTimeM.Foreground = new SolidColorBrush(Extension.FromRgb(0xFF80B080));
         }
      }

      protected override void _RefreshCell(TimeCell subui, int subindex, TimeBlock subdata)
      {
         (subui.Root as Grid).Height = subdata.OnComputer ? 20 : 15;
      }

      class IndicatorCtrl : DataTemplater
      {
         public TextBlock Text;
         public TextBlock Icon;
         public IndicatorCtrl(FrameworkElement root) : base(root) { }
      }

      class ScheduleEntry : DataTemplater
      {
         public TextBlock ScheduleText;
         public ScheduleEntry(FrameworkElement root) : base(root) { }
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
         DateTime dayEndUtc = dayBeginUtc.AddDays(1);

         List<TimeBlock> blocks = WeeklySchedule[(int)DayBegin.DayOfWeek];

         Parent.ScheduleGrid.ColumnDefinitions.Clear();
         double lastHour = MinHour;
         for (int i = 0; i < blocks.Count; i++)
         {
            DateTime startTime = blocks[i].StartTime > dayBeginUtc ? blocks[i].StartTime : dayBeginUtc;
            DateTime endTime = blocks[i].EndTime < dayEndUtc ? blocks[i].EndTime : dayEndUtc;
            double hourBegin = (startTime - dayBeginUtc).TotalHours;
            double hourEnd = (endTime - dayBeginUtc).TotalHours;
            if (hourEnd > hourBegin)
            {
               Parent.ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(hourBegin - lastHour, 0), GridUnitType.Star) });
               Parent.ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourEnd - hourBegin, GridUnitType.Star) });
               lastHour = hourEnd;
            }
         }
         if (MaxHour > lastHour)
            Parent.ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MaxHour - lastHour, GridUnitType.Star) });

         DataTemplate? templateSchedule = Parent.Resources["ScheduleEntry"] as DataTemplate;
         DataTemplater.Populate<TimeBlock, ScheduleEntry>(Parent.ScheduleGrid, templateSchedule, blocks, (ui, index, block) =>
         {
            //FreeGrid.SetLeft(ui.Content, new PercentValue(PercentValue.ModeType.Percent, pctX));
            //FreeGrid.SetWidth(ui.Content, new PercentValue(PercentValue.ModeType.Pixel, 1));

            Grid.SetColumn(ui.Content, (index * 2) + 1);
            (ui.Root as Grid).Background = new SolidColorBrush(block.Color.Alpha(0x20));
            ui.ScheduleText.Foreground = new SolidColorBrush(block.Color.Lerp(Extension.FromArgb(0xFFC3F1AF), 0.75f));
            ui.ScheduleText.Text = block.Name;
         });
      }

      void RefreshIndicatorPanel()
      {
         Parent.IndicatorPanel.Visibility = Visibility.Visible;

         DateTime todayLocalS = new DateTime(DayBegin.Year, DayBegin.Month, DayBegin.Day, 0, 0, 0);
         DateTime todayLocalE = todayLocalS.AddDays(1);
         DateTime todayS = todayLocalS.ToUniversalTime();
         DateTime todayE = todayS.AddDays(1);
         List<TimeIndicator> indicators = TimeManager.Instance.QueryTimeIndicator(todayS, todayE).Where(entry => TimeManager.Instance.DoesIndicatorExist(entry.Category)).ToList();
         HashSet<IndicatorValue> already = new HashSet<IndicatorValue>();

         DataTemplate? templateIndicator = Parent.Resources["Indicator"] as DataTemplate;
         DataTemplater.Populate(Parent.Indicators, templateIndicator, indicators, (content, root, _, entry) =>
         {
            double pct = ((entry.Time.ToLocalTime() - todayLocalS).TotalHours - MinHour) / (MaxHour - MinHour);
            StackPanelShift.SetAlong(content, pct);

            IndicatorCtrl ctrl = new IndicatorCtrl(root);
            TimeManager.Instance.TryGetIndicatorValue(entry.Category, entry.Value, out Indicator? indicator, out IndicatorValue? value);
            (root as Panel).Background = new SolidColorBrush(indicator.GetColorBack(entry.Value, 0x58));
            ctrl.Icon.Foreground = new SolidColorBrush(indicator.GetColorFore(entry.Value));
            ctrl.Text.Foreground = new SolidColorBrush(indicator.GetColorBack(entry.Value));
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
