using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace WinPartFlash.Gui.MacOS.Interop;

/// <summary>
/// Thin wrapper around <c>+[SMAppService daemonServiceWithPlistName:]</c>
/// and <c>-[SMAppService registerAndReturnError:]</c> via
/// <see cref="LibObjc"/>.  Returns a structured result so the gateway can
/// distinguish "unsigned / not approved" (expected on dev builds) from
/// real failures.
/// </summary>
[SupportedOSPlatform("MacOS")]
internal static class SMAppServiceInterop
{
    public enum RegisterStatus
    {
        Ok,
        FrameworkUnavailable, // ServiceManagement.framework missing (pre-macOS 13)
        ClassUnavailable,     // SMAppService class absent
        NotApproved,          // User hasn't toggled "Allow in Background" yet
        Unsigned,             // Signature/authorized-clients mismatch
        Other
    }

    public readonly record struct RegisterResult(
        RegisterStatus Status,
        string? Domain,
        long Code,
        string? LocalizedDescription);

    private static bool _frameworkLoaded;

    private static bool EnsureFramework()
    {
        if (_frameworkLoaded) return true;
        var handle = LibObjc.Dlopen(
            "/System/Library/Frameworks/ServiceManagement.framework/ServiceManagement",
            LibObjc.RtldLazy);
        _frameworkLoaded = handle != IntPtr.Zero;
        return _frameworkLoaded;
    }

    public static RegisterResult Register(string plistName)
    {
        if (!EnsureFramework())
            return new RegisterResult(RegisterStatus.FrameworkUnavailable, null, 0, null);

        var smClass = LibObjc.ObjcGetClass("SMAppService");
        if (smClass == IntPtr.Zero)
            return new RegisterResult(RegisterStatus.ClassUnavailable, null, 0, null);

        var selDaemonWith = LibObjc.SelRegisterName("daemonServiceWithPlistName:");
        var selRegister = LibObjc.SelRegisterName("registerAndReturnError:");

        // Convert plistName via NSString +stringWithUTF8String:
        var nsStringClass = LibObjc.ObjcGetClass("NSString");
        var selStringWith = LibObjc.SelRegisterName("stringWithUTF8String:");
        var nsPlistName = LibObjc.MsgSendPtr_Utf8(nsStringClass, selStringWith, plistName);
        if (nsPlistName == IntPtr.Zero)
            return new RegisterResult(RegisterStatus.Other, null, 0, "NSString allocation failed");

        var service = LibObjc.MsgSendPtr_Ptr(smClass, selDaemonWith, nsPlistName);
        if (service == IntPtr.Zero)
            return new RegisterResult(RegisterStatus.Other, null, 0, "daemonServiceWithPlistName: returned nil");

        var ok = LibObjc.MsgSendByte_PtrOut(service, selRegister, out var errorPtr) != 0;
        if (ok)
            return new RegisterResult(RegisterStatus.Ok, null, 0, null);

        var (domain, code, description) = ReadNSError(errorPtr);
        var status = ClassifyError(domain, code);
        return new RegisterResult(status, domain, code, description);
    }

    private static (string? domain, long code, string? description) ReadNSError(IntPtr error)
    {
        if (error == IntPtr.Zero) return (null, 0, null);

        var selDomain = LibObjc.SelRegisterName("domain");
        var selCode = LibObjc.SelRegisterName("code");
        var selLocalized = LibObjc.SelRegisterName("localizedDescription");
        var selUtf8 = LibObjc.SelRegisterName("UTF8String");

        var domain = NSStringToManaged(LibObjc.MsgSendPtr(error, selDomain), selUtf8);
        var code = (long)LibObjc.MsgSendNInt(error, selCode);
        var description = NSStringToManaged(LibObjc.MsgSendPtr(error, selLocalized), selUtf8);
        return (domain, code, description);
    }

    private static string? NSStringToManaged(IntPtr nsString, IntPtr selUtf8)
    {
        if (nsString == IntPtr.Zero) return null;
        var cstr = LibObjc.MsgSendPtr(nsString, selUtf8);
        return cstr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(cstr);
    }

    private static RegisterStatus ClassifyError(string? domain, long code)
    {
        // SMAppServiceErrorDomain codes observed in practice:
        //   1  kSMErrorToolNotValid (signature/plist mismatch)
        //   2  kSMErrorLaunchDeniedByUser (requires approval)
        //   3  kSMErrorInvalidPlist
        //   4  kSMErrorInternalFailure
        //   108 requires approval (newer macOS)
        if (domain is "SMAppServiceErrorDomain")
        {
            return code switch
            {
                1 => RegisterStatus.Unsigned,
                2 or 108 => RegisterStatus.NotApproved,
                _ => RegisterStatus.Other
            };
        }
        // OSStatus-style errSecCSUnsigned etc
        if (code == -67062) return RegisterStatus.Unsigned;
        return RegisterStatus.Other;
    }
}
