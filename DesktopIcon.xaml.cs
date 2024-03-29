﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using VirtualDesktopApi;

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
         Width = System.Windows.SystemParameters.PrimaryScreenWidth;
         InitializeComponent();
         Top = App.GetBarTop(Height);

         _ShowDesktop(VirtualDesktop.Current, false);

         UpdateLayout();

         _AnimateIn();

         VirtualDesktop.CurrentChanged += VirtualDesktop_CurrentChanged;
         _Timer = new Timer { Interval = 700 };
         _Timer.Elapsed += (o, e) => { Dispatcher.Invoke(() => _AnimateOut()); };
         _Timer.Start();
      }
      void OnLoaded(object sender, RoutedEventArgs args)
      {
         App.PreventAltTab(this);
         App.Clickthrough(this);
      }
      protected override void OnSourceInitialized(EventArgs e)
      {
         base.OnSourceInitialized(e);

         this.MoveToDesktop(VirtualDesktop.Current);
      }
      protected override void OnClosing(CancelEventArgs e)
      {
         base.OnClosing(e);
         VirtualDesktop.CurrentChanged -= VirtualDesktop_CurrentChanged;
      }

      private Storyboard? _StoryboardShow;
      protected void _ShowDesktop(VirtualDesktop current, bool animate)
      {
         // Make sure we have the right number of name lines
         VirtualDesktop[] desktops = new VirtualDesktop[0];// VirtualDesktop.GetDesktops().ToArray();
         while (Border.Children.Count > desktops.Length)
            Border.Children.RemoveAt(Border.Children.Count - 1);

         DataTemplate? template = Resources["DesktopName"] as DataTemplate;
         while (Border.Children.Count < desktops.Length)
         {
            ContentControl content = new ContentControl { ContentTemplate = template };
            Border.Children.Add(content);
            Grid.SetRow(content, Border.Children.Count);
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
         }

         if (animate)
         {
            if (_StoryboardShow != null)
               _StoryboardShow.Stop(this);
            _StoryboardShow = new Storyboard();
         }

         // Set them all up
         for (int i = 0; i < desktops.Length; i++)
         {
            ContentControl content = Border.Children[i] as ContentControl;
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();
            TextBlock text = VisualTreeHelper.GetChild(presenter, 0) as TextBlock;

            // Make sure it's the right name.
            if (!string.IsNullOrEmpty(desktops[i].Name))
               text.Text = desktops[i].Name;
            else
               text.Text = "Desktop " + (i + 1).ToString();

            // Change the appearance.
            bool isCurrent = (desktops[i] == current);
            Color foregroundColor = isCurrent ? Extension.FromArgb(0xFFC3F1AF) : Extension.FromArgb(0xFF449637);
            double fontSize = isCurrent ? 24 : 12;
            Thickness margin = isCurrent ? new Thickness(9, 0, 9, 0) : new Thickness(6, 8, 6, 0);
            if (animate)
            {
               {
                  var animation = new ColorAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(isCurrent ? 0.2f : 0.4f));
                  animation.To = foregroundColor;
                  _StoryboardShow.Children.Add(animation);
                  Storyboard.SetTarget(animation, text);
                  Storyboard.SetTargetProperty(animation, new PropertyPath("Foreground.Color"));
               }
               {
                  var animation = new DoubleAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(0.35f));
                  animation.To = fontSize;
                  animation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
                  _StoryboardShow.Children.Add(animation);
                  Storyboard.SetTarget(animation, text);
                  Storyboard.SetTargetProperty(animation, new PropertyPath(TextBlock.FontSizeProperty));
               }
               {
                  var animation = new ThicknessAnimation();
                  animation.Duration = new Duration(TimeSpan.FromSeconds(0.35f));
                  animation.To = margin;
                  animation.EasingFunction = new ExponentialEase { Exponent = 6.0, EasingMode = EasingMode.EaseOut };
                  _StoryboardShow.Children.Add(animation);
                  Storyboard.SetTarget(animation, text);
                  Storyboard.SetTargetProperty(animation, new PropertyPath(TextBlock.MarginProperty));
               }
            }
            else
            {
               text.Foreground = new SolidColorBrush(foregroundColor);
               text.FontSize = fontSize;
               text.Margin = margin;
            }
         }

         if (animate)
         {
            _StoryboardShow.Completed += (sender, e) => { if (_StoryboardShow != null) _StoryboardShow.Remove(this); _StoryboardShow = null; };
            _StoryboardShow.Begin(this, HandoffBehavior.SnapshotAndReplace);
         }
      }

      private void VirtualDesktop_CurrentChanged(VirtualDesktop oldDesktop, VirtualDesktop newDesktop)
      {
         Dispatcher.Invoke(() =>
         {
            _ShowDesktop(newDesktop, true);
            UpdateLayout();
            this.MoveToDesktop(newDesktop);

            if (!_Finished)
            {
               if (_Timer != null)
               {
                  _Timer.Stop();
                  _Timer.Dispose();
               }
               _Timer = new Timer { Interval = 700 };
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
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = new Thickness(-10, 10, -10, 10);
            animation.To = new Thickness(-10, 0, -10, 0);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseIn };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         {
            var animation = new ColorAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.0f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.To = Extension.FromArgb(0xFF021204);
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseInOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Fill.Color"));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 8.0f;
            animation.To = 2.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            animation.From = 8.0f;
            animation.To = 2.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.33f));
            Content.Height = 6.0f;
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(6.0f, TimeSpan.FromSeconds(0)));
            //animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height / 2, TimeSpan.FromSeconds(0.2f), new ExponentialEase { Exponent = 2.0, EasingMode = EasingMode.EaseIn }));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(Height, TimeSpan.FromSeconds(0.33f), new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut }));
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
            Border.Opacity = 0.0f;
            animation.To = 1.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.2f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.13f));
            animation.To = 1.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardFade.Completed += (sender, e) => { if (_StoryboardFade != null) _StoryboardFade.Remove(this); _StoryboardFade = null; };
         _StoryboardFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      void _AnimateOut()
      {
         if (_Finished)
            return;
         _Finished = true;

         _Timer.Stop();
         _Timer.Dispose();
         _Timer = null;

         OnFinish?.Invoke(this);

         _StoryboardFade = new Storyboard();
         {
            var animation = new ThicknessAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = new Thickness(-10, 10, -10, 10);
            animation.EasingFunction = new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Backfill);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.MarginProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Border);
            Storyboard.SetTargetProperty(animation, new PropertyPath(StackPanel.OpacityProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 8.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow1);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 8.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Glow2);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Rectangle.StrokeThicknessProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.5f));
            animation.To = 6.0f;
            animation.EasingFunction = new ExponentialEase { Exponent = 4.0, EasingMode = EasingMode.EaseOut };
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, Content);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Grid.HeightProperty));
         }
         {
            var animation = new DoubleAnimation();
            animation.BeginTime = TimeSpan.FromSeconds(0.25f);
            animation.Duration = new Duration(TimeSpan.FromSeconds(0.15f));
            animation.To = 0.0f;
            _StoryboardFade.Children.Add(animation);
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Window.OpacityProperty));
         }
         _StoryboardFade.Completed += (sender, e) => { if (_StoryboardFade != null) _StoryboardFade.Remove(this); _StoryboardFade = null; Close(); };
         _StoryboardFade.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
   }
}
