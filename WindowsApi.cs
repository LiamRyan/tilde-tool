﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Tildetool
{
   public static class WindowsApi
   {
      public const int WH_JOURNALRECORD = 0;
      public const int WH_JOURNALPLAYBACK = 1;
      public const int WH_KEYBOARD = 2;
      public const int WH_GETMESSAGE = 3;
      public const int WH_CALLWNDPROC = 4;
      public const int WH_CBT = 5;
      public const int WH_SYSMSGFILTER = 6;
      public const int WH_MOUSE = 7;
      public const int WH_HARDWARE = 8;
      public const int WH_DEBUG = 9;
      public const int WH_SHELL = 10;
      public const int WH_FOREGROUNDIDLE = 11;
      public const int WH_CALLWNDPROCRET = 12;
      public const int WH_KEYBOARD_LL = 13;
      public const int WH_MOUSE_LL = 14;

      public delegate int HookProc(int code, IntPtr wParam, IntPtr lParam);

      [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
      public static extern IntPtr GetModuleHandle(string lpModuleName);

      [DllImport("user32.dll", EntryPoint = "SetWindowsHookEx", SetLastError = true)]
      public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

      [DllImport("User32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
      public static extern byte UnhookWindowsHookEx(IntPtr hHook);

      [DllImport("user32.dll")]
      public static extern int CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);


      [StructLayout(LayoutKind.Sequential)]
      public struct RECT
      {
         public int left;
         public int top;
         public int right;
         public int bottom;
      }

      [DllImport("user32.dll", SetLastError = true)]
      public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

      [DllImport("user32.dll")]
      public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

      [DllImport("user32.dll", SetLastError = true)]
      public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

      [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
      public static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

      public delegate bool EnumDelegate(IntPtr hWnd, int lParam);

      public const int MAXTITLE = 255;

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      public static extern bool IsWindowVisible(IntPtr hWnd);

      [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDelegate lpEnumCallbackFunction, IntPtr lParam);

      [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
      public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);

      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

      [StructLayout(LayoutKind.Sequential)]
      public struct FLASHWINFO
      {
         public uint cbSize;
         public IntPtr hwnd;
         public uint dwFlags;
         public uint uCount;
         public int dwTimeout;
      }

      public const int SW_HIDE = 0;
      public const int SW_SHOWNORMAL = 1;
      public const int SW_SHOWMINIMIZED = 2;
      public const int SW_SHOWMAXIMIZED = 3;
      public const int SW_SHOWNOACTIVATE = 4;
      public const int SW_SHOW = 5;
      public const int SW_MINIMIZE = 6;
      public const int SW_SHOWMINNOACTIVE = 7;
      public const int SW_SHOWNA = 8;
      public const int SW_RESTORE = 9;
      public const int SW_SHOWDEFAULT = 10;
      public const int SW_FORCEMINIMIZE = 11;

      public const int SWP_NOSIZE = 0x0001;
      public const int SWP_NOMOVE = 0x0002;
      public const int SWP_NOZORDER = 0x0004;
      public const int SWP_SHOWWINDOW = 0x0040;

      public const int WM_KEYDOWN = 0x0100;
      public const int WM_KEYUP = 0x0101;

      /// <summary>Stop flashing. The system restores the window to its original state.</summary>
      public const uint FLASHW_STOP = 0;

      /// <summary>Flash the window caption.</summary>
      public const uint FLASHW_CAPTION = 1;

      /// <summary>Flash the taskbar button.</summary>
      public const uint FLASHW_TRAY = 2;

      /// <summary>Flash both the window caption and taskbar button.This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
      /// </summary>
      public const uint FLASHW_ALL = 3;

      /// <summary>Flash continuously, until the FLASHW_STOP flag is set.</summary>
      public const uint FLASHW_TIMER = 4;

      /// <summary>Flash continuously until the window comes to the foreground.</summary>
      public const uint FLASHW_TIMERNOFG = 12;
   }
}
