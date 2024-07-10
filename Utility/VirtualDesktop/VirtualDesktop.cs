// Author: Markus Scholtes, 2023
// Version 1.16, 2023-09-17
// Version for Windows 11 22H2.2215 and up
// Compile with:
// C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe VirtualDesktop11-23H2.cs

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ComponentModel;
using System.Text;
using WinRT;

// Based on http://stackoverflow.com/a/32417530, Windows 10 SDK, github project Grabacr07/VirtualDesktop and own research
// See also https://github.com/slnz00/VirtualDesktopDumper for API if the internal interface changes.
// Modified for use in tildetool

namespace VirtualDesktopApi
{
   #region COM API
   public static class Guids
   {
      public static readonly Guid CLSID_ImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
      public static readonly Guid CLSID_VirtualDesktopManagerInternal = new Guid("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
      public static readonly Guid CLSID_VirtualDesktopManager = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
      public static readonly Guid CLSID_VirtualDesktopPinnedApps = new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");
      public static readonly Guid CLSID_VirtualDesktopNotificationService = new Guid("a501fdec-4a09-464c-ae4e-1b9c21b84918");
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct Size
   {
      public int X;
      public int Y;
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct Rect
   {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
   }

   public enum APPLICATION_VIEW_CLOAK_TYPE : int
   {
      AVCT_NONE = 0,
      AVCT_DEFAULT = 1,
      AVCT_VIRTUAL_DESKTOP = 2
   }

   public enum APPLICATION_VIEW_COMPATIBILITY_POLICY : int
   {
      AVCP_NONE = 0,
      AVCP_SMALL_SCREEN = 1,
      AVCP_TABLET_SMALL_SCREEN = 2,
      AVCP_VERY_SMALL_SCREEN = 3,
      AVCP_HIGH_SCALE_FACTOR = 4
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct HString
   {
      private readonly IntPtr _abi;

      public HString(string str)
      {
         this._abi = MarshalString.GetAbi(MarshalString.CreateMarshaler(str));
      }

      public static implicit operator string(HString hStr)
          => MarshalString.FromAbi(hStr._abi);
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
   public interface IApplicationView
   {
      int SetFocus();
      int SwitchTo();
      int TryInvokeBack(IntPtr /* IAsyncCallback* */ callback);
      int GetThumbnailWindow(out IntPtr hwnd);
      int GetMonitor(out IntPtr /* IImmersiveMonitor */ immersiveMonitor);
      int GetVisibility(out int visibility);
      int SetCloak(APPLICATION_VIEW_CLOAK_TYPE cloakType, int unknown);
      int GetPosition(ref Guid guid /* GUID for IApplicationViewPosition */, out IntPtr /* IApplicationViewPosition** */ position);
      int SetPosition(ref IntPtr /* IApplicationViewPosition* */ position);
      int InsertAfterWindow(IntPtr hwnd);
      int GetExtendedFramePosition(out Rect rect);
      int GetAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] out string id);
      int SetAppUserModelId(string id);
      int IsEqualByAppUserModelId(string id, out int result);
      int GetViewState(out uint state);
      int SetViewState(uint state);
      int GetNeediness(out int neediness);
      int GetLastActivationTimestamp(out ulong timestamp);
      int SetLastActivationTimestamp(ulong timestamp);
      int GetVirtualDesktopId(out Guid guid);
      int SetVirtualDesktopId(ref Guid guid);
      int GetShowInSwitchers(out int flag);
      int SetShowInSwitchers(int flag);
      int GetScaleFactor(out int factor);
      int CanReceiveInput(out bool canReceiveInput);
      int GetCompatibilityPolicyType(out APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
      int SetCompatibilityPolicyType(APPLICATION_VIEW_COMPATIBILITY_POLICY flags);
      int GetSizeConstraints(IntPtr /* IImmersiveMonitor* */ monitor, out Size size1, out Size size2);
      int GetSizeConstraintsForDpi(uint uint1, out Size size1, out Size size2);
      int SetSizeConstraintsForDpi(ref uint uint1, ref Size size1, ref Size size2);
      int OnMinSizePreferencesUpdated(IntPtr hwnd);
      int ApplyOperation(IntPtr /* IApplicationViewOperation* */ operation);
      int IsTray(out bool isTray);
      int IsInHighZOrderBand(out bool isInHighZOrderBand);
      int IsSplashScreenPresented(out bool isSplashScreenPresented);
      int Flash();
      int GetRootSwitchableOwner(out IApplicationView rootSwitchableOwner);
      int EnumerateOwnershipTree(out IObjectArray ownershipTree);
      int GetEnterpriseId([MarshalAs(UnmanagedType.LPWStr)] out string enterpriseId);
      int IsMirrored(out bool isMirrored);
      int Unknown1(out int unknown);
      int Unknown2(out int unknown);
      int Unknown3(out int unknown);
      int Unknown4(out int unknown);
      int Unknown5(out int unknown);
      int Unknown6(int unknown);
      int Unknown7();
      int Unknown8(out int unknown);
      int Unknown9(int unknown);
      int Unknown10(int unknownX, int unknownY);
      int Unknown11(int unknown);
      int Unknown12(out Size size1);
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
   public interface IApplicationViewCollection
   {
      int GetViews(out IObjectArray array);
      int GetViewsByZOrder(out IObjectArray array);
      int GetViewsByAppUserModelId(string id, out IObjectArray array);
      int GetViewForHwnd(IntPtr hwnd, out IApplicationView view);
      int GetViewForApplication(object application, out IApplicationView view);
      int GetViewForAppUserModelId(string id, out IApplicationView view);
      int GetViewInFocus(out IntPtr view);
      int Unknown1(out IntPtr view);
      void RefreshCollection();
      int RegisterForApplicationViewChanges(object listener, out int cookie);
      int UnregisterForApplicationViewChanges(int cookie);
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
   public interface IVirtualDesktop
   {
      bool IsViewVisible(IApplicationView view);
      Guid GetId();
      HString GetName();
      HString GetWallpaperPath();
      bool IsRemote();
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("53F5CA0B-158F-4124-900C-057158060B27")]
   public interface IVirtualDesktopManagerInternal
   {
      int GetCount();
      void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
      bool CanViewMoveDesktops(IApplicationView view);
      IVirtualDesktop GetCurrentDesktop();
      void GetDesktops(out IObjectArray desktops);
      [PreserveSig]
      int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
      void SwitchDesktop(IVirtualDesktop desktop);
      //		void SwitchDesktopAndMoveForegroundView(IVirtualDesktop desktop);
      IVirtualDesktop CreateDesktop();
      void MoveDesktop(IVirtualDesktop desktop, int nIndex);
      void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
      IVirtualDesktop FindDesktop(ref Guid desktopid);
      void GetDesktopSwitchIncludeExcludeViews(IVirtualDesktop desktop, out IObjectArray unknown1, out IObjectArray unknown2);
      void SetDesktopName(IVirtualDesktop desktop, HString name);
      void SetDesktopWallpaper(IVirtualDesktop desktop, HString path);
      void UpdateWallpaperPathForAllDesktops(HString path);
      void CopyDesktopState(IApplicationView pView0, IApplicationView pView1);
      void CreateRemoteDesktop(HString path, out IVirtualDesktop desktop);
      void SwitchRemoteDesktop(IVirtualDesktop desktop, int switchType);
      void SwitchDesktopWithAnimation(IVirtualDesktop desktop);
      void GetLastActiveDesktop(out IVirtualDesktop desktop);
      void WaitForAnimationToComplete();
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
   public interface IVirtualDesktopManager
   {
      bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
      Guid GetWindowDesktopId(IntPtr topLevelWindow);
      void MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
   public interface IVirtualDesktopPinnedApps
   {
      bool IsAppIdPinned(string appId);
      void PinAppID(string appId);
      void UnpinAppID(string appId);
      bool IsViewPinned(IApplicationView applicationView);
      void PinView(IApplicationView applicationView);
      void UnpinView(IApplicationView applicationView);
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
   public interface IObjectArray
   {
      void GetCount(out int count);
      void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
   }

   [ComImport]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
   public interface IServiceProvider10
   {
      [return: MarshalAs(UnmanagedType.IUnknown)]
      object QueryService(ref Guid service, ref Guid riid);
   }
   #endregion

   [ComImport]
   [Guid("B9E5E94D-233E-49AB-AF5C-2B4541C3AADE")]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   public interface IVirtualDesktopNotification
   {
      void VirtualDesktopCreated(IVirtualDesktop pDesktop);

      void VirtualDesktopDestroyBegin(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback);

      void VirtualDesktopDestroyFailed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback);

      void VirtualDesktopDestroyed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback);

      //void Proc7(int p0);

      void VirtualDesktopMoved(IVirtualDesktop pDesktop, int nIndexFrom, int nIndexTo);

      void VirtualDesktopRenamed(IVirtualDesktop pDesktop, HString chName);

      void ViewVirtualDesktopChanged(IApplicationView pView);

      void CurrentVirtualDesktopChanged(IVirtualDesktop pDesktopOld, IVirtualDesktop pDesktopNew);

      void VirtualDesktopWallpaperChanged(IVirtualDesktop pDesktop, HString chPath);
   }

   [ComImport]
   [Guid("0cd45e71-d927-4f15-8b0a-8fef525337bf") /* replace at runtime */]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   public interface IVirtualDesktopNotificationService
   {
      uint Register(IVirtualDesktopNotification pNotification);

      void Unregister(uint dwCookie);
   }

   #region COM wrapper
   internal class VirtualDesktopNotification : IVirtualDesktopNotification
   {
      public void VirtualDesktopCreated(IVirtualDesktop pDesktop)
      {
         VirtualDesktop.VirtualDesktopCreated(pDesktop);
      }

      public void VirtualDesktopDestroyBegin(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback)
      {
         VirtualDesktop.VirtualDesktopDestroyBegin(pDesktopDestroyed, pDesktopFallback);
      }

      public void VirtualDesktopDestroyFailed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback)
      {
         VirtualDesktop.VirtualDesktopDestroyFailed(pDesktopDestroyed, pDesktopFallback);
      }

      public void VirtualDesktopDestroyed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback)
      {
         VirtualDesktop.VirtualDesktopDestroyed(pDesktopDestroyed, pDesktopFallback);
      }

      public void Proc7(int p0)
      {
      }

      public void VirtualDesktopMoved(IVirtualDesktop pDesktop, int nIndexFrom, int nIndexTo)
      {
         VirtualDesktop.VirtualDesktopMoved(pDesktop, nIndexFrom, nIndexTo);
      }
      public void VirtualDesktopRenamed(IVirtualDesktop pDesktop, HString chName)
      {
         VirtualDesktop.VirtualDesktopRenamed(pDesktop, chName);
      }

      public void ViewVirtualDesktopChanged(IApplicationView pView)
      {
         VirtualDesktop.ViewVirtualDesktopChanged(pView);
      }

      public void CurrentVirtualDesktopChanged(IVirtualDesktop pDesktopOld, IVirtualDesktop pDesktopNew)
      {
         VirtualDesktop.CurrentVirtualDesktopChanged(pDesktopOld, pDesktopNew);
      }

      public void VirtualDesktopWallpaperChanged(IVirtualDesktop pDesktop, HString chPath)
      {
         VirtualDesktop.VirtualDesktopWallpaperChanged(pDesktop, chPath);
      }
   }

   public static class VirtualDesktopManager
   {
      public static void Initialize()
      {
         IServiceProvider10 shell = null;
         Type immersiveShell = Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell);
         try
         {
            shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_ImmersiveShell));
         }
         catch (System.InvalidCastException e)
         {
            Debug.Write("Unable to initialize VirtualDesktopManager: " + e.ToString());
         }
         try
         {
            if (shell != null)
               VirtualDesktopManagerInternal = (IVirtualDesktopManagerInternal)shell.QueryService(Guids.CLSID_VirtualDesktopManagerInternal, typeof(IVirtualDesktopManagerInternal).GUID);
         }
         catch (System.InvalidCastException e)
         {
            Debug.Write("Unable to initialize VirtualDesktopManager: " + e.ToString());
         }
         try
         {
            _VirtualDesktopManager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(Guids.CLSID_VirtualDesktopManager));
         }
         catch (System.InvalidCastException e)
         {
            Debug.Write("Unable to initialize VirtualDesktopManager: " + e.ToString());
         }
         try
         {
            if (shell != null)
               ApplicationViewCollection = (IApplicationViewCollection)shell.QueryService(typeof(IApplicationViewCollection).GUID, typeof(IApplicationViewCollection).GUID);
         }
         catch (System.InvalidCastException e)
         {
            Debug.Write("Unable to initialize VirtualDesktopManager: " + e.ToString());
         }
         try
         {
            if (shell != null)
               VirtualDesktopPinnedApps = (IVirtualDesktopPinnedApps)shell.QueryService(Guids.CLSID_VirtualDesktopPinnedApps, typeof(IVirtualDesktopPinnedApps).GUID);
         }
         catch (System.InvalidCastException e)
         {
            Debug.Write("Unable to initialize VirtualDesktopManager: " + e.ToString());
         }
         //try
         //{
         //   if (shell != null)
         //      VirtualDesktopNotificationService = (IVirtualDesktopNotificationService)shell.QueryService(Guids.CLSID_VirtualDesktopNotificationService, typeof(IVirtualDesktopNotificationService).GUID);
         //}
         //catch (System.InvalidCastException e)
         //{
         //   Debug.Write("Unable to initialize VirtualDesktopManager: " + e.ToString());
         //}

         //try
         //{
         //   if (VirtualDesktopNotificationService != null)
         //   {
         //      VirtualDesktopNotification = new VirtualDesktopNotification();
         //      VirtualDesktopNotificationCookie = VirtualDesktopNotificationService.Register(VirtualDesktopNotification);
         //   }
         //}
         //catch (System.InvalidCastException e)
         //{
         //   Debug.Write("Unable to initialize VirtualDesktopManager: " + e.ToString());
         //}

         VirtualDesktop.Created += (a) => VirtualDesktop.RebuildDictionary();
         VirtualDesktop.Destroyed += (a, b) => VirtualDesktop.RebuildDictionary();
         VirtualDesktop.RebuildDictionary();
      }

      internal static IVirtualDesktopManagerInternal VirtualDesktopManagerInternal;
      internal static IVirtualDesktopManager _VirtualDesktopManager;
      internal static IApplicationViewCollection ApplicationViewCollection;
      internal static IVirtualDesktopPinnedApps VirtualDesktopPinnedApps;
      internal static IVirtualDesktopNotificationService VirtualDesktopNotificationService;

      internal static VirtualDesktopNotification VirtualDesktopNotification;
      internal static uint VirtualDesktopNotificationCookie;

      internal static IVirtualDesktop GetDesktop(int index)
      {  // get desktop with index
         int count = VirtualDesktopManagerInternal.GetCount();
         if (index < 0 || index >= count) throw new ArgumentOutOfRangeException("index");
         IObjectArray desktops;
         VirtualDesktopManagerInternal.GetDesktops(out desktops);
         object objdesktop;
         desktops.GetAt(index, typeof(IVirtualDesktop).GUID, out objdesktop);
         Marshal.ReleaseComObject(desktops);
         return (IVirtualDesktop)objdesktop;
      }

      internal static int GetDesktopIndex(IVirtualDesktop desktop)
      { // get index of desktop
         int index = -1;
         Guid IdSearch = desktop.GetId();
         IObjectArray desktops;
         VirtualDesktopManagerInternal.GetDesktops(out desktops);
         object objdesktop;
         for (int i = 0; i < VirtualDesktopManagerInternal.GetCount(); i++)
         {
            desktops.GetAt(i, typeof(IVirtualDesktop).GUID, out objdesktop);
            if (IdSearch.CompareTo(((IVirtualDesktop)objdesktop).GetId()) == 0)
            {
               index = i;
               break;
            }
         }
         Marshal.ReleaseComObject(desktops);
         return index;
      }

      internal static IApplicationView GetApplicationView(this IntPtr hWnd)
      { // get application view to window handle
         IApplicationView view;
         ApplicationViewCollection.GetViewForHwnd(hWnd, out view);
         return view;
      }

      internal static string GetAppId(IntPtr hWnd)
      { // get Application ID to window handle
         string appId;
         hWnd.GetApplicationView().GetAppUserModelId(out appId);
         return appId;
      }
   }
   #endregion

   #region public interface
   public class WindowInformation
   { // stores window informations
      public string Title { get; set; }
      public int Handle { get; set; }
   }

   public partial class VirtualDesktop
   {
      // get process id to window handle
      [DllImport("user32.dll")]
      private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

      // get handle of active window
      [DllImport("user32.dll")]
      private static extern IntPtr GetForegroundWindow();

      private static readonly Guid AppOnAllDesktops = new Guid("BB64D5B7-4DE3-4AB2-A87C-DB7601AEA7DC");
      private static readonly Guid WindowOnAllDesktops = new Guid("C2DDEA68-66F2-4CF9-8264-1BFD00FBBBAC");

      private IVirtualDesktop ivd;
      private VirtualDesktop(IVirtualDesktop desktop) { this.ivd = desktop; }

      public override int GetHashCode()
      { // get hash
         return ivd.GetHashCode();
      }

      public override bool Equals(object obj)
      { // compare with object
         var desk = obj as VirtualDesktop;
         return desk != null && object.ReferenceEquals(this.ivd, desk.ivd);
      }

      #region Event Interface

      public delegate void VirtualDesktopCreatedDelegate(VirtualDesktop pDesktop);
      public delegate void VirtualDesktopDestroyBeginDelegate(VirtualDesktop pDesktopDestroyed, VirtualDesktop pDesktopFallback);
      public delegate void VirtualDesktopDestroyFailedDelegate(VirtualDesktop pDesktopDestroyed, VirtualDesktop pDesktopFallback);
      public delegate void VirtualDesktopDestroyedDelegate(VirtualDesktop pDesktopDestroyed, VirtualDesktop pDesktopFallback);
      public delegate void VirtualDesktopMovedDelegate(VirtualDesktop pDesktop, int nIndexFrom, int nIndexTo);
      public delegate void VirtualDesktopRenamedDelegate(VirtualDesktop pDesktop, string chName);
      public delegate void ViewVirtualDesktopChangedDelegate(IApplicationView pView);
      public delegate void CurrentVirtualDesktopChangedDelegate(VirtualDesktop pDesktopOld, VirtualDesktop pDesktopNew);
      public delegate void VirtualDesktopWallpaperChangedDelegate(VirtualDesktop pDesktop, string chPath);

      internal static void VirtualDesktopCreated(IVirtualDesktop pDesktop)
      {
         Created?.Invoke(VirtualDesktop.FromInterface(pDesktop));
      }

      internal static void VirtualDesktopDestroyBegin(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback)
      {
         DestroyBegin?.Invoke(VirtualDesktop.FromInterface(pDesktopDestroyed), VirtualDesktop.FromInterface(pDesktopFallback));
      }

      internal static void VirtualDesktopDestroyFailed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback)
      {
         DestroyFailed?.Invoke(VirtualDesktop.FromInterface(pDesktopDestroyed), VirtualDesktop.FromInterface(pDesktopFallback));
      }

      internal static void VirtualDesktopDestroyed(IVirtualDesktop pDesktopDestroyed, IVirtualDesktop pDesktopFallback)
      {
         Destroyed?.Invoke(VirtualDesktop.FromInterface(pDesktopDestroyed), VirtualDesktop.FromInterface(pDesktopFallback));
      }

      internal static void VirtualDesktopMoved(IVirtualDesktop pDesktop, int nIndexFrom, int nIndexTo)
      {
         Moved?.Invoke(VirtualDesktop.FromInterface(pDesktop), nIndexFrom, nIndexTo);
      }
      internal static void VirtualDesktopRenamed(IVirtualDesktop pDesktop, string chName)
      {
         Renamed?.Invoke(VirtualDesktop.FromInterface(pDesktop), chName);
      }

      internal static void ViewVirtualDesktopChanged(IApplicationView pView)
      {
         ViewChanged?.Invoke(pView);
      }

      internal static void CurrentVirtualDesktopChanged(IVirtualDesktop pDesktopOld, IVirtualDesktop pDesktopNew)
      {
         CurrentChanged?.Invoke(VirtualDesktop.FromInterface(pDesktopOld), VirtualDesktop.FromInterface(pDesktopNew));
      }

      internal static void VirtualDesktopWallpaperChanged(IVirtualDesktop pDesktop, string chPath)
      {
         WallpaperChanged?.Invoke(VirtualDesktop.FromInterface(pDesktop), chPath);
      }

      public static event VirtualDesktopCreatedDelegate? Created;
      public static event VirtualDesktopDestroyBeginDelegate? DestroyBegin;
      public static event VirtualDesktopDestroyFailedDelegate? DestroyFailed;
      public static event VirtualDesktopDestroyedDelegate? Destroyed;
      public static event VirtualDesktopMovedDelegate? Moved;
      public static event VirtualDesktopRenamedDelegate? Renamed;
      public static event ViewVirtualDesktopChangedDelegate? ViewChanged;
      public static event CurrentVirtualDesktopChangedDelegate? CurrentChanged;
      public static event VirtualDesktopWallpaperChangedDelegate? WallpaperChanged;

      #endregion
      #region Static Interface

      static Dictionary<IVirtualDesktop, VirtualDesktop> DesktopList = new Dictionary<IVirtualDesktop, VirtualDesktop>();
      internal static void RebuildDictionary()
      {
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return;

         // Grab the new list of virtual desktops.
         int count = VirtualDesktopManager.VirtualDesktopManagerInternal.GetCount();
         IVirtualDesktop[] desktops = Enumerable.Range(0, count).Select(VirtualDesktopManager.GetDesktop).ToArray();

         // build a new dictionary, reusing old ones when possible.
         Dictionary<IVirtualDesktop, VirtualDesktop> newDesktops = new Dictionary<IVirtualDesktop, VirtualDesktop>();
         foreach (IVirtualDesktop idesktop in desktops)
         {
            VirtualDesktop desktop;
            if (DesktopList.TryGetValue(idesktop, out desktop))
               newDesktops[idesktop] = desktop;
            else
               newDesktops[idesktop] = new VirtualDesktop(idesktop);
         }
         DesktopList = newDesktops;
      }

      public static int Count => DesktopList.Count;

      public static VirtualDesktop Current
      {
         get
         {
            if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
               return null;
            return FromInterface(VirtualDesktopManager.VirtualDesktopManagerInternal.GetCurrentDesktop());
         }
      }

      public static IEnumerable<VirtualDesktop> GetDesktops()
      {
         for (int i = 0; i < Count; i++)
            yield return FromIndex(i);
      }

      public static VirtualDesktop FromIndex(int index)
      {
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return null;
         return FromInterface(VirtualDesktopManager.GetDesktop(index));
      }

      public static VirtualDesktop FromWindow(IntPtr hWnd)
      { // return desktop object to desktop on which window <hWnd> is displayed
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();

         if (VirtualDesktopManager._VirtualDesktopManager == null || VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return null;

         Guid id = VirtualDesktopManager._VirtualDesktopManager.GetWindowDesktopId(hWnd);
         if ((id.CompareTo(AppOnAllDesktops) == 0) || (id.CompareTo(WindowOnAllDesktops) == 0))
            return FromInterface(VirtualDesktopManager.VirtualDesktopManagerInternal.GetCurrentDesktop());
         else
            return FromInterface(VirtualDesktopManager.VirtualDesktopManagerInternal.FindDesktop(ref id));
      }

      Dictionary<IVirtualDesktop, VirtualDesktop> Desktops = new Dictionary<IVirtualDesktop, VirtualDesktop>();
      public static VirtualDesktop FromInterface(IVirtualDesktop ivd)
      {
         VirtualDesktop desktop;
         if (DesktopList.TryGetValue(ivd, out desktop))
            return desktop;
         return null;
      }

      public static string DesktopNameFromDesktop(VirtualDesktop desktop)
      { // return name of desktop or "Desktop n" if it has no name
         return desktop.Name;
      }

      public static string DesktopNameFromIndex(int index)
      { // return name of desktop from index (-> index = 0..Count-1) or "Desktop n" if it has no name

         // get desktop name
         string desktopName = null;
         try
         {
            desktopName = VirtualDesktopManager.GetDesktop(index).GetName();
         }
         catch { }

         // no name found, generate generic name
         if (string.IsNullOrEmpty(desktopName))
         { // create name "Desktop n" (n = number starting with 1)
            desktopName = "Desktop " + (index + 1).ToString();
         }
         return desktopName;
      }

      public static bool HasDesktopNameFromIndex(int index)
      { // return true is desktop is named or false if it has no name

         // read desktop name in registry
         string desktopName = null;
         try
         {
            desktopName = VirtualDesktopManager.GetDesktop(index).GetName();
         }
         catch { }

         // name found?
         if (string.IsNullOrEmpty(desktopName))
            return false;
         else
            return true;
      }

      public static string DesktopWallpaperFromIndex(int index)
      { // return name of desktop wallpaper from index (-> index = 0..Count-1)

         // get desktop name
         string desktopwppath = "";
         try
         {
            desktopwppath = VirtualDesktopManager.GetDesktop(index).GetWallpaperPath();
         }
         catch { }

         return desktopwppath;
      }

      public static int SearchDesktop(string partialName)
      { // get index of desktop with partial name, return -1 if no desktop found
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return -1;

         int index = -1;
         for (int i = 0; i < VirtualDesktopManager.VirtualDesktopManagerInternal.GetCount(); i++)
         { // loop through all virtual desktops and compare partial name to desktop name
            string desktopName = DesktopNameFromIndex(i);
            if (desktopName.ToUpper().IndexOf(partialName.ToUpper()) >= 0)
            {
               index = i;
               break;
            }
         }

         return index;
      }

      public static VirtualDesktop Create()
      { // create a new desktop
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return null;
         return FromInterface(VirtualDesktopManager.VirtualDesktopManagerInternal.CreateDesktop());
      }

      #endregion
      #region Instance Interface

      public int Index
      {
         get
         {
            if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
               return -1;

            int index = -1;
            Guid IdSearch = ivd.GetId();
            IObjectArray desktops;
            VirtualDesktopManager.VirtualDesktopManagerInternal.GetDesktops(out desktops);
            object objdesktop;
            for (int i = 0; i < VirtualDesktopManager.VirtualDesktopManagerInternal.GetCount(); i++)
            {
               desktops.GetAt(i, typeof(IVirtualDesktop).GUID, out objdesktop);
               if (IdSearch.CompareTo(((IVirtualDesktop)objdesktop).GetId()) == 0)
               {
                  index = i;
                  break;
               }
            }
            Marshal.ReleaseComObject(desktops);
            return index;
         }
      }

      public string Name
      {
         get
         {
            // get desktop name
            string desktopName = null;
            try
            {
               desktopName = ivd.GetName();
            }
            catch { }

            // no name found, generate generic name
            if (string.IsNullOrEmpty(desktopName))
            { // create name "Desktop n" (n = number starting with 1)
               desktopName = "Desktop " + (Index + 1).ToString();
            }
            return desktopName;
         }
      }

      public void Remove(VirtualDesktop fallback = null)
      { // destroy desktop and switch to <fallback>
         IVirtualDesktop fallbackdesktop;
         if (fallback == null)
         { // if no fallback is given use desktop to the left except for desktop 0.
            VirtualDesktop dtToCheck = FromInterface(VirtualDesktopManager.GetDesktop(0));
            if (this.Equals(dtToCheck))
            { // desktop 0: set fallback to second desktop (= "right" desktop)
               VirtualDesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 4, out fallbackdesktop); // 4 = RightDirection
            }
            else
            { // set fallback to "left" desktop
               VirtualDesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 3, out fallbackdesktop); // 3 = LeftDirection
            }
         }
         else
            // set fallback desktop
            fallbackdesktop = fallback.ivd;

         VirtualDesktopManager.VirtualDesktopManagerInternal.RemoveDesktop(ivd, fallbackdesktop);
      }

      public void Move(int index)
      { // move current desktop to desktop in index (-> index = 0..Count-1)
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return;
         VirtualDesktopManager.VirtualDesktopManagerInternal.MoveDesktop(ivd, index);
      }

      public void SetName(string Name)
      { // set name for desktop, empty string removes name
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return;
         VirtualDesktopManager.VirtualDesktopManagerInternal.SetDesktopName(this.ivd, new HString(Name));
      }

      public void SetWallpaperPath(string Path)
      { // set path for wallpaper, empty string removes path
         if (string.IsNullOrEmpty(Path)) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return;
         VirtualDesktopManager.VirtualDesktopManagerInternal.SetDesktopWallpaper(this.ivd, new HString(Path));
      }

      public static void SetAllWallpaperPaths(string Path)
      { // set wallpaper path for all desktops
         if (string.IsNullOrEmpty(Path)) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return;
         VirtualDesktopManager.VirtualDesktopManagerInternal.UpdateWallpaperPathForAllDesktops(new HString(Path));
      }

      public bool IsVisible
      { // return true if this desktop is the current displayed one
         get {
            if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
               return false;
            return object.ReferenceEquals(ivd, VirtualDesktopManager.VirtualDesktopManagerInternal.GetCurrentDesktop());
         }
      }

      public void MakeVisible()
      { // make this desktop visible
         if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
            return;
         VirtualDesktopManager.VirtualDesktopManagerInternal.SwitchDesktop(ivd);
      }

      public VirtualDesktop Left
      { // return desktop at the left of this one, null if none
         get
         {
            if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
               return null;
            IVirtualDesktop desktop;
            int hr = VirtualDesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 3, out desktop); // 3 = LeftDirection
            if (hr == 0)
               return FromInterface(desktop);
            else
               return null;
         }
      }

      public VirtualDesktop Right
      { // return desktop at the right of this one, null if none
         get
         {
            if (VirtualDesktopManager.VirtualDesktopManagerInternal == null)
               return null;
            IVirtualDesktop desktop;
            int hr = VirtualDesktopManager.VirtualDesktopManagerInternal.GetAdjacentDesktop(ivd, 4, out desktop); // 4 = RightDirection
            if (hr == 0)
               return FromInterface(desktop);
            else
               return null;
         }
      }

      public void MoveWindow(IntPtr hWnd)
      { // move window to this desktop
         int processId;
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         GetWindowThreadProcessId(hWnd, out processId);

         if (System.Diagnostics.Process.GetCurrentProcess().Id == processId)
         { // window of process
            try // the easy way (if we are owner)
            {
               VirtualDesktopManager._VirtualDesktopManager.MoveWindowToDesktop(hWnd, ivd.GetId());
            }
            catch // window of process, but we are not the owner
            {
               if (VirtualDesktopManager.ApplicationViewCollection == null || VirtualDesktopManager.VirtualDesktopManagerInternal == null)
                  return;
               IApplicationView view;
               VirtualDesktopManager.ApplicationViewCollection.GetViewForHwnd(hWnd, out view);
               VirtualDesktopManager.VirtualDesktopManagerInternal.MoveViewToDesktop(view, ivd);
            }
         }
         else
         { // window of other process
            if (VirtualDesktopManager.ApplicationViewCollection == null || VirtualDesktopManager.VirtualDesktopManagerInternal == null)
               return;
            IApplicationView view;
            VirtualDesktopManager.ApplicationViewCollection.GetViewForHwnd(hWnd, out view);
            try
            {
               VirtualDesktopManager.VirtualDesktopManagerInternal.MoveViewToDesktop(view, ivd);
            }
            catch
            { // could not move active window, try main window (or whatever windows thinks is the main window)
               VirtualDesktopManager.ApplicationViewCollection.GetViewForHwnd(System.Diagnostics.Process.GetProcessById(processId).MainWindowHandle, out view);
               VirtualDesktopManager.VirtualDesktopManagerInternal.MoveViewToDesktop(view, ivd);
            }
         }
      }

      public void MoveActiveWindow()
      { // move active window to this desktop
         MoveWindow(GetForegroundWindow());
      }

      public bool HasWindow(IntPtr hWnd)
      { // return true if window is on this desktop
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         if (VirtualDesktopManager._VirtualDesktopManager == null)
            return false;
         Guid id = VirtualDesktopManager._VirtualDesktopManager.GetWindowDesktopId(hWnd);
         if ((id.CompareTo(AppOnAllDesktops) == 0) || (id.CompareTo(WindowOnAllDesktops) == 0))
            return true;
         else
            return ivd.GetId() == id;
      }

      public static bool IsWindowPinned(IntPtr hWnd)
      { // return true if window is pinned to all desktops
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopPinnedApps == null)
            return false;
         return VirtualDesktopManager.VirtualDesktopPinnedApps.IsViewPinned(hWnd.GetApplicationView());
      }

      public static void PinWindow(IntPtr hWnd)
      { // pin window to all desktops
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopPinnedApps == null)
            return;
         var view = hWnd.GetApplicationView();
         if (!VirtualDesktopManager.VirtualDesktopPinnedApps.IsViewPinned(view))
         { // pin only if not already pinned
            VirtualDesktopManager.VirtualDesktopPinnedApps.PinView(view);
         }
      }

      public static void UnpinWindow(IntPtr hWnd)
      { // unpin window from all desktops
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopPinnedApps == null)
            return;
         var view = hWnd.GetApplicationView();
         if (VirtualDesktopManager.VirtualDesktopPinnedApps.IsViewPinned(view))
         { // unpin only if not already unpinned
            VirtualDesktopManager.VirtualDesktopPinnedApps.UnpinView(view);
         }
      }

      public static bool IsApplicationPinned(IntPtr hWnd)
      { // return true if application for window is pinned to all desktops
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopPinnedApps == null)
            return false;
         return VirtualDesktopManager.VirtualDesktopPinnedApps.IsAppIdPinned(VirtualDesktopManager.GetAppId(hWnd));
      }

      public static void PinApplication(IntPtr hWnd)
      { // pin application for window to all desktops
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopPinnedApps == null)
            return;
         string appId = VirtualDesktopManager.GetAppId(hWnd);
         if (!VirtualDesktopManager.VirtualDesktopPinnedApps.IsAppIdPinned(appId))
         { // pin only if not already pinned
            VirtualDesktopManager.VirtualDesktopPinnedApps.PinAppID(appId);
         }
      }

      public static void UnpinApplication(IntPtr hWnd)
      { // unpin application for window from all desktops
         if (hWnd == IntPtr.Zero) throw new ArgumentNullException();
         if (VirtualDesktopManager.VirtualDesktopPinnedApps == null)
            return;
         var view = hWnd.GetApplicationView();
         string appId = VirtualDesktopManager.GetAppId(hWnd);
         if (VirtualDesktopManager.VirtualDesktopPinnedApps.IsAppIdPinned(appId))
         { // unpin only if pinned
            VirtualDesktopManager.VirtualDesktopPinnedApps.UnpinAppID(appId);
         }
      }

      #endregion
   }
   #endregion
}
