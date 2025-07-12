using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tildetool.Time.Serialization;
using Tildetool.WPF;

namespace Tildetool.Time
{
   public class SummaryPane
   {
      public Timekeep Parent;
      public SummaryPane(Timekeep parent)
      {
         Parent = parent;
      }

      class SummaryRow : DataTemplater
      {
         public TextBlock SummaryDate;
         public FreeGrid RowT;
         public FreeGrid RowB;
         public SummaryRow(FrameworkElement root) : base(root) { }
      }
      class SummaryBlockT : DataTemplater
      {
         public Grid Container;
         public Grid Pane;
         public TextBlock Name;
         public TextBlock TimeH;
         public TextBlock TimeM;
         public SummaryBlockT(FrameworkElement root) : base(root) { }
      }
      class SummaryBlockB : DataTemplater
      {
         public Grid Container;
         public Grid Pane;
         public TextBlock Name;
         public TextBlock TimeH;
         public TextBlock TimeM;
         public SummaryBlockB(FrameworkElement root) : base(root) { }
      }

      Dictionary<FrameworkElement, string> CategoryByRoot = new();
      string FocusCategory = null;

      public void Refresh(DateTime day)
      {
         //
         Parent.Summary.Visibility = (Parent.CurDailyMode == Timekeep.DailyMode.Summary) ? Visibility.Visible : Visibility.Collapsed;
         if (Parent.CurDailyMode != Timekeep.DailyMode.Summary)
            return;

         // Make sure our project data is up-to-date.
         TimeManager.Instance.UpdateProjectData();

         // Determine the period of time we'll cover.
         const int periodCount = 4;
         DateTime dayBeginLocal = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0);
         DateTime weekBeginLocal = dayBeginLocal.AddDays(-(int)day.DayOfWeek).ToUniversalTime();
         DateTime periodEndLocal = weekBeginLocal.AddDays(7);
         DateTime periodBeginLocal = periodEndLocal.AddDays(-7 * periodCount);
         DateTime periodBegin = periodBeginLocal.ToUniversalTime();
         DateTime periodEnd = periodEndLocal.ToUniversalTime();

         // Query for all the periods within that range, and group by week.
         List<TimePeriod> periods = TimeManager.Instance.QueryTimePeriod(periodBegin, periodEnd);
         List<TimePeriod>[] periodsByWeek = Enumerable.Range(0, periodCount).Select(i => new List<TimePeriod>()).ToArray();
         foreach (var period in periods)
         {
            if (period.StartTime.ToLocalTime().DayOfWeek == DayOfWeek.Sunday || period.EndTime.ToLocalTime().DayOfWeek == DayOfWeek.Sunday)
               continue;
            int week = Math.Max(0, (int)Math.Floor((period.StartTime - periodBegin).TotalDays / 7.0));
            periodsByWeek[week].Add(period);
         }

         //
         bool periodIsFocus(TimePeriod period)
         {
            string category = period.Ident;
            while (TimeManager.Instance.ProjectIdentToCategory.TryGetValue(category, out string parentCategory) && !string.IsNullOrEmpty(parentCategory))
               category = parentCategory;
            return FocusCategory.CompareTo(category) == 0;
         }
         double maxHours = 0.0;
         if (!string.IsNullOrEmpty(FocusCategory))
            maxHours = periodsByWeek.Select(ps => ps.Where(periodIsFocus).Sum(period => (period.EndTime - period.StartTime).TotalHours)).Max();

         // We'll create one row per week.
         CategoryByRoot.Clear();
         DataTemplate? tmplSummaryRow = Parent.Resources["SummaryRow"] as DataTemplate;
         DataTemplater.Populate(Parent.SummaryRows, tmplSummaryRow, periodsByWeek.Reverse(), (SummaryRow ui, int i, List<TimePeriod> data) =>
         {
            i = periodsByWeek.Length - 1 - i;
            DateTime thisPeriodBeginLocal = periodBeginLocal.AddDays((i * 7) + 1);
            DateTime thisPeriodEndLocal = thisPeriodBeginLocal.AddDays(6);
            thisPeriodEndLocal = thisPeriodEndLocal < DateTime.Now ? thisPeriodEndLocal : DateTime.Now;
            ui.SummaryDate.Text = $"{periodBeginLocal.AddDays((i * 7) + 1).ToString("yy/MM/dd")} to {thisPeriodEndLocal.ToString("yy/MM/dd")}";
            double totalHours = (thisPeriodEndLocal - thisPeriodBeginLocal).TotalHours;

            // If we have a focus category, we filter down to projects that are nested in it.
            if (!string.IsNullOrEmpty(FocusCategory))
            {
               // Remove unless we're that category or a subcategory of it.
               data = data.Where(periodIsFocus).ToList();

               // Resize our total time to match.
               totalHours = data.Sum(period => (period.EndTime - period.StartTime).TotalHours);
            }

            // Sum the duration by project and by category.
            Dictionary<string, double> hourByProject = new();
            Dictionary<string, double> hourByCategory = new();
            Dictionary<string, int> subprojectByCategory = new();
            HashSet<string> already = new();
            foreach (TimePeriod period in data)
            {
               double durationH = (period.EndTime - period.StartTime).TotalHours;
               hourByProject[period.Ident] = hourByProject.GetValueOrDefault(period.Ident, 0.0f) + durationH;

               string category = period.Ident;
               while (TimeManager.Instance.ProjectIdentToCategory.TryGetValue(category, out string parentCategory) && !string.IsNullOrEmpty(parentCategory))
                  category = parentCategory;
               hourByCategory[category] = hourByCategory.GetValueOrDefault(category, 0.0f) + durationH;

               if (already.Add(period.Ident))
                  subprojectByCategory[category] = subprojectByCategory.GetValueOrDefault(category, 0) + 1;
            }

            if (string.IsNullOrEmpty(FocusCategory))
            {
               // We count leftover time as belonging to night.
               // TODO: use QueryDayPeriod once implemented.
               double leftoverHours = totalHours - hourByCategory.Values.Sum();
               if (TimeManager.Instance.ProjectIdentToOrder.ContainsKey("Sleep"))
               {
                  hourByCategory["Sleep"] = hourByCategory.GetValueOrDefault("Sleep") + leftoverHours;
                  subprojectByCategory["Sleep"] = subprojectByCategory.GetValueOrDefault("Sleep", 0) + 1;
               }
            }

            // Now assemble the list of categories.
            List<string> categories = hourByCategory.Keys.ToList();
            categories.Sort((a, b) => TimeManager.Instance.ProjectIdentToOrder[a].CompareTo(TimeManager.Instance.ProjectIdentToOrder[b]));

            // Calculate the base percentage width for each.
            const double minWidth = 0.005;
            Dictionary<string, double> pctByCategory = new();
            foreach (var kv in hourByCategory)
               pctByCategory[kv.Key] = Math.Max(minWidth, kv.Value / totalHours);

            if (pctByCategory.ContainsKey("Sleep"))
               pctByCategory["Sleep"] = 0.025;

            // Compute total widths.
            const double categoryPad = 0.01;
            int categoryGapCount = categories.Count - 1;
            double totalPadPct = categoryPad * categoryGapCount;
            double totalPct = pctByCategory.Values.Sum();
            double maxPct = 1.0;
            if (!string.IsNullOrEmpty(FocusCategory))
               maxPct = totalHours / maxHours;
            double pctFactor = (maxPct - totalPadPct) / totalPct;

            // Populate top rows for categories.
            double beginPct = 0.0;
            Dictionary<string, double> categoryMinW = new();
            Dictionary<string, double> categoryW = new();
            DataTemplate? tmplSummaryBlockT = Parent.Resources["SummaryBlockT"] as DataTemplate;
            DataTemplater.Populate(ui.RowT, tmplSummaryBlockT, categories, (SummaryBlockT subui, int o, string category) =>
            {
               CategoryByRoot[subui.Container] = category;

               if (TimeManager.Instance.IdentToProject.TryGetValue(category, out Project project))
                  subui.Name.Text = project.Name;
               else
                  subui.Name.Text = category;
               double hours = hourByCategory[category];
               double pctWidth = pctFactor * pctByCategory[category];

               int min = (int)Math.Round(hours * 60.0);
               int minPerDay = (int)Math.Round(hours * 60.0 * 24.0 / totalHours);
               subui.TimeH.Text = $"{min / 60}";
               if (minPerDay / 60 > 0)
                  subui.TimeM.Text = $"{min % 60:D2} {minPerDay / 60}h{minPerDay % 60:D2}m";
               else
                  subui.TimeM.Text = $"{min % 60:D2} {minPerDay % 60:D2}m";

               double pctBegin = beginPct;
               if (!string.IsNullOrEmpty(category))
               {
                  categoryMinW[category] = beginPct;
                  categoryW[category] = pctWidth;
               }
               double pctEnd = pctBegin + pctWidth;
               FreeGrid.SetLeft(subui.Content, new PercentValue(PercentValue.ModeType.Percent, pctBegin));
               FreeGrid.SetWidth(subui.Content, new PercentValue(PercentValue.ModeType.Percent, pctEnd - pctBegin));

               beginPct += pctWidth + categoryPad;
            });

            // Populate bottom rows for individual projects.
            double maxHeight = 66.0;
            List<string> projects = hourByProject.Keys.ToList();
            projects.Sort((a, b) => -hourByProject[a].CompareTo(hourByProject[b]));
            DataTemplate? tmplSummaryBlockB = Parent.Resources["SummaryBlockB"] as DataTemplate;
            DataTemplater.Populate(ui.RowB, tmplSummaryBlockB, projects, (SummaryBlockB subui, int o, string ident) =>
            {
               string category = ident;
               while (TimeManager.Instance.ProjectIdentToCategory.TryGetValue(category, out string parentCategory) && !string.IsNullOrEmpty(parentCategory))
                  category = parentCategory;

               subui.Root.VerticalAlignment = VerticalAlignment.Top;

               double hours = hourByProject[ident];
               if (hours <= 0.25 && string.IsNullOrEmpty(FocusCategory))
                  subui.Name.Text = "";
               else if (TimeManager.Instance.IdentToProject.TryGetValue(ident, out Project project))
                  subui.Name.Text = project.Name;
               else
                  subui.Name.Text = ident;
               subui.Name.FontSize = string.IsNullOrEmpty(FocusCategory) ? 8 : 12;

               subui.Name.UpdateLayout();
               maxHeight = Math.Max(maxHeight, subui.Name.ActualHeight);
               subui.Container.Height = Math.Max(66, subui.Name.ActualWidth * Math.Sin(60.0 * Math.PI / 180.0));

               int min = (int)Math.Round(hours * 60.0);
               subui.TimeH.Visibility = (min >= 60) ? Visibility.Visible : Visibility.Collapsed;
               subui.TimeH.Text = $"{min / 60}";
               subui.TimeM.Text = $"{min % 60:D2}";

               const double projectPad = 0.0015;
               double catTotalW = categoryW[category];
               double totalPad = (projectPad * (subprojectByCategory[category] - 1));
               double catWforSub = catTotalW - totalPad;

               double pctBegin = categoryMinW[category];
               double catH = hourByCategory[category];
               double pctWidth = catWforSub * (hours / catH);
               FreeGrid.SetLeft(subui.Content, new PercentValue(PercentValue.ModeType.Percent, pctBegin));
               FreeGrid.SetWidth(subui.Content, new PercentValue(PercentValue.ModeType.Percent, pctWidth));

               categoryMinW[category] = pctBegin + pctWidth + projectPad;
            });
         });
      }

      public void SummaryBlockT_MouseEnter(object sender, MouseEventArgs e)
      {
         if (sender is FrameworkElement element)
            if (CategoryByRoot.TryGetValue(element, out FocusCategory))
               Refresh(Parent.DailyDay);
      }

      public void SummaryBlockT_MouseLeave(object sender, MouseEventArgs e)
      {
         if (sender is FrameworkElement element)
            if (CategoryByRoot.TryGetValue(element, out string focusCategory))
               if (FocusCategory == focusCategory)
               {
                  FocusCategory = null;
                  Refresh(Parent.DailyDay);
               }
      }
   }
}
