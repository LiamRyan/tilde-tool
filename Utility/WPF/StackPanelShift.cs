using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Tildetool.WPF
{
   public class StackPanelShift : StackPanel
   {
      public static readonly DependencyProperty AlongProperty = DependencyProperty.Register("Along", typeof(double), typeof(StackPanelShift),
         new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

      public static double GetAlong(UIElement element)
      {
         return (double)element.GetValue(AlongProperty);
      }
      public static void SetAlong(UIElement element, double value)
      {
         element.SetValue(AlongProperty, value);
      }

      public double ItemThickness { get; set; }

      protected override Size MeasureOverride(Size availableSize)
      {
         Size totalBounds = new Size(availableSize.Width, 0);
         List<List<double>> rowL = new List<List<double>>();
         List<List<double>> rowR = new List<List<double>>();

         for (int i = 0; i < InternalChildren.Count; i++)
         {
            int index = i;
            UIElement child = InternalChildren[index];
            double along = GetAlong(child);
            Point childPos = new Point(along * availableSize.Width, 0);

            // Let the child figure out how much it wants.
            {
               if (childPos.X >= availableSize.Width)
                  childPos.X = availableSize.Width - 1;
               Size availSize = new Size(availableSize.Width - childPos.X, ItemThickness);
               child.Measure(availSize);
               if (childPos.X + child.DesiredSize.Width > availableSize.Width)
                  childPos.X = availableSize.Width - child.DesiredSize.Width;
            }

            // Pick a row.
            int row = 0;
            for (; row < rowL.Count; row++)
               if (Enumerable.Range(0, rowL[row].Count).All(i => childPos.X >= rowR[row][i] || childPos.X + child.DesiredSize.Width <= rowL[row][i]))
                  break;
            if (row >= rowL.Count)
            {
               rowL.Add(new List<double>());
               rowR.Add(new List<double>());
               totalBounds.Height = ItemThickness * (row + 1);
            }
            rowL[row].Add(childPos.X);
            rowR[row].Add(childPos.X + child.DesiredSize.Width);

            // That's its position!  Track our ongoing bounds.
            totalBounds.Width = Math.Max(totalBounds.Width, childPos.X + child.DesiredSize.Width);
         }

         return totalBounds;
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         Size totalBounds = new Size(finalSize.Width, 0);
         List<List<double>> rowL = new List<List<double>>();
         List<List<double>> rowR = new List<List<double>>();

         for (int i = 0; i < InternalChildren.Count; i++)
         {
            int index = i;
            UIElement child = InternalChildren[index];
            double along = GetAlong(child);
            Point childPos = new Point(along * finalSize.Width, 0);

            // Pick a row.
            int row = 0;
            for (; row < rowL.Count; row++)
               if (Enumerable.Range(0, rowL[row].Count).All(i => childPos.X >= rowR[row][i] || childPos.X + child.DesiredSize.Width <= rowL[row][i]))
                  break;
            if (row >= rowL.Count)
            {
               rowL.Add(new List<double>());
               rowR.Add(new List<double>());
               totalBounds.Height = ItemThickness * (row + 1);
            }
            rowL[row].Add(childPos.X);
            rowR[row].Add(childPos.X + child.DesiredSize.Width);

            // Assign the position
            childPos.Y = ItemThickness * row;
            child.Arrange(new Rect(childPos, child.DesiredSize));

            // That's its position!  Track our ongoing bounds.
            totalBounds.Width = Math.Max(totalBounds.Width, childPos.X + child.DesiredSize.Width);
         }

         return totalBounds;
      }
   }
}