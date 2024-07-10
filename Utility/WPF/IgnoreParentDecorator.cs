using System.Windows;
using System.Windows.Controls;

namespace Tildetool.WPF
{
   public class IgnoreParentDecorator : Decorator
   {
      protected override Size MeasureOverride(Size constraint)
      {
         Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
         return Child.DesiredSize;
      }
   }
}
