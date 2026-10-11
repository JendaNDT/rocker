// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private delegate nint WindowSubclass(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data);
    [DllImport("comctl32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowSubclass(nint window,WindowSubclass callback,nuint id,nuint data);
    [DllImport("comctl32.dll")] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool RemoveWindowSubclass(nint window,WindowSubclass callback,nuint id);
    [DllImport("comctl32.dll")]private static extern nint DefSubclassProc(nint window,uint message,nuint wParam,nint lParam);
    private WindowSubclass? nativeWindowCallback;private nint nativeWindow;private int timeZoneNotifications;
    private TimeZoneInfo practiceTimeZone=TimeZoneInfo.Local;
    private void InitializeNativeEvents()
    {
        nativeWindow=WinRT.Interop.WindowNative.GetWindowHandle(this);nativeWindowCallback=OnNativeWindowMessage;if(!SetWindowSubclass(nativeWindow,nativeWindowCallback,0x524b,0))throw new InvalidOperationException("Nativní události okna nelze připojit.");
    }
    private nint OnNativeWindowMessage(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data)
    {
        try
        {
            if(message is 0x001e or 0x001a){TimeZoneInfo.ClearCachedData();practiceTimeZone=TimeZoneInfo.Local;++timeZoneNotifications;}
            // Both suspend and resume leave the explicit session paused. Never
            // count an unseen interval or automatically resume the user's audio.
            if(message==0x0218 && wParam is 4 or 7 or 0x12){practiceSession.Pause();FlushPractice();RefreshPracticeControls();}
            if(message==0x0082)DetachNativeEvents();
        }
        catch(Exception){/* Never unwind a managed exception through a window procedure. */}
        return DefSubclassProc(window,message,wParam,lParam);
    }
    private void DetachNativeEvents(){if(nativeWindowCallback is { } callback){RemoveWindowSubclass(nativeWindow,callback,0x524b);nativeWindowCallback=null;}}
}
