using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tildetool
{
   public abstract class DataTemplater
   {
      public DataTemplater(FrameworkElement root)
      {
         foreach (FieldInfo field in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
         {
            object value = root.FindName(field.Name);
            field.SetValue(this, value);
         }
      }

      public static void Populate<TData>(Panel parent, DataTemplate template, IEnumerable<TData> datalist, System.Action<ContentControl, FrameworkElement, int, TData> populate)
      {
         int index = 0;
         foreach (TData data in datalist)
         {
            ContentControl content;
            ContentPresenter presenter;
            if (index >= parent.Children.Count)
            {
               content = new ContentControl { ContentTemplate = template };
               parent.Children.Add(content);
               content.ApplyTemplate();
               presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            }
            else
            {
               content = parent.Children[index] as ContentControl;
               content.ApplyTemplate();
               presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            }
            presenter.ApplyTemplate();

            if (populate != null)
            {
               FrameworkElement root = VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement;
               populate(content, root, index, data);
            }

            index++;
         }
         while (parent.Children.Count > index)
            parent.Children.RemoveAt(parent.Children.Count - 1);
      }
   }
}
