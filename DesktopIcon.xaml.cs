using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WindowsDesktop;

namespace Tildetool
{
   /// <summary>
   /// Interaction logic for DesktopIcon.xaml
   /// </summary>
   public partial class DesktopIcon : Window
   {
      public delegate void PopupEvent(object sender);
      public event PopupEvent? OnFinish;

      public DesktopIcon()
      {
         InitializeComponent();

         Text.Text = VirtualDesktop.Current.Name;
         UpdateLayout();

         Left = (System.Windows.SystemParameters.PrimaryScreenWidth - Width) / 2;
         Top = 0;

         _AnimateIn();

         VirtualDesktop.CurrentChanged += VirtualDesktop_CurrentChanged;
         _Timer = new Timer { Interval = 2000 };
         _Timer.Elapsed += (o, e) => { Dispatcher.Invoke(() => _AnimateOut()); };
         _Timer.Start();
      }
      protected override void OnSourceInitialized(EventArgs e)
      {
         base.OnSourceInitialized(e);

         this.MoveToDesktop(VirtualDesktop.Current);
         Left = (System.Windows.SystemParameters.PrimaryScreenWidth - Width) / 2;
         Top = 0;
      }
      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);
         VirtualDesktop.CurrentChanged -= VirtualDesktop_CurrentChanged;
      }

      private void VirtualDesktop_CurrentChanged(object? sender, VirtualDesktopChangedEventArgs e)
      {
         Dispatcher.Invoke(() =>
         {
            Text.Text = e.NewDesktop.Name;
            UpdateLayout();
            this.MoveToDesktop(e.NewDesktop);
            Left = (System.Windows.SystemParameters.PrimaryScreenWidth - Width) / 2;

            if (!_Finished)
            {
               if (_Timer != null)
               {
                  _Timer.Stop();
                  _Timer.Dispose();
               }
               _Timer = new Timer { Interval = 2000 };
               _Timer.Elapsed += (o, e) => { Dispatcher.Invoke(() => _AnimateOut()); };
               _Timer.Start();
            }
         });
      }

      public void Cancel()
      {
         _AnimateOut();
      }

      Timer _Timer;
      bool _Finished = false;

      private Storyboard? _StoryboardFade;
      void _AnimateIn()
      {
         _StoryboardFade = new Storyboard();
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.From = 0.0f;
            animation.To = 1.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardFade.Completed += (sender, e) => { if (_StoryboardFade != null) _StoryboardFade.Remove(); _StoryboardFade = null; };
         _StoryboardFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      void _AnimateOut()
      {
         if (_Finished)
            return;
         _Finished = true;
         _Timer.Stop();

         OnFinish?.Invoke(this);

         _StoryboardFade = new Storyboard();
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardFade.Completed += (sender, e) => { if (_StoryboardFade != null) _StoryboardFade.Remove(); _StoryboardFade = null; _Timer.Dispose(); Close(); };
         _StoryboardFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
   }
}
