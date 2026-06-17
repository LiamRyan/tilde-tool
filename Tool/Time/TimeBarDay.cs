using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            (Color colorGrid, Color colorBack, Color colorFore) =
               block.Project != null ? ((RGB)0x143528, (RGB)0x449677, (RGB)0xC3F1DF)
               : block.OnComputer ? ((RGB)0x143518, (RGB)0x449637, (RGB)0xC3F1AF)
               : ((RGB)0x0D211D, (RGB)0x517F65, (RGB)0x69A582);
            block.ColorGrid = new(colorGrid);
            block.ColorBack = new(colorBack);
            block.ColorFore = new(colorFore);

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

         double totalHours = MaxHour - MinHour;
         DataTemplate? templateSchedule = Parent.Resources["ScheduleEntry"] as DataTemplate;
         DataTemplater.Populate<TimeBlock, ScheduleEntry>(Parent.ScheduleGrid, templateSchedule, blocks, (ui, index, block) =>
         {
            DateTime startTime = block.StartTime > dayBeginUtc ? block.StartTime : dayBeginUtc;
            DateTime endTime = block.EndTime < dayEndUtc ? block.EndTime : dayEndUtc;
            double hourBegin = (startTime - dayBeginUtc).TotalHours;
            double hourEnd = (endTime - dayBeginUtc).TotalHours;
            if (hourEnd > hourBegin)
            {
               FreeGrid.SetLeft(ui.Content, new PercentValue(PercentValue.ModeType.Percent, (hourBegin - MinHour) / totalHours));
               FreeGrid.SetWidth(ui.Content, new PercentValue(PercentValue.ModeType.Percent, (hourEnd - hourBegin) / totalHours));
            }

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
         void drawIndicator(ContentControl content, FrameworkElement root, int index, TimeIndicator entry)
         {
            double pct = ((entry.Time.ToLocalTime() - todayLocalS).TotalHours - MinHour) / (MaxHour - MinHour);
            StackPanelShift.SetAlong(content, pct);

            IndicatorCtrl ctrl = new IndicatorCtrl(root);
            TimeManager.Instance.TryGetIndicatorValue(entry.Category, entry.Value, out Indicator? indicator, out IndicatorValue? value);
            (root as Panel).Background = new SolidColorBrush(indicator.GetColorBack(entry.Value, 0x58));
            ctrl.Icon.Foreground = new SolidColorBrush(indicator.GetColorFore(entry.Value));
            ctrl.Text.Foreground = new SolidColorBrush(indicator.GetColorBack(entry.Value));
            ctrl.Icon.Text = value != null ? value.Icon : $"?";

            bool showFull = (Parent.IndicatorBar.FocusCategory != null && string.Compare(entry.Category, Parent.IndicatorBar.FocusCategory.Name) == 0) || already.Add(value);
            ctrl.Text.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
            if (showFull)
               ctrl.Text.Text = value != null ? value.Name : $"?{entry.Category}?";
         }

         Parent.FocusIndicators.Visibility = Parent.IndicatorBar.FocusCategory != null ? Visibility.Visible : Visibility.Collapsed;
         if (Parent.IndicatorBar.FocusCategory != null)
         {
            DataTemplater.Populate(Parent.FocusIndicators, templateIndicator, indicators.Where(i => string.Compare(i.Category, Parent.IndicatorBar.FocusCategory.Name) == 0), drawIndicator);
            DataTemplater.Populate(Parent.Indicators, templateIndicator, indicators.Where(i => string.Compare(i.Category, Parent.IndicatorBar.FocusCategory.Name) != 0), drawIndicator);
         }
         else
            DataTemplater.Populate(Parent.Indicators, templateIndicator, indicators, drawIndicator);
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

      #region Keyboard input

      public override bool HandleKeyDown(object sender, KeyEventArgs e)
      {
         if (e.Key == Key.OemOpenBrackets || e.Key == Key.OemCloseBrackets)
         {
            if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
               return false;

            DateTime dayBeginUtc = new DateTime(Parent.DailyDay.Year, Parent.DailyDay.Month, Parent.DailyDay.Day, 0, 0, 0).ToUniversalTime();
            FindGaps();
            int gap = GapsBegin.FindLastIndex(g => g < TimeManager.Instance.CurrentStartTime.AddSeconds(-30));

            List<TimePeriod> timePeriods = TimeManager.Instance.QueryTimePeriod(dayBeginUtc, dayBeginUtc.AddDays(1));
            timePeriods.OrderBy(p => p.StartTime);

            DateTime origin;
            bool wasCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool isToLeft = e.Key == Key.OemOpenBrackets;
            if (wasCtrl && isToLeft) // carve to-left: origin is NOW
               origin = DateTime.UtcNow;
            else if (wasCtrl && !isToLeft) // carve to-right: origin is END of the most recent gap
               origin = gap == -1 ? TimeManager.Instance.CurrentStartTime : GapsEnd[gap];
            else if ((wasCtrl && !isToLeft) || isToLeft) // to-left: origin is END of the most recent gap
               origin = gap == -1 ? TimeManager.Instance.CurrentStartTime : GapsEnd[gap];
            else // to-right: origin is START of most recent gap
               origin = gap == -1 ? TimeManager.Instance.CurrentStartTime : GapsBegin[gap];

            if (origin > TimeManager.Instance.CurrentStartTime)
               origin = TimeManager.Instance.CurrentStartTime;

            DailyRow_Pct1 = ((origin - dayBeginUtc).TotalHours - Parent.TimeBar.MinHour) / (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour);

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
               DateTime finis;
               if (isToLeft)
                  finis = GapsBegin.LastOrDefault(g => g < origin, TimeManager.Instance.CurrentStartTime);
               else
                  finis = GapsEnd.LastOrDefault(g => g > origin && g <= TimeManager.Instance.CurrentStartTime.AddSeconds(30), TimeManager.Instance.CurrentStartTime);
               DailyRow_Pct2 = (finis - origin).TotalHours / (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour);

               Smooth = false;
               TimeAreaHotspot_MouseMove(null, null);
               TimeAreaHotspot_MouseLeftButtonUp(null, null);
               Smooth = true;
            }
            else
            {
               DailyRow_Pct2 = 0.0;

               Smooth = false;
               TimeAreaHotspot_MouseMove(null, null);
               Smooth = true;

               Parent.TimekeepTextEditor.Show("how long", (text, note) =>
               {
                  string[] texts = text.Split(' ');
                  if (texts.Length > 2)
                  {
                     DailyRow_Pct1 = null;
                     TimeAreaHotspot_MouseLeftButtonUp(null, null);
                     return;
                  }
                  int hours = 0;
                  if (texts.Length == 2 && !int.TryParse(texts[0], out hours))
                  {
                     DailyRow_Pct1 = null;
                     TimeAreaHotspot_MouseLeftButtonUp(null, null);
                     return;
                  }
                  if (!int.TryParse(texts[^1], out int value))
                  {
                     DailyRow_Pct1 = null;
                     TimeAreaHotspot_MouseLeftButtonUp(null, null);
                     return;
                  }
                  value += 100 * 60 * hours;

                  TimeSpan interval = value >= 100 ? new TimeSpan(0, value / 100, value % 100) : new TimeSpan(0, value, 0);
                  // ctrl means carve
                  if (isToLeft)
                     DailyRow_Pct2 = -interval.TotalHours / (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour);
                  else
                     DailyRow_Pct2 = interval.TotalHours / (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour);

                  Smooth = false;
                  TimeAreaHotspot_MouseMove(null, null);
                  Carve = wasCtrl;
                  TimeAreaHotspot_MouseLeftButtonUp(null, null);
                  Smooth = true;
               },
               () =>
               {
                  DailyRow_Pct1 = null;
                  TimeAreaHotspot_MouseLeftButtonUp(null, null);
               });
            }
            return true;
         }
         return false;
      }

      #endregion
   }
}
