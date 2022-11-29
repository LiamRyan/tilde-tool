using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

         // Make sure the process is valid.
         bool isValid = false;
         try
         {
            isValid = !_Process.HasExited;
            if (!isValid)
            {
               App.WriteLog($"App exited before move: {_Spawn.FileName}");
               MessageBox.Show($"Unable to manage process {_Spawn.FileName} / {_Spawn.ShellOpen}.\n\nApp exited before move: {_Spawn.FileName}");
            }
         }
         catch (Exception e)
         {
            App.WriteLog("Process exception");
            App.WriteLog(e.ToString());
            MessageBox.Show($"Unable to manage process {_Spawn.FileName} / {_Spawn.ShellOpen}.\n\nProcess exception: {e.ToString()}");
            isValid = false;
         }

         // Alright, handle it.
         if (!isValid)
         {
            _Process.Dispose();
            _Process = null;
         }
         else if (_Process.MainWindowHandle != IntPtr.Zero)
         {
            try
            {
               // If we're going to move it, and it's minimized or maximized, go back to normal so movement works.
               if ((_Spawn.WindowX != null && _Spawn.WindowY != null) || _Spawn.Monitor != null || !string.IsNullOrEmpty(_Spawn.VirtualDesktop))
                  ShowWindow(_Process.MainWindowHandle, SW_SHOWNORMAL);

               // Switch monitors.
               int baseX = 0, baseY = 0;
               if (_Spawn.Monitor != null)
               {
                  // Translate out of its current screen space.
                  RECT rect;
                  GetWindowRect(_Process.MainWindowHandle, out rect);
                  System.Windows.Forms.Screen curScreen = null;
                  try
                  {
                     curScreen = System.Windows.Forms.Screen.AllScreens.Where(s => s.Bounds.Contains(rect.left, rect.top)).First();
                  }
                  catch (Exception ex) { }
                  if (curScreen != null)
                  {
                     baseX = rect.left - curScreen.WorkingArea.X;
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
                  SetWindowPos(_Process.MainWindowHandle, IntPtr.Zero, wx, wy, ww, wh, SWP_NOZORDER | moveBit | sizeBit | SWP_SHOWWINDOW);
               }

               // Maximize and minimize.
               if (_Spawn.Maximize)
                  ShowWindow(_Process.MainWindowHandle, SW_SHOWMAXIMIZED);
               else if (_Spawn.Minimize)
                  ShowWindow(_Process.MainWindowHandle, SW_SHOWMINIMIZED);

               // Switch virtual desktop.
               if (!string.IsNullOrEmpty(_Spawn.VirtualDesktop))
               {
                  int desktopIndex = VirtualDesktop.SearchDesktop(_Spawn.VirtualDesktop);
                  if (desktopIndex != -1)
                  {
                     VirtualDesktop desktop = VirtualDesktop.FromIndex(desktopIndex);
                     desktop.MoveWindow(_Process.MainWindowHandle);
                  }
               }
            }
            catch (Exception e)
            {
               App.WriteLog("App exception");
               App.WriteLog(e.ToString());
               MessageBox.Show($"Unable to manage process {_Spawn.FileName} / {_Spawn.ShellOpen}.\n\nApp exception: {e.ToString()}");
            }

            _Process.Dispose();
            _Process = null;
         }

         //
         if (_Process == null)
         {
            _Timer.Stop();
            _Timer.Dispose();
            _Timer = null;
         }
         else
            _Timer.Start();
      }
   }
}
