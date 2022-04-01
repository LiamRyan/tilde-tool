using System;
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

namespace Tildetool
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

      #endregion
      #region Events

      public delegate void PopupEvent(object sender);
      public event PopupEvent? OnFinish;

      #endregion
      #region Spawning

      Grid[]? _Options;
      System.Action<int>[]? _OptionFns = null;

      void _SetArray(string[] names, System.Action<int>[] optionFns)
      {
         if (names.Length != optionFns.Length)
            throw new ArgumentException("names length must match optionFns length");

         _Options = new Grid[names.Length];
         double angleDelta = 360.0 / names.Length;
         for (int i = 0; i < names.Length; i++)
         {
            // Spawn the control and apply the templates so child controls are created.
            ContentControl content = new ContentControl { ContentTemplate = Resources["OptionTemplate"] as DataTemplate };
            OptionGrid.Children.Add(content);
            content.ApplyTemplate();
            ContentPresenter presenter = VisualTreeHelper.GetChild(content, 0) as ContentPresenter;
            presenter.ApplyTemplate();

            // Find our elements.
            _Options[i] = VisualTreeHelper.GetChild(presenter, 0) as Grid;
            Shape.Arc arc = _Options[i].FindElementByName<Shape.Arc>("Arc");
            TextBlock text = _Options[i].FindElementByName<TextBlock>("Text");

            // Resize based on the number of elements.
            arc.StartAngle = 90.0 - ((angleDelta - 10.0) / 2.0);
            arc.EndAngle = 90.0 + ((angleDelta - 10.0) / 2.0);

            // Rotate to the correct orientation.
            double angle = angleDelta * i;
            _Options[i].LayoutTransform = new RotateTransform { Angle = angle };
            text.LayoutTransform = new RotateTransform { Angle = (angle > 90.0 && angle < 270.0) ? -180.0 : 0.0 };

            // Set text.
            text.Text = names[i];
         }

         // Store the callbacks for the options.
         _OptionFns = optionFns;
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
            Storyboard.SetTarget(myDoubleAnimation, RootWindow);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Window.OpacityProperty));
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

         _StoryboardAppear.Completed += (sender, e) => { if (_StoryboardAppear != null) { _StoryboardAppear.Remove(); _StoryboardAppear = null; } };
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
      void _AnimateCancel()
      {
         if (_StoryboardAppear != null)
         {
            _StoryboardAppear.Stop();
            _StoryboardAppear.Remove();
            _StoryboardAppear = null;
         }

         _StoryboardExit = new Storyboard();
         for (int i = 0; i < _Options.Length; i++)
            _AnimateFadeOne(_StoryboardExit, i);

         _StoryboardExit.Completed += (sender, e) => { Dispatcher.BeginInvoke(Close); };
         _StoryboardExit.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }
      void _AnimateSelect(int index)
      {
         if (_StoryboardAppear != null)
         {
            _StoryboardAppear.Stop();
            _StoryboardAppear.Remove();
            _StoryboardAppear = null;
         }

         _StoryboardExit = new Storyboard();
         for (int i = 0; i < _Options.Length; i++)
            if (i != index)
               _AnimateFadeOne(_StoryboardExit, i);

         Shape.Arc arc = _Options[index].FindElementByName<Shape.Arc>("Arc");
         var flashAnimation = new ColorAnimationUsingKeyFrames();
         flashAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.2f));
         flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xFF), KeyTime.FromPercent(0.5), new ExponentialEase { Exponent = 3.0, EasingMode = EasingMode.EaseOut }));
         flashAnimation.KeyFrames.Add(new EasingColorKeyFrame(Color.FromArgb(0xCC, 0x00, 0x00, 0x00), KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
         _StoryboardExit.Children.Add(flashAnimation);
         Storyboard.SetTarget(flashAnimation, arc);
         Storyboard.SetTargetProperty(flashAnimation, new PropertyPath("Stroke.Color"));

         _AnimateFadeOne(_StoryboardExit, index, 0.2, 0.3);

         _StoryboardExit.Completed += (sender, e) => { Dispatcher.BeginInvoke(Close); };
         _StoryboardExit.Begin(this, HandoffBehavior.SnapshotAndReplace);
      }

      #endregion

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

      IntPtr Handle;
      bool WasNumlock;
      public ExplorerItemPopup()
      {
         // Grab the window that was selected before we opened.
         Handle = GetForegroundWindow();
         WasNumlock = Keyboard.IsKeyToggled(Key.NumLock);
         if (!WasNumlock)
         {
            keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY, IntPtr.Zero);
            keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, IntPtr.Zero);
         }

         InitializeComponent();

         _MediaPlayer.Open(new Uri("Resource\\beepG.mp3", UriKind.Relative));
         _MediaPlayer.Play();
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
         if (_ResultProcess != null && _ResultProcess.MainWindowHandle != IntPtr.Zero)
         {
            ShowWindow(_ResultProcess.MainWindowHandle, SW_SHOW);
            SetForegroundWindow(_ResultProcess.MainWindowHandle);
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

      System.Diagnostics.Process? _ResultProcess = null;
      protected override void OnSourceInitialized(EventArgs e)
      {
         base.OnSourceInitialized(e);

         _SetArray(new string[] { "Command Prompt", "Copy Path", "Grep" },
                   new Action<int>[] {
                     (index) =>
                     {
                        string folderPath, filePath;
                        GetFolderData(out folderPath, out filePath);
                        _ResultProcess = new System.Diagnostics.Process();
                        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                        startInfo.FileName = "cmd.exe";
                        startInfo.WorkingDirectory = folderPath;
                        _ResultProcess.StartInfo = startInfo;
                        _ResultProcess.Start();
                     },
                     (index) =>
                     {
                        string folderPath, filePath;
                        GetFolderData(out folderPath, out filePath);
                        Clipboard.SetText(filePath);
                     },
                     (index) =>
                     {
                        string folderPath, filePath;
                        GetFolderData(out folderPath, out filePath);
                        _ResultProcess = new System.Diagnostics.Process();
                        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                        startInfo.FileName = "D:\\Program Files\\grepWin\\grepWin.exe";
                        startInfo.Arguments = folderPath;
                        _ResultProcess.StartInfo = startInfo;
                        _ResultProcess.Start();
                     },
                   });

         _AnimateAppear();
      }
      bool _Finished = false;
      public void Cancel()
      {
         if (_Finished)
            return;

         _Finish();
         _AnimateCancel();
         _MediaPlayer.Open(new Uri("Resource\\beepA.mp3", UriKind.Relative));
         _MediaPlayer.Play();
      }

      void GetFolderData(out string folderPath, out string filePath)
      {
         folderPath = null;
         filePath = null;

         List<string> selected = new List<string>();
         Shell32.Shell shell = new Shell32.Shell();
         foreach (SHDocVw.InternetExplorer window in shell.Windows())
         {
            if (window.HWND != (int)Handle)
               continue;
            var shellWindow = window.Document as Shell32.ShellFolderView;
            if (shellWindow == null)
               continue;

            folderPath = new Uri(window.LocationURL).LocalPath;

            //var currentFolder = shellWindow.Folder.Items().Item();
            //return currentFolder.Path;

            Shell32.FolderItems items = shellWindow.SelectedItems();
            if (items.Count > 0)
               filePath = items.Item(0).Path;
            break;
         }
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
            case Key.Escape:
               e.Handled = true;
               Cancel();
               return;
         }

         // Check if it's a command key.
         Dictionary<Key, int> keyToInt = KeyToIndex[_Options.Length - 1];
         int index;
         if (keyToInt.TryGetValue(e.Key, out index))
         {
            // Call the function.
            try
            {
               _OptionFns[index](index);
            }
            catch (Exception ex)
            {
               Console.WriteLine(ex.ToString());
            }

            // Finish up.
            e.Handled = true;
            _Finish();
            _AnimateSelect(index);
            _MediaPlayer.Open(new Uri("Resource\\beepC.mp3", UriKind.Relative));
            _MediaPlayer.Play();
            return;
         }
      }
   }
}
