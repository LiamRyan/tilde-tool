using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VirtualDesktopApi
{
   public static class WindowExtension
   {
      internal static IntPtr GetHandle(Window window)
      {
         HwndSource hwndSource = PresentationSource.FromVisual(window) as HwndSource;
         if (hwndSource == null)
            throw new ArgumentException("Unable to get a window handle. Call it after the Window.SourceInitialized event is fired.", nameof(window));
         return hwndSource.Handle;
      }

      public static void MoveToDesktop(this Window window, VirtualDesktop virtualDesktop)
      {
         virtualDesktop.MoveWindow(GetHandle(window));
      }
   }
}
