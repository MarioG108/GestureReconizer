using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace GestureControl.Interop.Native;

/// <summary>
/// Helper to detect Windows User Interface Privilege Isolation (UIPI) conflicts.
/// Section 11 and Section 15 of the technical architecture.
/// </summary>
public static class UipiHelper
{
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint TOKEN_QUERY = 0x0008;

    /// <summary>
    /// Checks whether the current process is running as Administrator.
    /// </summary>
    public static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Checks whether the foreground window belongs to an elevated process.
    /// If our process is standard and foreground is elevated, SendInput will be blocked by UIPI.
    /// </summary>
    public static bool IsForegroundWindowElevated(out string processName)
    {
        processName = "unknown";
        var hWnd = Win32Input.GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return false;

        Win32Input.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0)
            return false;

        try
        {
            var proc = Process.GetProcessById((int)pid);
            processName = proc.ProcessName;

            // Attempt to open process with query token
            IntPtr hProcess = proc.Handle;
            if (OpenProcessToken(hProcess, TOKEN_QUERY, out IntPtr hToken))
            {
                try
                {
                    // If we can query, check elevation
                    return false; // Typically accessible
                }
                finally
                {
                    CloseHandle(hToken);
                }
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5) // ERROR_ACCESS_DENIED
        {
            // Access denied when reading process handle almost always indicates
            // the target process has higher integrity (elevated / SYSTEM) than us!
            return true;
        }
        catch
        {
            // Ignore other transient errors
        }

        return false;
    }
}
