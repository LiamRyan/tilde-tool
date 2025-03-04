using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

      public bool IsTextEditor => _TextEditorCallback != null;

      public string Text => Parent.TextEditor.Text;

      System.Action<string>? _TextEditorCallback;
      List<string> Options;
      bool EatEvent = false;
      public void Show(System.Action<string> callback, List<string> options)
      {
         _TextEditorCallback = callback;
         Options = options;
         Options.Sort();
         Parent.TextEditorPane.Visibility = Visibility.Visible;
         Parent.TextEditor.Text = "";
         Parent.TextEditor.Focus();
         Parent.TextOption0.Visibility = Visibility.Collapsed;
         Parent.TextOption1.Visibility = Visibility.Collapsed;
         Parent.TextOption2.Visibility = Visibility.Collapsed;
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
         }
         else if (e.Key == Key.Escape)
         {
            _TextEditorCallback = null;
            Parent.TextEditorPane.Visibility = Visibility.Collapsed;
            EatEvent = true;
         }
      }

      bool _ManualChange = false;
      public void TextEditor_TextChanged(object sender, TextChangedEventArgs e)
      {
         if (_ManualChange)
            return;
         _ManualChange = true;

         // removed end: delete a character
         if (Parent.TextEditor.Text.Length > 0)
            if (e.Changes.All(c => c.AddedLength <= 0 && c.Offset == Parent.TextEditor.Text.Length))
               Parent.TextEditor.Text = Parent.TextEditor.Text[..^1];

         if (Parent.TextEditor.Text.Length == 0)
         {
            Parent.TextOption0.Visibility = Visibility.Collapsed;
            Parent.TextOption1.Visibility = Visibility.Collapsed;
            Parent.TextOption2.Visibility = Visibility.Collapsed;
            _ManualChange = false;
            return;
         }

         int oldLength = Parent.TextEditor.Text.Length;
         IEnumerable<string> options = Options.Where(o => o.Length >= oldLength && o.StartsWith(Parent.TextEditor.Text));
         string[] foundOptions = options.Take(4).ToArray();
         if (foundOptions.Length > 0)
         {
            Parent.TextEditor.Text = foundOptions[0];
            Parent.TextEditor.Select(oldLength, foundOptions[0].Length - oldLength);
         }

         Parent.TextOption0.Visibility = foundOptions.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
         if (foundOptions.Length > 0)
            Parent.TextOption0.Text = foundOptions[0];

         Parent.TextOption1.Visibility = foundOptions.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
         if (foundOptions.Length > 1)
            Parent.TextOption1.Text = foundOptions[1];

         Parent.TextOption2.Visibility = foundOptions.Length > 2 ? Visibility.Visible : Visibility.Collapsed;
         if (foundOptions.Length > 2)
            Parent.TextOption2.Text = foundOptions[2];

         _ManualChange = false;
      }
   }
}
