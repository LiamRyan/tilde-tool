using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Tildetool.Time.Serialization
{
   public class IndicatorValue
   {
      public string Icon { get; set; }
      public string Name { get; set; }
      public string ColorBack { get; set; }
      public string ColorFore { get; set; }
      public bool IsZero { get; set; }

      public Color GetColorBack(byte alpha = 0xFF) => Color.FromArgb(alpha, byte.Parse(ColorBack[0..2], NumberStyles.HexNumber), byte.Parse(ColorBack[2..4], NumberStyles.HexNumber), byte.Parse(ColorBack[4..6], NumberStyles.HexNumber));
      public Color GetColorFore(byte alpha = 0xFF) => Color.FromArgb(alpha, byte.Parse(ColorFore[0..2], NumberStyles.HexNumber), byte.Parse(ColorFore[2..4], NumberStyles.HexNumber), byte.Parse(ColorFore[4..6], NumberStyles.HexNumber));
   }
   public class Indicator
   {
      public string Name { get; set; }
      public string Hotkey { get; set; }
      public IndicatorValue[] Values { get; set; }
      public bool Hidden { get; set; }

      public int MinValue => -Offset;
      public int MaxValue => Values.Length - Offset - 1;

      int _Offset = int.MinValue;
      public int Offset
      {
         get
         {
            if (_Offset != int.MinValue)
               return _Offset;

            for (int i = 0; i < Values.Length; i++)
               if (Values[i].IsZero)
               {
                  _Offset = i;
                  return _Offset;
               }
            _Offset = Values.Length / 2;
            return _Offset;
         }
      }

      public int GetIndex(double value)
         => (int)Math.Round(value) + Offset;

      public double GetSubvalue(double value)
         => value + 0.5 - Math.Round(value);

      public IndicatorValue GetValue(double value)
      {
         int index = (int)Math.Round(value) + Offset;
         if (index < 0 || index >= Values.Length)
            return null;

         return Values[index];
      }

      Color GetColor(double value, byte alpha, System.Func<IndicatorValue, byte, Color> fnGetColor)
      {
         int index = (int)Math.Round(value) + Offset;
         if (index < 0 || index >= Values.Length)
            return new();

         IndicatorValue valueCls = Values[index];
         Color baseColor = fnGetColor(valueCls, alpha);

         double valueRounded = Math.Round(value);
         if (value < valueRounded && index > 0)
         {
            IndicatorValue altValueCls = Values[index - 1];
            Color altColor = fnGetColor(altValueCls, alpha);
            return baseColor.Lerp(altColor, 0.5f * (float)(valueRounded - value));
         }
         if (value > valueRounded && index + 1 < Values.Length)
         {
            IndicatorValue altValueCls = Values[index + 1];
            Color altColor = fnGetColor(altValueCls, alpha);
            return baseColor.Lerp(altColor, 0.5f * (float)(value - valueRounded));
         }
         return baseColor;
      }

      public Color GetColorBack(double value, byte alpha = 0xFF)
         => GetColor(value, alpha, (valueCls, alpha) => valueCls.GetColorBack(alpha));

      public Color GetColorFore(double value, byte alpha = 0xFF)
         => GetColor(value, alpha, (valueCls, alpha) => valueCls.GetColorFore(alpha));
   }
}
