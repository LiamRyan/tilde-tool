﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using System.Diagnostics;
using System.Timers;
using Tildetool.Explorer.Serialization;
using System.Threading;
using Windows.Storage;
using System.Windows.Interop;

namespace Tildetool.Explorer
{
   /// <summary>
   /// Interaction logic for ExplorerItemPopup.xaml
   /// </summary>
   public partial class ExplorerItemPopup : Window
   {
      MediaPlayer _MediaPlayer = new MediaPlayer();

      #region WinAPI

      [DllImport("user32.dll")]
      static extern IntPtr GetForegroundWindow();

      [DllImport("user32.dll")]
      static extern bool ShowWindow(IntPtr handle, int nCmdShow);

      [DllImport("user32.dll")]
      static extern bool SetForegroundWindow(IntPtr hWnd);

      [DllImport("user32.dll")]
      static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

      const int SW_SHOW = 5;  // Activates the window and displays it in its current size and position.

      public static int VK_NUMLOCK = 0x90;
      public static int VK_SCROLL = 0x91;
      public static int VK_CAPITAL = 0x14;
      public static int KEYEVENTF_EXTENDEDKEY = 0x0001; // If specified, the scan code was preceded by a prefix byte having the value 0xE0 (224).
      public static int KEYEVENTF_KEYUP = 0x0002; // If specified, the key is being released. If not specified, the key is being depressed.

      [DllImport("User32.dll", SetLastError = true)]
      public static extern void keybd_event(
          byte bVk,
          byte bScan,
          int dwFlags,
          IntPtr dwExtraInfo);

      #endregion
      #region Events

      public delegate void PopupEvent(object sender);
      public event PopupEvent? OnFinish;

      #endregion
      #region Spawning

      Grid[]? _Options;
      ExplorerCommand[]? _OptionFns = null;

      bool AsFile = true;
      List<ExplorerCommand> _IterAction(bool file, bool folder)
      {
         string extension = "";
         if (FilePath != null)
         {
            int index = FilePath.LastIndexOf('.');
            extension = FilePath.Substring(index + 1);
         }

         List<ExplorerCommand> actions = new List<ExplorerCommand>(4);
         List<ExplorerCommand> extActions;
         if (ExplorerManager.Instance.CommandByExt.TryGetValue(extension, out extActions))
            actions.AddRange(extActions.Where(e => e.AsFile == file || !e.AsFile == folder));
         if (ExplorerManager.Instance.CommandByExt.TryGetValue("*", out extActions))
            actions.AddRange(extActions.Where(e => e.AsFile == file || !e.AsFile == folder));

         return actions;
      }
      void _Populate()
      {
         if (string.IsNullOrEmpty(FilePath))
            AsFile = false;
         List<ExplorerCommand> innerActions = _IterAction(AsFile, !AsFile);
         List<ExplorerCommand> outerActions = _IterAction(!AsFile, AsFile);
         _SetArray(innerActions, outerActions);

         OptionIcon.Visibility = ((AsFile && FileIcon != null) || (!AsFile && FolderIcon != null)) ? Visibility.Visible : Visibility.Hidden;
         if (AsFile)
         {
            if (FileIcon != null)
               OptionIcon.Source = Imaging.CreateBitmapSourceFromHIcon(FileIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            OptionName.Text = FilePath.Split('\\', '/').Last();
         }
         else
         {
            if (FolderIcon != null)
               OptionIcon.Source = Imaging.CreateBitmapSourceFromHIcon(FolderIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            OptionName.Text = FolderPath.Split('\\', '/').Last();
         }
      }

      void _SetArray(List<ExplorerCommand> innerActions, List<ExplorerCommand> outerActions)
      {
         _Options = new Grid[innerActions.Count];
         double angleDelta = 360.0 / Math.Max(innerActions.Count, 2);
         for (int i = 0; i < innerActions.Count; i++)
         {
            // Spawn the control and apply the templates so child controls are created.
            ContentControl content = new ContentControl { ContentTemplate = Resources["OptionTemplate"] as DataTemplate };
            OptionGrid.Children.Add(content);
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();

            // Find our elements.
            _Options[i] = VisualTreeHelper.GetChild(presenter, 0) as Grid;
            Shape.Arc arc2 = _Options[i].FindElementByName<Shape.Arc>("Arc2");
            Shape.Arc arc = _Options[i].FindElementByName<Shape.Arc>("Arc");
            TextBlock text = _Options[i].FindElementByName<TextBlock>("Text");
            TextBlock hotkey = _Options[i].FindElementByName<TextBlock>("Hotkey");

            // Resize based on the number of elements.
            arc.StartAngle = 90.0 - ((angleDelta - 10.0) / 2.0);
            arc2.StartAngle = 89.0 - ((angleDelta - 10.0) / 2.0);
            arc.EndAngle = 90.0 + ((angleDelta - 10.0) / 2.0);
            arc2.EndAngle = 91.0 + ((angleDelta - 10.0) / 2.0);

            // Rotate to the correct orientation.
            double angle = angleDelta * i;
            _Options[i].LayoutTransform = new RotateTransform { Angle = angle };
            text.LayoutTransform = new RotateTransform { Angle = (angle > 90.0 && angle < 270.0) ? -180.0 : 0.0 };
            hotkey.RenderTransform = new RotateTransform { Angle = -(angleDelta - 20.0) / 2.0 };

            // Set text.
            text.Text = innerActions[i].Title;
            hotkey.Text = innerActions[i].Hotkey.ToLower();
         }

         // Store the callbacks for the options.
         _OptionFns = innerActions.ToArray();
      }

      #endregion
      #region Animation

      private Storyboard? _StoryboardAppear;
      private Storyboard? _StoryboardExit;

      void _AnimateAppear()
      {
         _StoryboardAppear = new Storyboard();
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardAppear.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Grid.OpacityProperty));
         }
         if (false)
         {
            var myDoubleAnimation = new DoubleAnimation();
            myDoubleAnimation.From = 0.0;
            myDoubleAnimation.To = 1.0;
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            _StoryboardAppear.Children.Add(myDoubleAnimation);
            Storyboard.SetTarget(myDoubleAnimation, RootFrame);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Image.OpacityProperty));
         }

         double deltaAngle = Math.PI * 2.0 / _Options.Length;
         for (int i = 0; i < _Options.Length; i++)
         {
            _Options[i].RenderTransform = new TranslateTransform();
            Point fromDelta = new Point(Math.Sin(i * deltaAngle), Math.Cos(i * deltaAngle));

            {
               var myDoubleAnimation = new DoubleAnimation();
               myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.3f));
               myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 3.0f, EasingMode = EasingMode.EaseOut };
               myDoubleAnimation.From = 100.0 * -fromDelta.X; myDoubleAnimation.To = 0.0;
               _StoryboardAppear.Children.Add(myDoubleAnimation);
               Storyboard.SetTarget(myDoubleAnimation, _Options[i]);
               Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath("RenderTransform.X"));
            }
            {
               var myDoubleAnimation = new DoubleAnimation();
               myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.3f));
               myDoubleAnimation.EasingFunction = new ExponentialEase { Exponent = 3.0f, EasingMode = EasingMode.EaseOut };
               myDoubleAnimation.From = 100.0 * fromDelta.Y; myDoubleAnimation.To = 0.0;
               _StoryboardAppear.Children.Add(myDoubleAnimation);
               Storyboard.SetTarget(myDoubleAnimation, _Options[i]);
               Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath("RenderTransform.Y"));
            }
         }

         _StoryboardAppear.Completed += (sender, e) => { if (_StoryboardAppear != null) { _StoryboardAppear.Remove(this); _StoryboardAppear = null; } };
         _StoryboardAppear.Begin(this);
      }
      void _AnimateFadeOne(Storyboard parent, int index, double beginTime = 0.0, double duration = 0.2)
      {
         var fadeAnimation = new DoubleAnimation();
         fadeAnimation.To = 0.0;
         fadeAnimation.BeginTime = TimeSpan.FromSeconds(beginTime);
         fadeAnimation.Duration = new Duration(TimeSpan.FromSeconds(duration));
         parent.Children.Add(fadeAnimation);
         Storyboard.SetTarget(fadeAnimation, _Options[index]);
         Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath(Grid.OpacityProperty));

         double deltaAngle = Math.PI * 2.0 / _Options.Length;
         Point fromDelta = new Point(Math.Sin(index * deltaAngle), Math.Cos(index * deltaAngle));
         _Options[index].RenderTransform = new TranslateTransform();

         {
            DoubleAnimation moveAnimation = new DoubleAnimation();
            moveAnimation.BeginTime = TimeSpan.FromSeconds(beginTime);
            moveAnimation.Duration = new Duration(TimeSpan.FromSeconds(duration));
            moveAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            parent.Children.Add(moveAnimation);
            Storyboard.SetTarget(moveAnimation, _Options[index]);
            Storyboard.SetTargetProperty(moveAnimation, new PropertyPath("RenderTransform.X"));
            moveAnimation.To = 50.0 * fromDelta.X;
         }
         {
            DoubleAnimation moveAnimation = new DoubleAnimation();
            moveAnimation.BeginTime = TimeSpan.FromSeconds(beginTime);
            moveAnimation.Duration = new Duration(TimeSpan.FromSeconds(duration));
            moveAnimation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            parent.Children.Add(moveAnimation);
            Storyboard.SetTarget(moveAnimation, _Options[index]);
            Storyboard.SetTargetProperty(moveAnimation, new PropertyPath("RenderTransform.Y"));
            moveAnimation.To = 50.0 * -fromDelta.Y;
         }
      }
      FrameworkElement FindChild(Grid element, string name)
      {
         foreach (FrameworkElement child in element.Children)
            if (child.Name == name)
               return child;
         return null;
      }
      void _AnimateCancel(bool closeAtEnd)
      {
         if (_StoryboardAppear != null)
         {
            _StoryboardAppear.Stop(this);
            _StoryboardAppear.Remove(this);
            _StoryboardAppear = null;
         }

         _StoryboardExit = new Storyboard();
         for (int i = 0; i < _Options.Length; i++)
            _AnimateFadeOne(_StoryboardExit, i);

         if (closeAtEnd)
            _StoryboardExit.Completed += (sender, e) => { Dispatcher.BeginInvoke(Close); };
         else
            _StoryboardExit.Completed += (sender, e) => { if (_StoryboardExit == sender) { _StoryboardExit.Stop(this); _StoryboardExit.Remove(this); _StoryboardExit = null; } };
         _StoryboardExit.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      void _AnimateSelect(int index)
      {
         if (_StoryboardAppear != null)
         {
            _StoryboardAppear.Stop(this);
            _StoryboardAppear.Remove(this);
            _StoryboardAppear = null;
         }

         _StoryboardExit = new Storyboard();
         for (int i = 0; i < _Options.Length; i++)
            if (i != index)
               _AnimateFadeOne(_StoryboardExit, i);

         if (index != -1)
         {
            Shape.Arc arc = _Options[index].FindElementByName<Shape.Arc>("Arc");
            var flashAnimation = new ColorAnimationUsingKeyFrames();
            flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
            flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xCC, 0x00, 0x00, 0x00), KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            _StoryboardExit.Children.Add(flashAnimation);
            Storyboard.SetTarget(flashAnimation, arc);
            Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Stroke.Color"));

            _AnimateFadeOne(_StoryboardExit, index, 0.2, 0.3);
         }

         _StoryboardExit.Completed += (sender, e) => { Dispatcher.BeginInvoke(Close); };
         _StoryboardExit.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      #endregion

      bool WasNumlock;
      public ExplorerItemPopup()
      {
         // Grab the window that was selected before we opened.
         WasNumlock = Keyboard.IsKeyToggled(Key.NumLock);
         if (!WasNumlock)
         {
            keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, IntPtr.Zero);
         }

         InitializeComponent();

         OptionGrid.Children.Clear();

         App.PlayBeep(App.BeepSound.Wake);
      }
      protected override void OnLostFocus(RoutedEventArgs e)
      {
         base.OnLostFocus(e);
         Cancel();
      }
      protected override void OnClosed(EventArgs e)
      {
         base.OnClosed(e);

         // Closing our window can send the child process to the back, so force it to the
         //  front manually now that we are closed.
         Process process = ResultProcess;
         if (process != null)
         {
            IntPtr handle = IntPtr.Zero;
            try
            {
               handle = process.MainWindowHandle;
            }
            catch { }
            if (handle != IntPtr.Zero)
            {
               ShowWindow(handle, SW_SHOW);
               SetForegroundWindow(handle);
            }
            process.Dispose();
            ResultProcess = null;
         }
         if (FileIcon != null)
         {
            FileIcon.Dispose();
            FileIcon = null;
         }
         if (FolderIcon != null)
         {
            FolderIcon.Dispose();
            FolderIcon = null;
         }
      }
      void _Finish()
      {
         if (!WasNumlock)
         {
            keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, IntPtr.Zero);
         }

         _Finished = true;
         OnFinish?.Invoke(this);
      }

      public Process? ResultProcess = null;

      static IntPtr Handle;
      public static string? FolderPath;
      public static string? FilePath;
      public static string[]? AllFilePaths;
      public static System.Drawing.Icon? FolderIcon;
      public static System.Drawing.Icon? FileIcon;
      public static bool LoadFolderData()
      {
         // Get information about the folder and selected files
         Handle = GetForegroundWindow();
         FolderPath = null;
         FilePath = null;
         AllFilePaths = null;
         FolderIcon = null;
         FileIcon = null;

         List<string> selected = new List<string>();
         Shell32.Shell shell = new Shell32.Shell();
         foreach (SHDocVw.InternetExplorer window in shell.Windows())
         {
            if (window.HWND != (int)Handle)
               continue;
            var shellWindow = window.Document as Shell32.ShellFolderView;
            if (shellWindow == null)
               continue;

            FolderPath = new Uri(window.LocationURL).LocalPath;
            try
            {
               FolderIcon = System.Drawing.Icon.ExtractAssociatedIcon(FolderPath);
            }
            catch (Exception ex)
            {
               App.WriteLog(ex.ToString());
            }

            Shell32.FolderItems items = shellWindow.SelectedItems();
            if (items.Count > 0)
            {
               FilePath = items.Item(0).Path;
               try
               {
                  FileIcon = System.Drawing.Icon.ExtractAssociatedIcon(FilePath);
               }
               catch (Exception ex)
               {
                  App.WriteLog(ex.ToString());
               }
            }

            AllFilePaths = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
               AllFilePaths[i] = items.Item(i).Path;
            return true;
         }
         return false;
      }

      protected override void OnSourceInitialized(EventArgs e)
      {
         base.OnSourceInitialized(e);

         _Populate();
         _AnimateAppear();
      }

      System.Timers.Timer? _SpawnTimer = null;
      public void WaitForSpawn()
      {
         _SpawnTimer = new System.Timers.Timer();
         _SpawnTimer.Interval = 50;
         _SpawnTimer.Elapsed += (s, e) =>
         {
            Process process = ResultProcess;
            if (process == null)
            {
               _SpawnTimer.Stop();
               _SpawnTimer.Dispose();
               _SpawnTimer = null;
               return;
            }
            IntPtr handle = IntPtr.Zero;
            try
            {
               handle = process.MainWindowHandle;
            }
            catch { }
            if (handle != IntPtr.Zero)
            {
               ShowWindow(handle, SW_SHOW);
               SetForegroundWindow(handle);
               Close();

               _SpawnTimer.Stop();
               _SpawnTimer.Dispose();
               _SpawnTimer = null;
            }
         };
         _SpawnTimer.Start();
      }

      bool _Finished = false;
      public void Cancel()
      {
         if (_Finished)
            return;

         _Finish();
         _AnimateCancel(true);
         App.PlayBeep(App.BeepSound.Cancel);
      }

      Dictionary<Key, int>[] KeyToIndex =
         {
            new Dictionary<Key, int>{ { Key.NumPad8, 0 } },
            new Dictionary<Key, int>{ { Key.NumPad8, 0 }, { Key.NumPad2, 1 } },
            new Dictionary<Key, int>{ { Key.NumPad8, 0 }, { Key.NumPad6, 1 },{ Key.NumPad3, 1 }, { Key.NumPad4, 2 },{ Key.NumPad1, 2 } },
            new Dictionary<Key, int>{ { Key.NumPad8, 0 }, { Key.NumPad6, 1 }, { Key.NumPad2, 2 }, { Key.NumPad4, 3 } },
            new Dictionary<Key, int>{ { Key.NumPad8, 0 }, { Key.NumPad6, 1 }, { Key.NumPad3, 2 }, { Key.NumPad1, 3 }, { Key.NumPad4, 4 } },
            new Dictionary<Key, int>{ { Key.NumPad8, 0 }, { Key.NumPad9, 1 }, { Key.NumPad3, 2 }, { Key.NumPad2, 3 }, { Key.NumPad1, 4 }, { Key.NumPad7, 5 } },
            new Dictionary<Key, int>{ { Key.NumPad8, 0 }, { Key.NumPad9, 1 }, { Key.NumPad6, 2 }, { Key.NumPad3, 3 }, { Key.NumPad1, 4 }, { Key.NumPad4, 5 }, { Key.NumPad7, 6 } },
            new Dictionary<Key, int>{ { Key.NumPad8, 0 }, { Key.NumPad9, 1 }, { Key.NumPad6, 2 }, { Key.NumPad3, 3 }, { Key.NumPad2, 4 }, { Key.NumPad1, 5 }, { Key.NumPad4, 6 }, { Key.NumPad7, 7 } },
         };

      private void Window_KeyDown(object sender, KeyEventArgs e)
      {
         if (_Finished)
            return;

         // Check if it's escape, and close if so.
         switch (e.Key)
         {
            case Key.Return:
               Process process = new Process();
               ProcessStartInfo startInfo = new ProcessStartInfo();
               startInfo.FileName = System.IO.Directory.GetCurrentDirectory() + "\\Explorer.json";
               startInfo.UseShellExecute = true;
               process.StartInfo = startInfo;
               process.Start();
               Cancel();
               return;

            case Key.Escape:
               e.Handled = true;
               Cancel();
               return;

            case Key.NumPad0:
               if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
               {
                  e.Handled = true;
                  if (!string.IsNullOrEmpty(FilePath))
                  {
                     AsFile = !AsFile;
                     _AnimateCancel(false);
                     _Populate();
                     _AnimateAppear();
                  }
                  return;
               }
               break;
         }

         // Check if it's a command key.
         ExplorerCommand? action = null;
         Dictionary<Key, int> keyToInt = KeyToIndex[_Options.Length - 1];
         int index;
         if (keyToInt.TryGetValue(e.Key, out index))
            action = _OptionFns[index];

         if (action == null)
         {
            List<ExplorerCommand> actions = _IterAction(AsFile, !AsFile);
            foreach (ExplorerCommand testAction in actions)
               if (testAction.HotkeyAsKey == e.Key)
               {
                  action = testAction;
                  break;
               }
            if (action == null)
            {
               actions = _IterAction(!AsFile, AsFile);
               foreach (ExplorerCommand testAction in actions)
                  if (testAction.HotkeyAsKey == e.Key)
                  {
                     action = testAction;
                     break;
                  }
            }
         }

         if (action != null)
         {
            index = -1;
            for (int i = 0; i < _OptionFns.Length; i++)
               if (_OptionFns[i] == action)
               {
                  index = i;
                  break;
               }

            // Call the function.
            if (action.Command.Equals("{COPY}"))
            {
               if (action.AsFile)
                  Clipboard.SetText(String.Join("\n", AllFilePaths));
               else
                  Clipboard.SetText(FolderPath);
            }
            else
            {
               Thread trd = new Thread(new ThreadStart(() =>
               {
                  try
                  {
                     string path = action.AsFile ? FilePath : FolderPath;
                     ResultProcess = new Process();
                     ProcessStartInfo startInfo = new ProcessStartInfo();
                     startInfo.FileName = action.Command;
                     if (action.InWorkingDir)
                        startInfo.WorkingDirectory = path;
                     else
                        startInfo.Arguments = path;
                     ResultProcess.StartInfo = startInfo;
                     ResultProcess.Start();
                  }
                  catch (Exception ex)
                  {
                     MessageBox.Show(ex.ToString());
                     App.WriteLog(ex.ToString());
                  }
               }));
               trd.IsBackground = true;
               trd.Start();

               WaitForSpawn();
            }

            // Finish up.
            e.Handled = true;
            _Finish();
            _AnimateSelect(index);
            App.PlayBeep(App.BeepSound.Accept);
            return;
         }
      }
   }
}
