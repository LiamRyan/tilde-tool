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
         public StackPanel Rows;
         public Path IndicatorGraphLine;
         public IndicatorGraph(FrameworkElement root) : base(root) { }
      }
      class IndicatorRow : DataTemplater
      {
         public Grid RowLine;
         public TextBlock TitleL;
         public TextBlock TitleR;
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
         DateTime weekBeginLocal = dayBeginLocal.AddDays(-(int)day.DayOfWeek);
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

         Indicator[] indicators = TimeManager.Instance.Indicators.Where(i => !i.Hidden).ToArray();
         DataTemplate? templateGraph = Parent.Resources["IndicatorGraph"] as DataTemplate;
         DataTemplate? templateRow = Parent.Resources["IndicatorRow"] as DataTemplate;
         DataTemplater.Populate(Parent.IndicatorGraphs, templateGraph, indicators, (content, root, i, data) =>
         {
            const double dayLength = 24.0 / ((double)periodCount * 7.0 * 24.0);
            IndicatorGraph pane = new IndicatorGraph(root);

            Indicator indicator = TimeManager.Instance.GetIndicator(data.Name);

            List<double> points = new List<double>();
            List<double> values = new List<double>();
            List<Color> colors = new List<Color>();
            var indicators = TimeManager.Instance.QueryTimeIndicator(periodBegin, periodEnd);
            foreach (TimeIndicator entry in indicators)
            {
               if (data.Name.CompareTo(entry.Category) != 0)
                  continue;
               DateTime thisTime = entry.Time.ToLocalTime();
               DateTime thisDayBegin = new DateTime(thisTime.Year, thisTime.Month, thisTime.Day, 0, 0, 0);
               double pct = (thisTime - periodBeginLocal).TotalHours / ((double)periodCount * 7.0 * 24.0);
               points.Add(pct);
               values.Add(entry.Value);
               colors.Add(TimeManager.Instance.GetIndicator(entry.Category).GetColorBack(entry.Value));
            }

            TimeManager.Instance.QueryAdjacentTimeIndicators(data.Name, periodBegin, periodEnd, out double prevValue, out double nextValue);
            if (points.Count == 0 || points[0] > 0.001)
            {
               points.Insert(0, 0.0);
               if (prevValue != double.MinValue)
               {
                  values.Insert(0, prevValue);
                  bool same = points.Count >= 2 && (int)Math.Round(values[0]) == (int)Math.Round(values[1]);
                  colors.Insert(0, indicator.GetColorBack(prevValue));
               }
               else if (values.Count > 0)
               {
                  values.Insert(0, values[0]);
                  colors.Insert(0, new Color() { R = colors[0].R, G = colors[0].G, B = colors[0].B, A = 0 });
               }
               else
               {
                  values.Add(0);
                  colors.Add(indicator.GetColorBack(0.0, 0x00));
               }
            }
            if (points[^1] < 0.999)
            {
               if (nextValue != double.MinValue)
               {
                  points.Add(1.0);
                  values.Add(nextValue);
                  bool same = values.Count >= 2 && (int)Math.Round(values[^1]) == (int)Math.Round(values[^2]);
                  colors.Add(indicator.GetColorBack(nextValue));
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
            const double lineHeight = 10.0;
            const double linePad = 2.0;
            double getLineY(double value)
            {
               int valueIndex = indicator.GetIndex(value);
               double valueSub = indicator.GetSubvalue(value);
               double lineY = (lineHeight + linePad) * valueIndex;
               double lineH = lineHeight * valueSub;
               return lineY + lineH;
            }
            figure.StartPoint = new Point(points[0], -getLineY(values[0]));
            for (int o = 1; o < points.Count; o++)
               figure.Segments.Add(new LineSegment(new Point(points[o], -getLineY(values[o])), true));
            geometry.Figures.Add(figure);
            pane.IndicatorGraphLine.Data = geometry;
            double maxValue = values.Max();
            pane.IndicatorGraphLine.Margin = new(0, 3.0 + (lineHeight * (1.0 - indicator.GetSubvalue(maxValue))), 0, 0);

            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0.0, 0.5);
            brush.EndPoint = new Point(1.0, 0.5);
            for (int o = 0; o < points.Count; o++)
               brush.GradientStops.Add(new GradientStop(colors[o], points[o]));
            pane.IndicatorGraphLine.Stroke = brush;

            List<int> valuesSet = values.Select(v => (int)Math.Round(v)).Distinct().ToList();
            valuesSet.Sort();
            valuesSet.Reverse();
            DataTemplater.Populate(pane.Rows, templateRow, valuesSet, (subcontent, subroot, o, index) =>
            {
               IndicatorRow row = new IndicatorRow(subroot);
               IndicatorValue subdata = data.Values[index - data.MinValue];
               row.RowLine.Background = new SolidColorBrush(subdata.GetColorBack(0x20));
               row.TitleL.Foreground = new SolidColorBrush(subdata.GetColorBack(0x60));
               row.TitleR.Foreground = new SolidColorBrush(subdata.GetColorBack(0x60));
               row.TitleL.Text = subdata.Name;
               row.TitleR.Text = subdata.Name;
            });
         });
      }
   }
}
