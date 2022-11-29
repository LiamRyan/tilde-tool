using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Tildetool.Hotcommand.Serialization;
using VirtualDesktopApi;
using static Tildetool.WindowsApi;

namespace Tildetool.Hotcommand
{
   class CommandRun
   {
      public bool Done = false;

      Dispatcher Dispatcher;
      CommandSpawn _Spawn;
      Process? _Process;
      System.Timers.Timer? _Timer;
      bool _DidSpawn = false;
      public CommandRun(CommandSpawn spawn, Dispatcher dispatcher)
      {
         Dispatcher = dispatcher;
         _Spawn = spawn;
         if (spawn.PauseSec > 0)
         {
            _Timer = new System.Timers.Timer();
            _Timer.Interval = (int)(1000 * spawn.PauseSec);
            _Timer.Elapsed += (s, e) =>
            {
               _Timer.Stop();
               _Timer.Dispose();
               _Timer = null;
               Execute(spawn);
            };
            _Timer.Start();
         }
         else
            Execute(spawn);
      }
      public void Dispose()
      {
         if (_Process != null)
         {
            _Process.Dispose();
            _Process = null;
         }
         if (_Timer != null)
         {
            _Timer.Stop();
            _Timer.Dispose();
            _Timer = null;
         }
         Done = true;
      }

      void Execute(CommandSpawn spawn)
      {
         bool wantSpawn = spawn.HasWindowParameter;
         Thread trd = new Thread(new ThreadStart(() =>
         {
            try
            {
               Process process = new Process();
               ProcessStartInfo startInfo = new ProcessStartInfo();
               if (!string.IsNullOrEmpty(spawn.ShellOpen))
               {
                  startInfo.UseShellExecute = true;
                  startInfo.FileName = spawn.ShellOpen;
               }
               else
                  startInfo.FileName = spawn.FileName;
               if (spawn.AsAdmin)
               {
                  startInfo.UseShellExecute = true;
                  startInfo.Verb = "runas";
               }
               if (spawn.ArgumentList != null && spawn.ArgumentList.Length > 0)
               {
                  foreach (string argument in spawn.ArgumentList)
                     startInfo.ArgumentList.Add(argument);
               }
               else
                  startInfo.Arguments = spawn.Arguments;
               startInfo.WorkingDirectory = spawn.WorkingDirectory;

               process.StartInfo = startInfo;
               process.Start();

               if (wantSpawn)
                  _Process = process;
               else
                  process.Dispose();
               _DidSpawn = true;

               if (wantSpawn)
                  Dispatcher.Invoke(Reposition);
            }
            catch (Exception ex)
            {
               MessageBox.Show(ex.ToString());
               App.WriteLog(ex.ToString());
            }
         }));
         trd.IsBackground = true;
         trd.Start();

         if (wantSpawn)
         {
            _Timer = new System.Timers.Timer();
            _Timer.Interval = 50;
            _Timer.Elapsed += (s, e) => Reposition();
            _Timer.Start();
         }
         else
            Dispose();
      }
      void Reposition()
      {
         if (!_DidSpawn)
            return;

         _Timer.Stop();

         // Our first step is to identify the window that will get spawned.  If
         //  we are told a specific window name to search for, wait for that.
         IntPtr windowHandle = IntPtr.Zero;
         bool isDone = false;
         if (!string.IsNullOrEmpty(_Spawn.WindowName))
         {
            bool EnumWindowsProc(IntPtr hWnd, int lParam)
            {
               StringBuilder windowText = new StringBuilder(MAXTITLE);
               int titleLength = GetWindowText(hWnd, windowText, windowText.Capacity + 1);
               windowText.Length = titleLength;
               string title = windowText.ToString();

               if (!string.IsNullOrEmpty(title) && IsWindowVisible(hWnd))
               {
                  if (title.ToUpper().IndexOf(_Spawn.WindowName.ToUpper()) >= 0)
                  {
                     windowHandle = hWnd;
                     return false;
                  }
               }
               return true;
            }
            EnumDelegate enumfunc = new EnumDelegate(EnumWindowsProc);
            EnumDesktopWindows(IntPtr.Zero, enumfunc, IntPtr.Zero);
         }
         // Otherwise, we wait until the process itself spawns a window handle.
         else
         {
            // Try to grab the window handle from the process.
            try
            {
               isDone = _Process.HasExited;
               if (!isDone)
                  windowHandle = _Process.MainWindowHandle;
               else
               {
                  App.WriteLog($"App exited before move: {_Spawn.FileName}");
                  MessageBox.Show($"Unable to manage process {_Spawn.FileName} / {_Spawn.ShellOpen}.\n\nApp exited before move: {_Spawn.FileName}");
               }
            }
            catch (Exception e)
            {
               // exception, show an error and give up.
               App.WriteLog("Process exception");
               App.WriteLog(e.ToString());
               MessageBox.Show($"Unable to manage process {_Spawn.FileName} / {_Spawn.ShellOpen}.\n\nProcess exception: {e.ToString()}");
               isDone = true;
            }
         }

         // Alright, handle it.
         if (!isDone && windowHandle != IntPtr.Zero)
         {
            try
            {
               // If we're going to move it, and it's minimized or maximized, go back to normal so movement works.
               if ((_Spawn.WindowX != null && _Spawn.WindowY != null) || _Spawn.Monitor != null || !string.IsNullOrEmpty(_Spawn.VirtualDesktop))
                  ShowWindow(windowHandle, SW_SHOWNORMAL);

               // Switch monitors.
               int baseX = 0, baseY = 0;
               if (_Spawn.Monitor != null)
               {
                  // Translate out of its current screen space.
                  RECT rect;
                  GetWindowRect(windowHandle, out rect);
                  System.Windows.Forms.Screen curScreen = null;
                  try
                  {
                     curScreen = System.Windows.Forms.Screen.AllScreens.Where(s => s.Bounds.Contains(rect.left, rect.top)).First();
                  }
                  catch (Exception ex) { }
                  if (curScreen != null)
                  {
                     if (_Spawn.WindowX == null)
                        baseX = rect.left - curScreen.WorkingArea.X;
                     if (_Spawn.WindowY == null)
                        baseY = rect.top - curScreen.WorkingArea.Y;
                  }

                  // Translate into the new screen space.
                  System.Windows.Forms.Screen targetScreen = System.Windows.Forms.Screen.AllScreens[_Spawn.Monitor.Value];
                  baseX += targetScreen.WorkingArea.X;
                  baseY += targetScreen.WorkingArea.Y;
               }

               // Move and resize.
               int moveBit = ((_Spawn.WindowX != null && _Spawn.WindowY != null) || _Spawn.Monitor != null) ? 0 : SWP_NOMOVE;
               int sizeBit = (_Spawn.WindowW != null && _Spawn.WindowH != null) ? 0 : SWP_NOSIZE;
               if (moveBit == 0 || sizeBit == 0)
               {
                  int wx = ((_Spawn.WindowX != null) ? _Spawn.WindowX.Value : 0) + baseX;
                  int wy = ((_Spawn.WindowY != null) ? _Spawn.WindowY.Value : 0) + baseY;
                  int ww = (_Spawn.WindowW != null) ? _Spawn.WindowW.Value : 0;
                  int wh = (_Spawn.WindowH != null) ? _Spawn.WindowH.Value : 0;
                  SetWindowPos(windowHandle, IntPtr.Zero, wx, wy, ww, wh, SWP_NOZORDER | moveBit | sizeBit | SWP_SHOWWINDOW);
               }

               // Maximize and minimize.
               if (_Spawn.Maximize)
                  ShowWindow(windowHandle, SW_SHOWMAXIMIZED);
               else if (_Spawn.Minimize)
                  ShowWindow(windowHandle, SW_SHOWMINIMIZED);

               // Switch virtual desktop.
               if (!string.IsNullOrEmpty(_Spawn.VirtualDesktop))
               {
                  int desktopIndex = VirtualDesktop.SearchDesktop(_Spawn.VirtualDesktop);
                  if (desktopIndex != -1)
                  {
                     VirtualDesktop desktop = VirtualDesktop.FromIndex(desktopIndex);
                     desktop.MoveWindow(windowHandle);
                  }
               }
            }
            catch (Exception e)
            {
               App.WriteLog("App exception");
               App.WriteLog(e.ToString());
               MessageBox.Show($"Unable to manage process {_Spawn.FileName} / {_Spawn.ShellOpen}.\n\nApp exception: {e.ToString()}");
            }

            isDone = true;
         }

         //
         if (isDone)
         {
            if (_Process != null)
               _Process.Dispose();
            _Process = null;

            if (_Timer != null)
            {
               _Timer.Stop();
               _Timer.Dispose();
            }
            _Timer = null;
         }
         else
            _Timer.Start();
      }
   }
}
