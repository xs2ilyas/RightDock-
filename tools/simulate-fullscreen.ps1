param([ValidateSet("on", "off")][string]$State = "on")

$ErrorActionPreference = "Stop"

Add-Type -Namespace Win32 -Name AppBar -MemberDefinition @'
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int max);
public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
'@

$process = Get-Process RightDock -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $process) { throw "RightDock is not running." }

$target = $process.Id
$script:found = [IntPtr]::Zero
$callback = [Win32.AppBar+EnumProc]{
    param($handle, $lParam)
    $windowPid = 0
    [Win32.AppBar]::GetWindowThreadProcessId($handle, [ref]$windowPid) | Out-Null
    if ($windowPid -eq $target) {
        $title = New-Object System.Text.StringBuilder 256
        [Win32.AppBar]::GetWindowText($handle, $title, 256) | Out-Null
        if ($title.ToString() -eq "Right Dock") {
            $script:found = $handle
            return $false
        }
    }
    return $true
}
[Win32.AppBar]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null

if ($script:found -eq [IntPtr]::Zero) { throw "Dock window not found." }

$message = [Win32.AppBar]::RegisterWindowMessage("RightDock_AppBarCallback")
$lParam = if ($State -eq "on") { [IntPtr]1 } else { [IntPtr]::Zero }
[Win32.AppBar]::PostMessage($script:found, $message, [IntPtr]2, $lParam) | Out-Null
Write-Output ("posted ABN_FULLSCREENAPP " + $State + " to " + $script:found)
