using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tildetool.Time.Serialization;

namespace Tildetool.Time
{
   public class ProjectBar
   {
      public Timekeep Parent;
      public ProjectBar(Timekeep parent)
      {
         Parent = parent;

         InitialProject = TimeManager.Instance.CurrentProject;
      }

      public Project? InitialProject;
      List<Project> GuiToProject;
      List<Panel> ProjectGui;

      public Project DailyFocus;

      public void SetActiveTime(string key)
      {
         // Make sure we actually changed to a valid project.
         Project? project = null;
         if (!TimeManager.Instance.HotkeyToProject.TryGetValue(key, out project))
            return;
         SetActiveProject(project);
      }

      public void SetActiveProject(Project project)
      {
         if (Parent.CurDailyMode != Timekeep.DailyMode.Today)
         {
            if (DailyFocus == project)
               DailyFocus = null;
            else
               DailyFocus = project;
            Parent.TimeBar.Refresh();
            return;
         }

         //
         if (project == TimeManager.Instance.CurrentProject)
            return;

         // Switch
         Project oldProject = TimeManager.Instance.CurrentProject;
         if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            TimeManager.Instance.AlterProject(project);
         else
            TimeManager.Instance.SetProject(project);

         // Update the display.
         Parent.RefreshTime();
         if (project != null)
            _AnimateCommand(GuiToProject.IndexOf(project));

         // Turn off a scheduled cancel since we'll finish on our own soon.
         Parent.UnscheduleCancel();

         // Update the coloring of the text.
         for (int i = 0; i < GuiToProject.Count; i++)
         {
            Grid area = ProjectGui[i].FindElementByName<Grid>("Area");
            TextBlock hotkey = ProjectGui[i].FindElementByName<TextBlock>("Hotkey");
            TextBlock text = ProjectGui[i].FindElementByName<TextBlock>("Text");
            bool isCurrent = GuiToProject[i] == TimeManager.Instance.CurrentProject;
            area.Height = isCurrent ? 54 : 40;
            hotkey.Foreground = new SolidColorBrush(isCurrent ? (Parent.Resources["ColorTextFore"] as SolidColorBrush).Color : (Parent.Resources["ColorTextBack"] as SolidColorBrush).Color);
            text.Foreground = new SolidColorBrush(isCurrent ? (Parent.Resources["ColorTextFore"] as SolidColorBrush).Color : (Parent.Resources["ColorTextBack"] as SolidColorBrush).Color);
            text.FontSize = isCurrent ? 20 : 12;
         }
         Parent.CurrentTimeH.Foreground = Parent.Resources["ColorTextBack"] as SolidColorBrush;
         Parent.CurrentTimeM.Foreground = Parent.Resources["ColorTextBack"] as SolidColorBrush;

         //
         if (Parent.CurDailyMode == Timekeep.DailyMode.Today)
         {
            for (int i = 0; i < TimeManager.Instance.Data.Length; i++)
            {
               ContentControl content = Parent.DailyRows.Children[i] as ContentControl;
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;
               TextBlock headerName = grid.FindElementByName<TextBlock>("HeaderName");
               TextBlock headerDate = grid.FindElementByName<TextBlock>("HeaderDate");

               bool isCurrent = TimeManager.Instance.Data[i] == TimeManager.Instance.CurrentProject;
               grid.Background = new SolidColorBrush(Extension.FromArgb((uint)(isCurrent ? 0x404A6030 : ((i % 2) == 0 ? 0x00042508 : 0x38042508))));
               headerName.Foreground = new SolidColorBrush(Extension.FromArgb(isCurrent ? 0xFFC3F1AF : 0xFF449637));
               headerDate.Foreground = new SolidColorBrush(Extension.FromArgb(isCurrent ? 0xFFC3F1AF : 0xFF449637));
            }
         }
      }

      public void RebuildList()
      {
         // Sort with the current first, then in time spent today descending.
         GuiToProject = TimeManager.Instance.Data.Where(p => p != InitialProject).ToList();
         GuiToProject.Sort((a, b) => -a.TimeTodaySec.CompareTo(b.TimeTodaySec));
         if (InitialProject != null)
            GuiToProject.Insert(0, InitialProject);

         // Add or remove to get the right quantity.
         DataTemplate? template = Parent.Resources["CommandOption"] as DataTemplate;
         void _populate(Panel grid, int count)
         {
            while (grid.Children.Count > count)
               grid.Children.RemoveAt(grid.Children.Count - 1);
            while (grid.Children.Count < count)
            {
               ContentControl content = new ContentControl { ContentTemplate = template };
               grid.Children.Add(content);
               content.ApplyTemplate();
               ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
               presenter.ApplyTemplate();
            }
         }

         //
         int preCount = (GuiToProject.Count - 1) / 2;
         int postCount = (GuiToProject.Count - 1) - preCount;
         _populate(Parent.GridPre, preCount);
         _populate(Parent.GridPost, postCount);

         // Add the data
         ProjectGui = new List<Panel>(GuiToProject.Count);
         int increment = 0;
         for (int i = 0; i < GuiToProject.Count; i++)
         {
            bool isCurrent = GuiToProject[i] == TimeManager.Instance.CurrentProject;

            // Pick the right control.
            ContentControl content;
            if (isCurrent)
            {
               content = Parent.CurrentOption;
               increment++;
            }
            else if (((i - increment) % 2) == 0)
               content = Parent.GridPost.Children[(i - increment) / 2] as ContentControl;
            else
               content = Parent.GridPre.Children[preCount - 1 - ((i - increment) / 2)] as ContentControl;

            // Find it
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            Grid grid = VisualTreeHelper.GetChild(presenter, 0) as Grid;
            ProjectGui.Add(grid);

            Grid area = grid.FindElementByName<Grid>("Area");
            TextBlock hotkey = grid.FindElementByName<TextBlock>("Hotkey");
            TextBlock text = grid.FindElementByName<TextBlock>("Text");

            // Update the text
            hotkey.Text = GuiToProject[i].Hotkey;
            text.Text = GuiToProject[i].Name;

            // Some special handling for the current one
            area.Height = isCurrent ? 54 : 40;
            hotkey.Foreground = isCurrent ? (Parent.Resources["ColorTextFore"] as SolidColorBrush) : (Parent.Resources["ColorTextBack"] as SolidColorBrush);
            text.Foreground = isCurrent ? (Parent.Resources["ColorTextFore"] as SolidColorBrush) : (Parent.Resources["ColorTextBack"] as SolidColorBrush);
            text.FontSize = isCurrent ? 20 : 12;
         }
      }

      private Storyboard? _StoryboardCommand;
      void _AnimateCommand(int guiIndex)
      {
         if (Parent._Finished)
            return;
         Parent._Finished = true;

         Panel grid = ProjectGui[guiIndex];
         Grid area = grid.FindElementByName<Grid>("Area");

         _StoryboardCommand = new Storyboard();
         {
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.8f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFFF0F0FF), TimeSpan.FromSeconds(0.125f), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Extension.FromArgb(0xFF042508), TimeSpan.FromSeconds(0.25f), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            _StoryboardCommand.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, area);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Background.Color"));
         }

         _StoryboardCommand.Completed += (sender, e) =>
         {
            _StoryboardCommand.Remove(Parent);
            Parent._AnimateFadeOut();
         };
         _StoryboardCommand.Begin(Parent, HandoffBehavior.SnapshotAndReplace);

         App.PlayBeep(App.BeepSound.Accept);
      }

      public bool HandleKeyDown(object sender, KeyEventArgs e)
      {
         // Handle key entry.
         if ((e.Key == Key.D0 || e.Key == Key.NumPad0) && (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)))
         {
            List<string> idents = TimeManager.Instance.ProjectIdentToId.Keys.ToList();
            Parent.TimekeepTextEditor.Show((text) =>
            {
               int projectId = TimeManager.Instance.AddProject(text);
               SetActiveProject(TimeManager.Instance.IdentToProject[text]);
            }, idents);
         }
         else if (e.Key >= Key.A && e.Key <= Key.Z)
         {
            SetActiveTime(e.Key.ToString());
            return true;
         }
         else if (e.Key >= Key.D0 && e.Key <= Key.D9)
         {
            SetActiveTime(('0' + e.Key - Key.D0).ToString());
            return true;
         }
         else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
         {
            SetActiveTime(('0' + e.Key - Key.NumPad0).ToString());
            return true;
         }

         return false;
      }
   }
}
