using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Tildetool.Time.Serialization;
using Tildetool.WPF;

namespace Tildetool.Time
{
    public class IndicatorGraphPane
    {
      public Timekeep Parent;
      public IndicatorGraphPane(Timekeep parent)
      {
         Parent = parent;
      }

      class IndicatorGraph : DataTemplater
      {
         public Grid Rows;
         public Path IndicatorGraphLine;
         public IndicatorGraph(FrameworkElement root) : base(root) { }
      }
      class IndicatorRow : DataTemplater
      {
         public Grid RowLine;
         public TextBlock Title;
         public IndicatorRow(FrameworkElement root) : base(root) { }
      }
      class IndicatorWorkRow : DataTemplater
      {
         public Grid RowLine;
         public IndicatorWorkRow(FrameworkElement root) : base(root) { }
      }

      public void Refresh(DateTime day)
      {
         //
         Parent.IndicatorGraphGrid.Visibility = (Parent.CurDailyMode == Timekeep.DailyMode.Indicators) ? Visibility.Visible : Visibility.Collapsed;
         if (Parent.CurDailyMode != Timekeep.DailyMode.Indicators)
            return;

         const int periodCount = 10;
         DateTime dayBeginLocal = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0);
         DateTime weekBeginLocal = dayBeginLocal.AddDays(-(int)day.DayOfWeek).ToUniversalTime();
         DateTime periodEndLocal = weekBeginLocal.AddDays(7);
         DateTime periodBeginLocal = periodEndLocal.AddDays(-7 * periodCount);
         DateTime periodBegin = periodBeginLocal.ToUniversalTime();
         DateTime periodEnd = periodEndLocal.ToUniversalTime();

         Project[] workProject = TimeManager.Instance.Data.Where(p => p.ShowOnIndicator).ToArray();
         List<TimePeriod> workPeriods;
         if (workProject.Length > 0)
            workPeriods = TimeManager.Instance.QueryTimePeriod(workProject[0], periodBegin, periodEnd);
         else
            workPeriods = new();

         SolidColorBrush[] WeekBrush = new SolidColorBrush[10];
         void _setGraph(TextBlock uiText, TextBlock uiTime, int weekNumber)
         {
            DateTime weekBegin = periodBegin.AddDays(7 * weekNumber);
            uiText.Text = weekBegin.ToString("yy/MM/dd");

            double sumHours = workPeriods.Where(p => p.EndTime >= weekBegin && p.StartTime < weekBegin.AddDays(7))
               .Select(p => (p.EndTime - p.StartTime).TotalHours).DefaultIfEmpty(0.0).Sum();
            string hourString = $"{Math.Round(sumHours)}h";
            uiTime.Text = hourString;

            if (sumHours < 26.0)
               WeekBrush[weekNumber] = new SolidColorBrush(Extension.FromArgb(0x90D60000));
            else if (sumHours < 36.0)
               WeekBrush[weekNumber] = new SolidColorBrush(Extension.FromArgb(0xA0E4C320));
            else if (sumHours < 44.0)
               WeekBrush[weekNumber] = new SolidColorBrush(Extension.FromArgb(0xFF44DE28));
            else if (sumHours < 50.0)
               WeekBrush[weekNumber] = new SolidColorBrush(Extension.FromArgb(0xFF2358FF));
            else
               WeekBrush[weekNumber] = new SolidColorBrush(Extension.FromArgb(0xFF7a06bd));
            uiText.Foreground = WeekBrush[weekNumber];
            uiTime.Foreground = WeekBrush[weekNumber];
         }
         _setGraph(Parent.GraphText1, Parent.GraphTime1, 0);
         _setGraph(Parent.GraphText2, Parent.GraphTime2, 1);
         _setGraph(Parent.GraphText3, Parent.GraphTime3, 2);
         _setGraph(Parent.GraphText4, Parent.GraphTime4, 3);
         _setGraph(Parent.GraphText5, Parent.GraphTime5, 4);
         _setGraph(Parent.GraphText6, Parent.GraphTime6, 5);
         _setGraph(Parent.GraphText7, Parent.GraphTime7, 6);
         _setGraph(Parent.GraphText8, Parent.GraphTime8, 7);
         _setGraph(Parent.GraphText9, Parent.GraphTime9, 8);
         _setGraph(Parent.GraphText10, Parent.GraphTime10, 9);

         Parent.IndicatorWork.Visibility = workProject.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
         if (workProject.Length > 0)
         {
            DataTemplate? templateWorkRow = Parent.Resources["IndicatorWorkRow"] as DataTemplate;
            DataTemplater.Populate(Parent.IndicatorWork, templateWorkRow, workPeriods, (content, root, i, data) =>
            {
               IndicatorWorkRow row = new(root);

               double pctBegin = (data.StartTime.ToLocalTime() - periodBeginLocal).TotalHours / ((double)periodCount * 7.0 * 24.0);
               double pctEnd = (data.EndTime.ToLocalTime() - periodBeginLocal).TotalHours / ((double)periodCount * 7.0 * 24.0);
               FreeGrid.SetLeft(content, new PercentValue(PercentValue.ModeType.Percent, pctBegin));
               FreeGrid.SetWidth(content, new PercentValue(PercentValue.ModeType.Percent, pctEnd - pctBegin));

               int weekNumber = (int)Math.Floor((data.StartTime.ToLocalTime() - periodBeginLocal).TotalDays / 7.0);
               weekNumber = Math.Clamp(weekNumber, 0, WeekBrush.Length);
               row.RowLine.Background = WeekBrush[weekNumber];
            });
         }

         DataTemplate? templateGraph = Parent.Resources["IndicatorGraph"] as DataTemplate;
         DataTemplate? templateRow = Parent.Resources["IndicatorRow"] as DataTemplate;
         DataTemplater.Populate(Parent.IndicatorGraphs, templateGraph, TimeManager.Instance.Indicators, (content, root, i, data) =>
         {
            const double dayLength = 24.0 / ((double)periodCount * 7.0 * 24.0);

            IndicatorGraph pane = new IndicatorGraph(root);

            List<double> points = new List<double>();
            List<int> values = new List<int>();
            List<Color> colors = new List<Color>();
            var indicators = TimeManager.Instance.QueryTimeIndicator(periodBegin, periodEnd);
            foreach (TimeIndicator entry in indicators)
            {
               if (data.Name.CompareTo(entry.Category) != 0)
                  continue;
               IndicatorValue value = TimeManager.Instance.GetIndicatorValue(entry.Category, entry.Value);
               DateTime thisTime = entry.Time.ToLocalTime();
               DateTime thisDayBegin = new DateTime(thisTime.Year, thisTime.Month, thisTime.Day, 0, 0, 0);
               double pct = (thisTime - periodBeginLocal).TotalHours / ((double)periodCount * 7.0 * 24.0);
               points.Add(pct);
               values.Add(entry.Value);
               colors.Add(value.GetColorBack());
            }

            TimeManager.Instance.QueryAdjacentTimeIndicators(data.Name, periodBegin, periodEnd, out int prevValue, out int nextValue);
            if (points.Count == 0 || points[0] > 0.001)
            {
               points.Insert(0, 0.0);
               if (prevValue != int.MinValue)
               {
                  IndicatorValue value = TimeManager.Instance.GetIndicatorValue(data.Name, prevValue);
                  values.Insert(0, prevValue);
                  bool same = points.Count >= 2 && values[0] == values[1];
                  colors.Insert(0, value.GetColorBack(0xFF));
               }
               else if (values.Count > 0)
               {
                  values.Insert(0, values[0]);
                  colors.Insert(0, new Color() { R = colors[0].R, G = colors[0].G, B = colors[0].B, A = 0 });
               }
               else
               {
                  IndicatorValue value = TimeManager.Instance.GetIndicatorValue(data.Name, 0);
                  values.Add(0);
                  colors.Add(value.GetColorBack(0x00));
               }
            }
            if (points[^1] < 0.999)
            {
               if (nextValue != int.MinValue)
               {
                  IndicatorValue value = TimeManager.Instance.GetIndicatorValue(data.Name, nextValue);
                  points.Add(1.0);
                  values.Add(nextValue);
                  bool same = values.Count >= 2 && (int)values[^1] == (int)values[^2];
                  colors.Add(value.GetColorBack(0xFF));
               }
               else
               {
                  points.Add(Math.Min(1.0, points[^1] + (0.2 * dayLength)));
                  values.Add(values[^1]);
                  colors.Add(new Color() { R = colors[^1].R, G = colors[^1].G, B = colors[^1].B, A = 0 });
                  points.Add(1.0);
                  values.Add(values[^1]);
                  colors.Add(colors[^1]);
               }
            }

            for (int o = 1; o < values.Count; o++)
               if (points[o] - points[o - 1] >= dayLength)
               {
                  points.Insert(o, points[o] - (0.2 * dayLength));
                  colors.Insert(o, colors[o - 1]);
                  values.Insert(o, values[o - 1]);
               }

            PathGeometry geometry = new PathGeometry();
            PathFigure figure = new PathFigure() { IsClosed = false };
            figure.StartPoint = new Point(points[0], -20.0 * (values[0] + data.Offset));
            for (int o = 1; o < points.Count; o++)
               figure.Segments.Add(new LineSegment(new Point(points[o], -20.0 * (values[o] + data.Offset)), true));
            geometry.Figures.Add(figure);
            pane.IndicatorGraphLine.Data = geometry;

            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0.0, 0.5);
            brush.EndPoint = new Point(1.0, 0.5);
            for (int o = 0; o < points.Count; o++)
               brush.GradientStops.Add(new GradientStop(colors[o], points[o]));
            pane.IndicatorGraphLine.Stroke = brush;

            List<int> valuesSet = values.Distinct().ToList();
            valuesSet.Sort();
            int minValue = valuesSet.Min();
            int maxValue = valuesSet.Max();
            DataTemplater.Populate(pane.Rows, templateRow, valuesSet, (subcontent, subroot, o, index) =>
            {
               IndicatorRow row = new IndicatorRow(subroot);
               IndicatorValue subdata = data.Values[index - data.MinValue];
               double pct = (maxValue > minValue) ? 1.0 - ((double)(index - minValue) / (double)(maxValue - minValue))
                  : 0.5;
               FreeGrid.SetTop(row.RowLine, new PercentValue(PercentValue.ModeType.Percent, pct - 0.15));
               FreeGrid.SetTop(row.Title, new PercentValue(PercentValue.ModeType.Percent, pct - 0.15));
               row.RowLine.Background = new SolidColorBrush(subdata.GetColorBack(0x20));
               row.Title.Foreground = new SolidColorBrush(subdata.GetColorBack(0x60));
               row.Title.Text = subdata.Name;
            });
         });
      }
   }
}
