using System;

namespace WinPartFlash.Gui.MacOS;

/// <summary>User cancelled the macOS authorization prompt.</summary>
public sealed class PrivilegedAuthorizationCancelledException : Exception
{
    public PrivilegedAuthorizationCancelledException() { }
    public PrivilegedAuthorizationCancelledException(string message) : base(message) { }
}

/// <summary>Authorization completed but the helper could not be reached.</summary>
public sealed class PrivilegedHelperUnavailableException : Exception
{
    public PrivilegedHelperUnavailableException(string message) : base(message) { }
    public PrivilegedHelperUnavailableException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Generic authorization failure (wrong password, policy, etc.).</summary>
public sealed class PrivilegedAuthorizationFailedException : Exception
{
    public PrivilegedAuthorizationFailedException(string message) : base(message) { }
}

/// <summary>The device is busy (mounted, in use). Caller may retry after unmounting.</summary>
public sealed class DeviceBusyException : Exception
{
    public DeviceBusyException(string message) : base(message) { }
}
