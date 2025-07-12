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
      TextBlock[] TextOptions;
      public void Show(System.Action<string> callback, List<string> options)
      {
         _TextEditorCallback = callback;
         Options = options;
         Options.Sort();
         Parent.TextEditorPane.Visibility = Visibility.Visible;
         Parent.TextEditor.Text = "";
         Parent.TextEditor.Focus();

         TextOptions = new[] { Parent.TextOption0, Parent.TextOption1, Parent.TextOption2, Parent.TextOption3, Parent.TextOption4, Parent.TextOption5,
            Parent.TextOption6, Parent.TextOption7, Parent.TextOption8 };
         foreach (var opt in TextOptions)
            opt.Visibility = Visibility.Collapsed;
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
            foreach (var option in TextOptions)
               option.Visibility = Visibility.Collapsed;
            _ManualChange = false;
            return;
         }

         int oldLength = Parent.TextEditor.Text.Length;
         IEnumerable<string> options = Options.Where(o => o.Length >= oldLength && o.StartsWith(Parent.TextEditor.Text));
         string[] foundOptions = options.Take(TextOptions.Length + 1).ToArray();
         if (foundOptions.Length > 0)
         {
            Parent.TextEditor.Text = foundOptions[0];
            Parent.TextEditor.Select(oldLength, foundOptions[0].Length - oldLength);
         }

         for (int i = 0; i < TextOptions.Length; i++)
         {
            TextOptions[i].Visibility = foundOptions.Length > i ? Visibility.Visible : Visibility.Collapsed;
            if (foundOptions.Length > i)
               TextOptions[i].Text = foundOptions[i];
         }

         _ManualChange = false;
      }
   }
}
