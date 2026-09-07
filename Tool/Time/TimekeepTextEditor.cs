using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
      public string Note => Parent.TextEditorNote.Text;

      System.Action<string, string>? _TextEditorCallback;
      System.Action<string, string>? _FnUpdate;
      System.Action? _FnCancel;

      string Title;
      public string[] FoundOptions = new string[0];
      public List<string> Options;
      bool EatEvent = false;
      TextBlock[] TextOptions;

      public void Show(string title, System.Action<string, string> fnCallback, System.Action<string, string>? fnUpdate = null, System.Action? fnCancel = null, List<string>? options = null)
      {
         _TextEditorCallback = fnCallback;
         _FnUpdate = fnUpdate;
         _FnCancel = fnCancel;

         Title = title;
         Options = options ?? new();
         Options.Sort();
         Parent.TextEditorPane.Visibility = Visibility.Visible;
         Parent.TextEditorNote.Text = "";
         Parent.TextEditorNote.Visibility = Visibility.Collapsed;
         Parent.TextEditor.Text = "";
         Parent.TextEditor.Focus();

         Parent.TextEditorTitle.Text = title;
         TextOptions = new[] { Parent.TextOption0, Parent.TextOption1, Parent.TextOption2, Parent.TextOption3, Parent.TextOption4, Parent.TextOption5,
            Parent.TextOption6, Parent.TextOption7, Parent.TextOption8 };
         foreach (var opt in TextOptions)
            opt.Visibility = Visibility.Collapsed;
      }

      public void ConfirmAddProject(string ident, System.Action<int> callback)
      {
         if (TimeManager.Instance.ProjectIdentToId.TryGetValue(ident, out int projectId))
         {
            callback(projectId);
            return;
         }

         Parent.TimekeepTextEditor.Show("confirm new project?", (text, note) =>
         {
            int projectId = TimeManager.Instance.AddProject(ident);
            callback(projectId);
         });
         Parent.TextEditor.Text = "y";
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
            _FnUpdate = null;
            _FnCancel = null;
            Parent.TextEditorPane.Visibility = Visibility.Collapsed;
            EatEvent = true;

            if (!string.IsNullOrEmpty(text))
               callback(text, Parent.TextEditorNote.Text);
         }
         else if (e.Key == Key.Escape)
         {
            _TextEditorCallback = null;
            _FnUpdate = null;
            var callback = _FnCancel;
            _FnCancel = null;
            Parent.TextEditorPane.Visibility = Visibility.Collapsed;
            EatEvent = true;

            callback?.Invoke();
         }
         else if (e.Key == Key.Tab)
         {
            if (Parent.TextEditor.IsFocused)
            {
               Parent.TextEditorNote.Visibility = Visibility.Visible;
               Parent.TextEditorNote.Focus();
            }
            else
            {
               Parent.TextEditor.Focus();
               Parent.TextEditorNote.Visibility = !string.IsNullOrEmpty(Parent.TextEditorNote.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
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
         //if (Parent.TextEditor.Text.Length > 0)
         //   if (e.Changes.All(c => c.AddedLength <= 0 && c.Offset == Parent.TextEditor.Text.Length))
         //      Parent.TextEditor.Text = Parent.TextEditor.Text[..^1];

         if (Parent.TextEditor.Text.Length == 0)
         {
            FoundOptions = new string[0];
            foreach (var option in TextOptions)
               option.Visibility = Visibility.Collapsed;
            _FnUpdate?.Invoke(Parent.TextEditor.Text, Parent.TextEditorNote.Text);
            _ManualChange = false;
            return;
         }

         bool wasDelete = e.Changes.All(c => c.AddedLength <= 0) && e.Changes.Any(c => c.RemovedLength > 0);
         if (!wasDelete)
         {
            int oldLength = Parent.TextEditor.Text.Length;
            IEnumerable<string> options = Options.Where(o => o.Length >= oldLength && o.StartsWith(Parent.TextEditor.Text));
            FoundOptions = options.Take(TextOptions.Length + 1).ToArray();
            if (FoundOptions.Length > 0)
            {
               Parent.TextEditor.Text = FoundOptions[0];
               Parent.TextEditor.Select(oldLength, FoundOptions[0].Length - oldLength);
            }

            for (int i = 0; i < TextOptions.Length; i++)
            {
               TextOptions[i].Visibility = FoundOptions.Length > i ? Visibility.Visible : Visibility.Collapsed;
               if (FoundOptions.Length > i)
                  TextOptions[i].Text = FoundOptions[i];
            }
         }
         else
            FoundOptions = new string[0];

         _FnUpdate?.Invoke(Parent.TextEditor.Text, Parent.TextEditorNote.Text);

         _ManualChange = false;
      }
   }
}
