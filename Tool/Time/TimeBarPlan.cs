using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Tildetool.Time.Serialization;

namespace Tildetool.Time
{
   public class TimeBarPlan : TimeBar
   {
      public TimeBarPlan(Timekeep parent) : base(parent) { }

      public override void SubRefresh()
      {
         Parent.DailyDate.Text = "Schedule";
      }

      public override List<TimeBlockRow> CollectTimeBlocks()
      {
         List<TimeBlockRow> projectPeriods = new();

         List<List<TimeBlock>> scheduleRows = GetWeeklySchedule();
         for (int i = 0; i < scheduleRows.Count; i++)
         {
            DateTime dayBegin = WeekBegin.AddDays(i);
            int dayCompare = dayBegin.Date.CompareTo(DateTime.Now.Date);
            bool showNowLineRow = dayBegin.ToUniversalTime() <= DateTime.UtcNow;
            double totalMinutes = scheduleRows[i].Sum(p => (p.EndTime - p.StartTime).TotalMinutes);

            foreach (TimeBlock block in scheduleRows[i])
            {
               if (dayCompare < 0)
               {
                  block.ColorGrid = new SolidColorBrush(block.Color.Lerp(Extension.FromArgb(0xFF202020), 0.8f));
                  block.ColorBack = new SolidColorBrush(Extension.FromArgb(0xFF808080));
               }
               else
               {
                  block.ColorGrid = new SolidColorBrush(block.Color);
                  block.ColorBack = block.CurStyle == TimeBlock.Style.TimePeriod && block.Project == null
                     ? new SolidColorBrush(Extension.FromArgb(0xFF69A582))
                     : new SolidColorBrush(block.Color.Lerp(Extension.FromArgb(0xFFC3F1AF), 0.75f));
               }
               block.CellTitle = block.Name;
            }

            projectPeriods.Add(new TimeBlockRow()
            {
               Blocks = scheduleRows[i],
               RowName = dayBegin.ToString("ddd"),
               Day = new DateOnly(dayBegin.Year, dayBegin.Month, dayBegin.Day),
               TotalMinutes = totalMinutes,

               IsHighlight = dayCompare == 0,
               IsGray = dayCompare < 0,

               ShowNowLine = showNowLineRow,
               HasDate = true
            });
         }

         return projectPeriods;
      }
   }
}
