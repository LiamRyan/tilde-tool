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

         TimeManager.Instance.QueryLastTimeIndicators(out int[] values, out DateTime[] dates);
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
         Parent.IndicatorPanes.Visibility = todayMode ? Visibility.Visible : Visibility.Collapsed;
         if (!todayMode)
            return;

         DataTemplate? templatePane = Parent.Resources["IndicatorPane"] as DataTemplate;
         DataTemplater.Populate(Parent.IndicatorPanes, templatePane, TimeManager.Instance.Indicators, (content, root, i, data) =>
         {
            IndicatorPane pane = new IndicatorPane(root);
            root.Height = 42;
            pane.Title.Text = data.Name;
            if (data.Name[0].ToString() == data.Hotkey)
               pane.Title.Text = $"[{data.Hotkey}]{data.Name[1..]}";
            else
               pane.Title.Text = $"[{data.Hotkey}] {data.Name}";

            int index = int.MinValue;
            if (FocusCategory == data)
               index = FocusCategoryValue;
            else if (IndicatorLastValue.TryGetValue(data, out int defaultValue))
               index = defaultValue;
            index += data.Offset;

            pane.Backfill.Visibility = (FocusCategory == data) ? Visibility.Visible : Visibility.Collapsed;
            if (index >= 0 && index < data.Values.Length)
            {
               pane.Backfill.Background = new SolidColorBrush(data.Values[index].GetColorBack(0x40));
               pane.Title.Foreground = new SolidColorBrush(data.Values[index].GetColorBack());
            }

            if (todayMode && index != int.MinValue)
            {
               bool hasIndex = index >= 0 && index < data.Values.Length;
               pane.Icon.Visibility = Visibility.Visible;
               pane.Text.Visibility = (FocusCategory == data) ? Visibility.Visible : Visibility.Collapsed;
               pane.Date.Visibility = hasIndex ? Visibility.Visible : Visibility.Collapsed;
               if (hasIndex)
               {
                  pane.Icon.Foreground = new SolidColorBrush(data.Values[index].GetColorFore());
                  pane.Text.Foreground = new SolidColorBrush(data.Values[index].GetColorBack());
                  pane.Icon.Text = data.Values[index].Icon;
                  pane.Text.Text = data.Values[index].Name;

                  if (IndicatorLastDateUtc.TryGetValue(data, out DateTime date) && hasIndex)
                  {
                     TimeSpan deltaF = DateTime.Now - date.ToLocalTime();
                     TimeSpan delta = DateTime.Now.Date - date.ToLocalTime().Date;
                     if (delta.TotalDays > 0.99)
                     {
                        pane.Date.Foreground = new SolidColorBrush(data.Values[index].GetColorBack());
                        pane.Date.Text = $"{((int)Math.Round(delta.TotalDays)):D} days ago";
                     }
                     else if (deltaF.TotalHours > 0.99)
                     {
                        pane.Date.Foreground = new SolidColorBrush(data.Values[index].GetColorBack().Lerp(Color.FromRgb(0, 0, 0), 0.5f));
                        pane.Date.Text = $"{((int)Math.Round(deltaF.TotalHours)):D} hours ago";
                     }
                     else
                        pane.Date.Visibility = Visibility.Collapsed;
                  }
                  else
                  {
                     pane.Date.Foreground = new SolidColorBrush(data.Values[index].GetColorBack().Lerp(Color.FromRgb(0, 0, 0), 0.5f));
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
      }

      #endregion
      #region Hotkey Input

      public bool HandleKeyDown(object sender, KeyEventArgs e)
      {
         if (FocusCategory != null)
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

      public Dictionary<Indicator, int> IndicatorLastValue;
      public Dictionary<Indicator, DateTime> IndicatorLastDateUtc;
      public Indicator? FocusCategory = null;
      public int FocusCategoryValue = 0;
      public void SetActive(string key)
      {
         Indicator? indicator = null;
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
            return;
         if (!TimeManager.Instance.IndicatorByHotkey.TryGetValue(key, out indicator))
            return;

         FocusCategory = indicator;
         FocusCategoryValue = IndicatorLastValue.GetValueOrDefault(FocusCategory, 0);
         if (FocusCategoryValue == int.MinValue)
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
         FocusCategoryValue = Math.Min(FocusCategoryValue + 1, FocusCategory.Values.Length - 1 - FocusCategory.Offset);
         Refresh();
      }

      public void DecValue()
      {
         FocusCategoryValue = Math.Max(FocusCategoryValue - 1, -FocusCategory.Offset);
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
      }

      #endregion
      #region Mouse Input

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
   }
}
