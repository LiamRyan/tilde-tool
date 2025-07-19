using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tildetool.WPF
{
   [TypeConverter(typeof(PercentValueConverter))]
   public class PercentValue
   {
      public enum ModeType
      {
         Pixel,
         Percent
      }

      public ModeType Mode { get; set; }
      public double Value { get; set; }

      public PercentValue(ModeType mode, double value)
      {
         Mode = mode;
         Value = value;
      }

      public double GetValue(double parentSize)
      {
         switch (Mode)
         {
            case ModeType.Pixel: return Value;
            case ModeType.Percent: return Value * parentSize;
         }
         return double.NaN;
      }

      public override string ToString()
      {
         switch (Mode)
         {
            case ModeType.Pixel: return Value.ToString();
            case ModeType.Percent: return $"{Value * 100.0}%";
         }
         return "0";
      }
   }

   public class PercentValueConverter : TypeConverter
   {
      public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
      {
         return sourceType == typeof(string) || sourceType == typeof(int) || sourceType == typeof(float) || sourceType == typeof(double);
      }

      public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
      {
         if (value is string valueAsString)
         {
            if (valueAsString.EndsWith('%'))
            {
               if (double.TryParse(valueAsString.TrimEnd('%'), out double result))
                  return new PercentValue(PercentValue.ModeType.Percent, 0.01 * result);
            }
            else if (double.TryParse(valueAsString, out double result))
               return new PercentValue(PercentValue.ModeType.Pixel, result);
         }
         else if (value is double valueDouble)
            return new PercentValue(PercentValue.ModeType.Pixel, valueDouble);
         else if (value is float valueFloat)
            return new PercentValue(PercentValue.ModeType.Pixel, valueFloat);
         else if (value is int valueInt)
            return new PercentValue(PercentValue.ModeType.Pixel, valueInt);

         return base.ConvertFrom(context, culture, value);
      }

      public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
      {
         return destinationType == typeof(string) || destinationType == typeof(int) || destinationType == typeof(float) || destinationType == typeof(double);
      }

      public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
      {
         if (value == null)
            return base.ConvertTo(context, culture, value, destinationType);
         PercentValue percentValue = value as PercentValue;
         if (percentValue == null)
            return base.ConvertTo(context, culture, value, destinationType);

         if (destinationType == typeof(string))
            return percentValue.ToString();
         if (destinationType == typeof(double))
            return (double)percentValue.Value;
         if (destinationType == typeof(float))
            return (float)percentValue.Value;
         if (destinationType == typeof(int))
            return (int)percentValue.Value;

         return base.ConvertTo(context, culture, value, destinationType);
      }
   }

   public class FreeGrid : Grid
   {
      public static readonly DependencyProperty LeftProperty = DependencyProperty.RegisterAttached("Left", typeof(PercentValue), typeof(FreeGrid),
         new FrameworkPropertyMetadata(new PercentValue(PercentValue.ModeType.Percent, 0.0), OnAttachedPropertyChanged));
      public static PercentValue GetLeft(UIElement element) => (PercentValue)element.GetValue(LeftProperty);
      public static void SetLeft(UIElement element, PercentValue value) => element.SetValue(LeftProperty, value);

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetLeftPct(UIElement element, double pct) => SetLeft(element, new PercentValue(PercentValue.ModeType.Percent, pct));

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetLeftPix(UIElement element, double pix) => SetLeft(element, new PercentValue(PercentValue.ModeType.Pixel, pix));


      public static readonly DependencyProperty TopProperty = DependencyProperty.RegisterAttached("Top", typeof(PercentValue), typeof(FreeGrid),
         new FrameworkPropertyMetadata(new PercentValue(PercentValue.ModeType.Percent, 0.0), OnAttachedPropertyChanged));
      public static PercentValue GetTop(UIElement element) => (PercentValue)element.GetValue(TopProperty);
      public static void SetTop(UIElement element, PercentValue value) => element.SetValue(TopProperty, value);

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetTopPct(UIElement element, double pct) => SetTop(element, new PercentValue(PercentValue.ModeType.Percent, pct));

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetTopPix(UIElement element, double pix) => SetTop(element, new PercentValue(PercentValue.ModeType.Pixel, pix));


      new public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterAttached("Width", typeof(PercentValue), typeof(FreeGrid),
         new FrameworkPropertyMetadata(new PercentValue(PercentValue.ModeType.Percent, 1.0), OnAttachedPropertyChanged));
      public static PercentValue GetWidth(UIElement element) => (PercentValue)element.GetValue(WidthProperty);
      public static void SetWidth(UIElement element, PercentValue value) => element.SetValue(WidthProperty, value);

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetWidthPct(UIElement element, double pct) => SetWidth(element, new PercentValue(PercentValue.ModeType.Percent, pct));

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetWidthPix(UIElement element, double pix) => SetWidth(element, new PercentValue(PercentValue.ModeType.Pixel, pix));


      new public static readonly DependencyProperty HeightProperty = DependencyProperty.RegisterAttached("Height", typeof(PercentValue), typeof(FreeGrid),
         new FrameworkPropertyMetadata(new PercentValue(PercentValue.ModeType.Percent, 1.0), OnAttachedPropertyChanged));
      public static PercentValue GetHeight(UIElement element) => (PercentValue)element.GetValue(HeightProperty);
      public static void SetHeight(UIElement element, PercentValue value) => element.SetValue(HeightProperty, value);

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetHeightPct(UIElement element, double pct) => SetHeight(element, new PercentValue(PercentValue.ModeType.Percent, pct));

      [MethodImpl(MethodImplOptions.AggressiveInlining), System.Diagnostics.DebuggerStepThrough]
      public static void SetHeightPix(UIElement element, double pix) => SetHeight(element, new PercentValue(PercentValue.ModeType.Pixel, pix));


      private static void OnAttachedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         if (d is Visual child)
            if (VisualTreeHelper.GetParent(child) is FreeGrid grid)
               grid.InvalidateMeasure();
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         for (int i = 0; i < InternalChildren.Count; i++)
         {
            int index = i;
            UIElement child = InternalChildren[index];

            double left = GetLeft(child).GetValue(finalSize.Width);
            double top = GetTop(child).GetValue(finalSize.Height);
            double width = GetWidth(child).GetValue(finalSize.Width);
            double height = GetHeight(child).GetValue(finalSize.Height);
            if (width < 0.0)
               width = 0.0;
            if (height < 0.0)
               height = 0.0;

            Point childPos = new Point(left, top);
            Size childSize = new Size(width, height);

            // Assign the position
            child.Arrange(new Rect(childPos, childSize));
         }

         return finalSize;
      }
   }
}