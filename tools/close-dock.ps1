param([switch]$Kill)

Add-Type -Namespace Win32 -Name Native -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
'@

$p = Get-Process RightDock -ErrorAction SilentlyContinue
if (-not $p) { Write-Output "not running"; exit 0 }

if ($Kill) {
    Stop-Process -Id $p.Id -Force
    Write-Output "killed $($p.Id)"
    exit 0
}

$h = [IntPtr]::Zero
Add-Type -Namespace Win32 -Name Enum -MemberDefinition @'
public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int max);
'@
$target = $p.Id
$cb = [Win32.Enum+EnumProc]{
    param($w, $l)
    $winPid = 0
    [Win32.Enum]::GetWindowThreadProcessId($w, [ref]$winPid) | Out-Null
    if ($winPid -eq $target -and [Win32.Enum]::IsWindowVisible($w)) {
        $sb = New-Object System.Text.StringBuilder 256
        [Win32.Enum]::GetWindowText($w, $sb, 256) | Out-Null
        if ($sb.ToString() -eq "Right Dock") { $script:found = $w; return $false }
    }
    return $true
}
[Win32.Enum]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$h = $script:found

if ($h -eq [IntPtr]::Zero) { Write-Output "no window handle"; exit 1 }

[Win32.Native]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 2

if (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) {
    Write-Output "still running"
    exit 1
}

Write-Output "closed"
