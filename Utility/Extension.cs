using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

      public static string Between(this string str, string strStart, string strEnd, int initialIndex = 0)
      {
         int indexS = str.IndexOf(strStart, initialIndex);
         if (indexS == -1)
            return "";
         indexS += strStart.Length;

         int indexE = str.IndexOf(strEnd, indexS);
         if (indexE == -1)
            return "";

         return str.Substring(indexS, indexE - indexS);
      }

      public static Color FromArgb(uint argb)
      {
         return Color.FromArgb((byte)((argb >> 24) & 0xFF), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));
      }
      public static Color FromRgb(uint rgb)
      {
         return Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
      }
      public static Color Alpha(this Color color, byte alpha)
      {
         return Color.FromArgb(alpha, color.R, color.G, color.B);
      }
      public static Color Lerp(this Color colorA, Color colorB, float pct)
      {
         return Color.FromArgb((byte)( ((1.0f - pct) * (float)colorA.A) + (pct * (float)colorB.A) ),
                               (byte)( ((1.0f - pct) * (float)colorA.R) + (pct * (float)colorB.R) ),
                               (byte)( ((1.0f - pct) * (float)colorA.G) + (pct * (float)colorB.G) ),
                               (byte)( ((1.0f - pct) * (float)colorA.B) + (pct * (float)colorB.B) ));
      }
      public static Color Lerp(this Color colorA, uint argbB, float pct)
      {
         Color colorB = FromArgb(argbB);
         return Color.FromArgb((byte)(((1.0f - pct) * (float)colorA.A) + (pct * (float)colorB.A)),
                               (byte)(((1.0f - pct) * (float)colorA.R) + (pct * (float)colorB.R)),
                               (byte)(((1.0f - pct) * (float)colorA.G) + (pct * (float)colorB.G)),
                               (byte)(((1.0f - pct) * (float)colorA.B) + (pct * (float)colorB.B)));
      }
   }

   public struct RGB
   {
      uint RGBA;
      public RGB(int rgb)
      {
         if (rgb >= (1 << 24))
            RGBA = (uint)rgb;
         else
            RGBA = (((uint)rgb) << 8) | 0xFF;
      }
      public RGB(uint rgba)
      {
         RGBA = rgba;
      }
      public RGB(byte r, byte g, byte b)
      {
         RGBA = (((uint)r) << 24) | (((uint)g) << 16) | (((uint)b) << 8) | 0xFF;
      }
      public RGB(byte r, byte g, byte b, float af)
      {
         byte a = (byte)(af * 255.0f);
         RGBA = (((uint)r) << 24) | (((uint)g) << 16) | (((uint)b) << 8) | (uint)a;
      }

      public static implicit operator Color(RGB colorRGB)
      {
         float r = (colorRGB.RGBA & 0xFF000000) >> 24;
         float g = (colorRGB.RGBA & 0x00FF0000) >> 16;
         float b = (colorRGB.RGBA & 0x0000FF00) >> 8;
         float a = (colorRGB.RGBA & 0x000000FF);
         return Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);
      }

      public static explicit operator RGB(int rgb) => new RGB(rgb);

      public static explicit operator RGB(uint rgb) => new RGB(rgb);

      public override string ToString()
      {
         if ((RGBA & 0xFF) != 0xFF)
            return $"{RGBA:X8}";
         return $"{RGBA >> 8:X6}";
      }
   }
}
