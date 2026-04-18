using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinPartFlash.Gui.MacOS.Interop;

/// <summary>
/// Minimal P/Invoke surface to the Obj-C runtime (<c>libobjc.dylib</c>)
/// plus <c>dlopen</c>, just enough to invoke SMAppService from Foundation
/// without dragging in a full binding generator.  All signatures are the
/// exact non-variadic arity we use at each call site — mixing the wrong
/// arity for <c>objc_msgSend</c> is undefined on ARM64 macOS.
/// </summary>
[SupportedOSPlatform("MacOS")]
internal static class LibObjc
{
    private const string Libobjc = "/usr/lib/libobjc.A.dylib";
    private const string Libdl = "/usr/lib/libSystem.dylib";

    public const int RtldLazy = 1;

    [DllImport(Libdl, EntryPoint = "dlopen")]
    public static extern IntPtr Dlopen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);

    [DllImport(Libobjc, EntryPoint = "objc_getClass")]
    public static extern IntPtr ObjcGetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(Libobjc, EntryPoint = "sel_registerName")]
    public static extern IntPtr SelRegisterName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    // objc_msgSend: id (id self, SEL op)
    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static extern IntPtr MsgSendPtr(IntPtr self, IntPtr selector);

    // objc_msgSend: id (id self, SEL op, id arg1)
    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static extern IntPtr MsgSendPtr_Ptr(IntPtr self, IntPtr selector, IntPtr arg1);

    // objc_msgSend: id (id self, SEL op, const char *arg1)
    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static extern IntPtr MsgSendPtr_Utf8(
        IntPtr self, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string arg1);

    // objc_msgSend: BOOL (id self, SEL op, id *arg1)
    // macOS ARM64: BOOL is unsigned char.  Use byte and compare to 0 ourselves.
    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static extern byte MsgSendByte_PtrOut(IntPtr self, IntPtr selector, out IntPtr arg1);

    // objc_msgSend: NSInteger (id self, SEL op)
    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static extern nint MsgSendNInt(IntPtr self, IntPtr selector);
}
