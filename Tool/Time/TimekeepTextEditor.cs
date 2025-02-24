using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Tildetool.Time
{
   public class TimekeepTextEditor
   {
      public Timekeep Parent;
      public TimekeepTextEditor(Timekeep parent)
      {
         Parent = parent;
      }

      System.Action<string>? _TextEditorCallback;
      bool IsTextEditor => _TextEditorCallback != null;
      bool EatEvent = false;
      public void Show(System.Action<string> callback)
      {
         _TextEditorCallback = callback;
         Parent.TextEditorPane.Visibility = Visibility.Visible;
         Parent.TextEditor.Text = "";
         Parent.TextEditor.Focus();
      }

      public bool HandleKeyDown(object sender, KeyEventArgs e)
      {
         if (EatEvent)
         {
            EatEvent = false;
            return true;
         }
         if (IsTextEditor)
            return true;
         return false;
      }

      public void TextEditor_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.Key == Key.Enter || e.Key == Key.Return)
         {
            string text = Parent.TextEditor.Text;
            var callback = _TextEditorCallback;
            _TextEditorCallback = null;
            Parent.TextEditorPane.Visibility = Visibility.Collapsed;
            EatEvent = true;

            if (!string.IsNullOrEmpty(text))
               callback(text);
            return;
         }
         if (e.Key == Key.Escape)
         {
            _TextEditorCallback = null;
            Parent.TextEditorPane.Visibility = Visibility.Collapsed;
            EatEvent = true;
            return;
         }
      }
   }
}
