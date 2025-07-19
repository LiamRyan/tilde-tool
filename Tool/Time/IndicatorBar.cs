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
   public class IndicatorBar
   {
      public Timekeep Parent;
      public IndicatorBar(Timekeep parent)
      {
         Parent = parent;

         TimeManager.Instance.QueryLastTimeIndicators(out double[] values, out DateTime[] dates);
         IndicatorLastValue = Enumerable.Range(0, TimeManager.Instance.Indicators.Length).ToDictionary(i => TimeManager.Instance.Indicators[i], i => values[i]);
         IndicatorLastDateUtc = Enumerable.Range(0, TimeManager.Instance.Indicators.Length).ToDictionary(i => TimeManager.Instance.Indicators[i], i => dates[i]);
      }

      #region Display

      class IndicatorPane : DataTemplater
      {
         public Frame Backfill;
         public TextBlock Title;
         public TextBlock Icon;
         public TextBlock Text;
         public TextBlock Date;
         public IndicatorPane(FrameworkElement root) : base(root) { }
      }

      public void Refresh()
      {
         bool todayMode = Parent.CurDailyMode == Timekeep.DailyMode.Today;
         Parent.IndicatorPane.Visibility = todayMode ? Visibility.Visible : Visibility.Collapsed;
         if (!todayMode)
            return;

         Indicator[] indicators = FocusCategory == null ? TimeManager.Instance.Indicators : new Indicator[] { FocusCategory };

         DataTemplate? templatePane = Parent.Resources["IndicatorPane"] as DataTemplate;
         DataTemplater.Populate(Parent.IndicatorPanes, templatePane, indicators, (content, root, _, data) =>
         {
            IndicatorPane pane = new IndicatorPane(root);
            root.Height = 42;
            pane.Title.Text = data.Name;
            if (data.Name[0].ToString() == data.Hotkey)
               pane.Title.Text = $"[{data.Hotkey}]{data.Name[1..]}";
            else
               pane.Title.Text = $"[{data.Hotkey}] {data.Name}";

            double value = double.MinValue;
            if (FocusCategory == data)
               value = FocusCategoryValue;
            else if (IndicatorLastValue.TryGetValue(data, out double defaultValue))
               value = defaultValue;
            int index = data.GetIndex(value);

            pane.Backfill.Visibility = (FocusCategory == data) ? Visibility.Visible : Visibility.Collapsed;
            if (index >= 0 && index < data.Values.Length)
            {
               pane.Backfill.Background = new SolidColorBrush(data.GetColorBack(value, 0x40));
               pane.Title.Foreground = new SolidColorBrush(data.GetColorBack(value));
            }

            if (todayMode && value != double.MinValue)
            {
               bool hasIndex = index >= 0 && index < data.Values.Length;
               pane.Icon.Visibility = Visibility.Visible;
               pane.Text.Visibility = (FocusCategory == data) ? Visibility.Visible : Visibility.Collapsed;
               pane.Date.Visibility = hasIndex ? Visibility.Visible : Visibility.Collapsed;
               if (hasIndex)
               {
                  pane.Icon.Foreground = new SolidColorBrush(data.GetColorFore(value));
                  pane.Text.Foreground = new SolidColorBrush(data.GetColorBack(value));
                  pane.Icon.Text = data.Values[index].Icon;
                  pane.Text.Text = data.Values[index].Name;

                  if (IndicatorLastDateUtc.TryGetValue(data, out DateTime date) && hasIndex)
                  {
                     TimeSpan deltaF = DateTime.Now - date.ToLocalTime();
                     TimeSpan delta = DateTime.Now.Date - date.ToLocalTime().Date;
                     if (delta.TotalDays > 0.99)
                     {
                        pane.Date.Foreground = new SolidColorBrush(data.GetColorBack(value));
                        pane.Date.Text = $"{((int)Math.Round(delta.TotalDays)):D} days ago";
                     }
                     else if (deltaF.TotalHours > 0.99)
                     {
                        pane.Date.Foreground = new SolidColorBrush(data.GetColorBack(value).Lerp(Color.FromRgb(0, 0, 0), 0.5f));
                        pane.Date.Text = $"{((int)Math.Round(deltaF.TotalHours)):D} hours ago";
                     }
                     else
                        pane.Date.Visibility = Visibility.Collapsed;
                  }
                  else
                  {
                     pane.Date.Foreground = new SolidColorBrush(data.GetColorBack(value).Lerp(Color.FromRgb(0, 0, 0), 0.5f));
                     pane.Date.Text = "? ? ?";
                  }
               }
               else
               {
                  pane.Icon.Text = index.ToString();
                  pane.Text.Text = "? ? ?";
               }
            }
            else
            {
               pane.Icon.Visibility = Visibility.Collapsed;
               pane.Text.Visibility = Visibility.Collapsed;
               pane.Date.Visibility = Visibility.Collapsed;
            }
         });

         Parent.IndicatorSlider.Visibility = FocusCategory != null ? Visibility.Visible : Visibility.Collapsed;
         if (FocusCategory != null)
         {
            int index = FocusCategory.GetIndex(FocusCategoryValue);
            if (index >= 0 && index < FocusCategory.Values.Length)
            {
               Parent.SliderBackfill.Background = new SolidColorBrush(FocusCategory.GetColorBack(FocusCategoryValue, 0x40));
               Parent.SliderLine.Background = new SolidColorBrush(FocusCategory.GetColorBack(FocusCategoryValue));
               Parent.SliderPaneName.Foreground = new SolidColorBrush(FocusCategory.GetColorBack(FocusCategoryValue));
               Parent.SliderPaneName.Text = FocusCategory.Values[index].Name;
            }

            double pct = (FocusCategoryValue + FocusCategory.Offset + 0.5) / FocusCategory.Values.Length;
            FreeGrid.SetLeftPct(Parent.SliderPaneName, pct);
            FreeGrid.SetLeftPct(Parent.SliderLine, pct);

            Grid[] sliders = new[] { Parent.SliderBar1, Parent.SliderBar2, Parent.SliderBar3, Parent.SliderBar4, Parent.SliderBar5, Parent.SliderBar6, Parent.SliderBar7, Parent.SliderBar8, Parent.SliderBar9, Parent.SliderBar10, Parent.SliderBar11 };
            for (int i = 0; i < sliders.Length; i++)
               sliders[i].Visibility = i < FocusCategory.Values.Length ? Visibility.Visible : Visibility.Collapsed;
            for (int i = 0; i < FocusCategory.Values.Length; i++)
            {
               FreeGrid.SetLeftPct(sliders[i], (double)i / (double)FocusCategory.Values.Length);
               FreeGrid.SetWidthPct(sliders[i], (double)1.0 / (double)FocusCategory.Values.Length);

               double value = i - FocusCategory.Offset;
               LinearGradientBrush brush = new LinearGradientBrush();
               brush.StartPoint = new Point(0.0, 0.5);
               brush.EndPoint = new Point(1.0, 0.5);
               brush.GradientStops = new()
               {
                  new GradientStop(FocusCategory.GetColorBack(value - 0.49), 0.0),
                  new GradientStop(FocusCategory.GetColorBack(value), 0.5),
                  new GradientStop(FocusCategory.GetColorBack(value + 0.49), 1.0)
               };
               sliders[i].Background = brush;
            }
         }
      }

      #endregion
      #region Hotkey Input

      public bool HandleKeyDown(object sender, KeyEventArgs e)
      {
         if (FocusCategory != null)
         {
            switch (e.Key)
            {
               case Key.Escape:
                  ClearActive();
                  return true;

               case Key.Return:
                  PlaceIndicator(DateTime.UtcNow);
                  return true;

               case Key.Up:
                  IncValue();
                  return true;

               case Key.Down:
                  DecValue();
                  return true;
            }

            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
               double subpct = e.Key == Key.D0 ? 1.0 : (e.Key - Key.D1) / 9.0;
               FocusCategoryValue = Math.Round(FocusCategoryValue) - 0.49 + (0.98 * subpct);
               Refresh();
               return true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
               double subpct = e.Key == Key.NumPad0 ? 1.0 : (e.Key - Key.NumPad1) / 9.0;
               FocusCategoryValue = Math.Round(FocusCategoryValue) - 0.49 + (0.98 * subpct);
               Refresh();
               return true;
            }
            return false;
         }

         // Handle key entry.
         if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
         {
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
               SetActive(e.Key.ToString());
               return true;
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
               SetActive(('0' + e.Key - Key.D0).ToString());
               return true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
               SetActive(('0' + e.Key - Key.NumPad0).ToString());
               return true;
            }
         }

         return false;
      }

      public Dictionary<Indicator, double> IndicatorLastValue;
      public Dictionary<Indicator, DateTime> IndicatorLastDateUtc;
      public Indicator? FocusCategory = null;
      public double FocusCategoryValue = 0;
      public void SetActive(string key)
      {
         Indicator? indicator = null;
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
            return;
         if (!TimeManager.Instance.IndicatorByHotkey.TryGetValue(key, out indicator))
            return;

         FocusCategory = indicator;
         FocusCategoryValue = IndicatorLastValue.GetValueOrDefault(FocusCategory, 0);
         if (FocusCategoryValue == double.MinValue)
            FocusCategoryValue = 0;
         Refresh();
      }

      public void ClearActive()
      {
         FocusCategory = null;
         Parent.IndicatorHover.Visibility = Visibility.Collapsed;
         Refresh();
      }

      public void IncValue()
      {
         FocusCategoryValue = Math.Min(FocusCategoryValue + 1, 0.49 + FocusCategory.Values.Length - 1 - FocusCategory.Offset);
         Refresh();
      }

      public void DecValue()
      {
         FocusCategoryValue = Math.Max(FocusCategoryValue - 1, -FocusCategory.Offset - 0.49);
         Refresh();
      }

      public void PlaceIndicator(DateTime timeUtc)
      {
         if (FocusCategory == null)
            return;
         TimeManager.Instance.AddTimeIndicator(new TimeIndicator() { Category = FocusCategory.Name, Value = FocusCategoryValue, Time = timeUtc });
         if (!IndicatorLastDateUtc.TryGetValue(FocusCategory, out DateTime lastTime) || timeUtc > lastTime)
         {
            IndicatorLastValue[FocusCategory] = FocusCategoryValue;
            IndicatorLastDateUtc[FocusCategory] = timeUtc;
         }
         FocusCategory = null;
         Parent.IndicatorHover.Visibility = Visibility.Collapsed;
         Refresh();
         Parent.TimeBar?.Refresh();
      }

      #endregion
      #region Mouse Input (Panel)

      public void IndicatorPanel_MouseEnter(object sender, MouseEventArgs e)
      {
         if (FocusCategory == null)
            return;
         Parent.IndicatorHover.Visibility = Visibility.Visible;
         IndicatorPanel_MouseMove(sender, e);
      }

      public void IndicatorPanel_MouseLeave(object sender, MouseEventArgs e)
      {
         Parent.IndicatorHover.Visibility = Visibility.Collapsed;
      }

      public void IndicatorPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         if (FocusCategory == null)
            return;

         Point pos = e.GetPosition(Parent.IndicatorPanel);
         double pctX = pos.X / Parent.IndicatorPanel.RenderSize.Width;
         DateTime dayBegin = new DateTime(Parent.DailyDay.Year, Parent.DailyDay.Month, Parent.DailyDay.Day, 0, 0, 0);
         DateTime clickTime = dayBegin.AddHours(Parent.TimeBar.MinHour + (pctX * (Parent.TimeBar.MaxHour - Parent.TimeBar.MinHour))).ToUniversalTime();
         PlaceIndicator(clickTime);
      }

      public void IndicatorPanel_MouseMove(object sender, MouseEventArgs e)
      {
         Point pos = e.GetPosition(Parent.IndicatorPanel);
         double pctX = pos.X / Parent.IndicatorPanel.RenderSize.Width;
         FreeGrid.SetLeft(Parent.IndicatorHover, new PercentValue(PercentValue.ModeType.Percent, pctX));
      }

      #endregion
      #region Mouse Input (Slider)

      double OldFocusCategoryValue;

      double GetSliderValue(MouseEventArgs e)
      {
         Point pos = e.GetPosition(Parent.IndicatorSliderGrid);
         double pct = pos.X / Parent.IndicatorSliderGrid.RenderSize.Width;
         double value = Math.Clamp((pct * FocusCategory.Values.Length) - FocusCategory.Offset - 0.5,
            -FocusCategory.Offset - 0.49, 0.49 + FocusCategory.Values.Length - 1 - FocusCategory.Offset);
         return value;
      }

      public void IndicatorSlider_MouseEnter(object sender, MouseEventArgs e)
      {
         if (FocusCategory == null)
            return;

         OldFocusCategoryValue = FocusCategoryValue;
         IndicatorSlider_MouseMove(sender, e);
      }

      public void IndicatorSlider_MouseLeave(object sender, MouseEventArgs e)
      {
         FocusCategoryValue = OldFocusCategoryValue;
         Refresh();
      }

      public void IndicatorSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         if (FocusCategory == null)
            return;

         FocusCategoryValue = GetSliderValue(e);
         OldFocusCategoryValue = FocusCategoryValue;
         Refresh();
      }

      public void IndicatorSlider_MouseMove(object sender, MouseEventArgs e)
      {
         FocusCategoryValue = GetSliderValue(e);
         Refresh();
      }

      #endregion
   }
}
