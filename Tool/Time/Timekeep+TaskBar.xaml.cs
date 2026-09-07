using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tildetool.Time.Serialization;

namespace Tildetool.Time
{
   /// <summary>
   /// Interaction logic for Timekeep_TaskBar.xaml
   /// </summary>
   public partial class Timekeep_TaskBar : UserControl
   {
      public Timekeep_TaskBar()
      {
         InitializeComponent();
      }

      Timekeep Parent;
      DayTask[] ScheduleTasks;

      public void Collect(Timekeep parent)
      {
         Parent = parent;
         ScheduleTasks = TimeManager.Instance.TaskByDayOfWeek[(int)Parent.DailyDay.DayOfWeek];
      }

      class UiTask : DataTemplater
      {
         public Panel Background;
         public TextBlock Text;
         public TextBlock Duration;
         public UiTask(FrameworkElement root) : base(root) { }
      }

      public void Refresh()
      {
         Parent.DayTaskPane.Visibility = ScheduleTasks.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
         if (ScheduleTasks.Length == 0)
            return;

         DataTemplate? templateSchedule = Resources["Task"] as DataTemplate;
         DataTemplater.Populate<DayTask, UiTask>(Tasks, templateSchedule, ScheduleTasks, (ui, index, task) =>
         {
            //(ui.Root as Panel).Background = new SolidColorBrush(task.Color.Alpha(0x20));
            //ui.ScheduleTextL.Background = new SolidColorBrush(task.Color.Lerp(Extension.FromArgb(0x40C3F1AF), 0.75f));
            ui.Text.Text = task.Name;
            ui.Duration.Text = task.DurationM > 0 ? $"{task.DurationM}m" : "";
         });
      }
   }
}
