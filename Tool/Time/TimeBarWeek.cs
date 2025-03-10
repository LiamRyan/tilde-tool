using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Tildetool.Time.Serialization;

namespace Tildetool.Time
{
   public class TimeBarWeek : TimeBar
   {
      public TimeBarWeek(Timekeep parent) : base(parent) { }

      public override void SubRefresh()
      {
         Parent.DailyDate.Text = "Progress";
      }

      public override List<TimeBlockRow> CollectTimeBlocks()
      {
         List<TimeBlockRow> projectPeriods = new();

         for (int i = 0; i < 7; i++)
         {
            DateTime dayBegin = WeekBegin.AddDays(i);
            DateTime todayS = WeekBegin.AddDays(i).ToUniversalTime();
            DateTime todayE = WeekBegin.AddDays(i + 1).ToUniversalTime();
            List<TimeBlock> weekRow = TimeManager.Instance.QueryTimePeriod(todayS, todayE).Select(p => TimeBlock.FromTimePeriod(p)).ToList();

            int dayCompare = todayS.ToLocalTime().Date.CompareTo(DateTime.Now.Date);

            foreach (TimeBlock block in weekRow)
            {
               bool thisProject = block.Project == Parent.ProjectBar.DailyFocus;
               if (Parent.ProjectBar.DailyFocus != null && !thisProject)
               {
                  block.ColorGrid = new SolidColorBrush(Extension.FromArgb(0xFF383838));
                  block.ColorBack = new SolidColorBrush(Extension.FromArgb(0xFF909090));
               }
               else
               {
                  block.ColorGrid = new SolidColorBrush(block.Color);
                  block.ColorBack = block.CurStyle == TimeBlock.Style.TimePeriod && block.Project == null
                     ? new SolidColorBrush(Extension.FromArgb(0xFF69A582))
                     : new SolidColorBrush(block.Color.Lerp(Extension.FromArgb(0xFFC3F1AF), thisProject ? 1.0f : 0.75f));
               }
               block.CellTitle = block.Name;
            }

            double thisNightLengthHour = TimeManager.Instance.QueryNightLength(todayS);
            bool showNowLineRow = todayS <= DateTime.UtcNow;

            double totalMinutes = weekRow.Where(p => p.Project == Parent.ProjectBar.DailyFocus).Sum(p => (p.EndTime - p.StartTime).TotalMinutes);

            projectPeriods.Add(new TimeBlockRow()
            {
               Blocks = weekRow,
               RowName = dayBegin.ToString("ddd"),
               Day = new DateOnly(dayBegin.Year, dayBegin.Month, dayBegin.Day),
               TotalMinutes = totalMinutes,

               IsHighlight = dayCompare == 0,
               IsGray = dayCompare > 0,

               ShowNowLine = showNowLineRow,
               HasDate = true,
               NightLength = thisNightLengthHour
            });
         }

         return projectPeriods;
      }
   }
}
