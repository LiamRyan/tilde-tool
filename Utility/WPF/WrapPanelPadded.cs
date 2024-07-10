using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace Tildetool.WPF
{
   public class WrapPanelPadded : WrapPanel
   {
      public bool ReverseOrder { get; set; }
      public double RowMargin { get; set; }
      public double ColMargin { get; set; }

      protected override Size MeasureOverride(Size availableSize)
      {
         Size childTotalAvailSize = new Size(!double.IsNaN(ItemWidth) ? ItemWidth : availableSize.Width,
                                             !double.IsNaN(ItemHeight) ? ItemHeight : availableSize.Height);
         Size childAvailSize = new Size(childTotalAvailSize.Width, childTotalAvailSize.Height);
         Size totalBounds = new Size(0, 0);
         Point childPos = new Point(0, 0);

         for (int i = 0; i < InternalChildren.Count; i++)
         {
            // Let the child figure out how much it wants.
            int index = ReverseOrder ? (InternalChildren.Count - i - 1) : i;
            UIElement child = InternalChildren[index];
            child.Measure(childAvailSize);

            // Clamp it to specified item height.
            Size childActualSize = new Size(!double.IsNaN(ItemWidth) ? ItemWidth : child.DesiredSize.Width,
                                            !double.IsNaN(ItemHeight) ? ItemHeight : child.DesiredSize.Height);

            // Handle if it needs to break to a new line.
            if (Orientation == Orientation.Horizontal)
            {
               if (childPos.X != 0 && childPos.X + childActualSize.Width > availableSize.Width)
               {
                  childPos = new Point(0, totalBounds.Height + RowMargin);
                  childAvailSize = new Size(childTotalAvailSize.Width, childTotalAvailSize.Height);
               }
            }
            else
            {
               if (childPos.Y != 0 && childPos.Y + childActualSize.Height > availableSize.Height)
               {
                  childPos = new Point(totalBounds.Width + ColMargin, 0);
                  childAvailSize = new Size(childTotalAvailSize.Width, childTotalAvailSize.Height);
               }
            }

            // That's its position!  Track our ongoing bounds.
            totalBounds.Width = Math.Max(totalBounds.Width, childPos.X + childActualSize.Width);
            totalBounds.Height = Math.Max(totalBounds.Height, childPos.Y + childActualSize.Height);

            // Advance for the next element.
            if (Orientation == Orientation.Horizontal)
               childPos.X += childActualSize.Width + ColMargin;
            else
               childPos.Y += childActualSize.Height + RowMargin;
         }

         return totalBounds;
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         Size totalBounds = new Size(0, 0);
         Point childPos = new Point(0, 0);

         for (int i = 0; i < InternalChildren.Count; i++)
         {
            int index = ReverseOrder ? (InternalChildren.Count - i - 1) : i;
            UIElement child = InternalChildren[index];

            // Clamp it to specified item height.
            Size childActualSize = new Size(!double.IsNaN(ItemWidth) ? ItemWidth : child.DesiredSize.Width,
                                            !double.IsNaN(ItemHeight) ? ItemHeight : child.DesiredSize.Height);

            // Handle if it needs to break to a new line.
            if (Orientation == Orientation.Horizontal)
            {
               if (childPos.X != 0 && childPos.X + childActualSize.Width > finalSize.Width)
                  childPos = new Point(0, totalBounds.Height + RowMargin);
            }
            else
            {
               if (childPos.Y != 0 && childPos.Y + childActualSize.Height > finalSize.Height)
                  childPos = new Point(totalBounds.Width + ColMargin, 0);
            }

            // That's its position!  Track our ongoing bounds.
            child.Arrange(new Rect(childPos, childActualSize));
            totalBounds.Width = Math.Max(totalBounds.Width, childPos.X + childActualSize.Width);
            totalBounds.Height = Math.Max(totalBounds.Height, childPos.Y + childActualSize.Height);

            // Advance for the next element.
            if (Orientation == Orientation.Horizontal)
               childPos.X += childActualSize.Width + ColMargin;
            else
               childPos.Y += childActualSize.Height + RowMargin;
         }

         return totalBounds;
      }
   }
}