using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Tildetool
{
   public static class Extension
   {
      #region Extension

      /// <summary>
      /// Extension method for a FrameworkElement that searches for a child element by type and name.
      /// </summary>
      /// <typeparam name="T">The type of the child element to search for.</typeparam>
      /// <param name="element">The parent framework element.</param>
      /// <param name="sChildName">The name of the child element to search for.</param>
      /// <returns>The matching child element, or null if none found.</returns>
      public static T? FindElementByName<T>(this FrameworkElement element, string sChildName) where T : FrameworkElement
      {
         T childElement = null;

         //
         // Spin through immediate children of the starting element.
         //
         var nChildCount = VisualTreeHelper.GetChildrenCount(element);
         for (int i = 0; i < nChildCount; i++)
         {
            // Get next child element.
            FrameworkElement? child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;

            // Do we have a child?
            if (child == null)
               continue;

            // Is child of desired type and name?
            if (child is T && child.Name.Equals(sChildName))
            {
               // Bingo! We found a match.
               childElement = (T)child;
               break;
            } // if

            // Recurse and search through this child's descendants.
            childElement = FindElementByName<T>(child, sChildName);

            // Did we find a matching child?
            if (childElement != null)
               break;
         }

         return childElement;
      }
      #endregion
   }
}
