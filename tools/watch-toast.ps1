param(
    [int]$Seconds = 120,
    [string]$Shot = "C:\Users\DELL\AppData\Local\Temp\rdphase2\toast.png",
    [switch]$Click
)

Add-Type -AssemblyName System.Drawing
Add-Type -Namespace Win32 -Name W -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int max);
public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
'@
[Win32.W]::SetProcessDPIAware() | Out-Null

$proc = Get-Process RightDock -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "RightDock not running"; exit 1 }

$deadline = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $deadline) {
    $found = $null
    $target = $proc.Id
    $cb = [Win32.W+EnumProc]{
        param($h, $l)
        $pid2 = 0
        [Win32.W]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $target -and [Win32.W]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [Win32.W]::GetWindowText($h, $sb, 256) | Out-Null
            $title = $sb.ToString()
            $r = New-Object Win32.W+RECT
            [Win32.W]::GetWindowRect($h, [ref]$r) | Out-Null
            $w = $r.Right - $r.Left
            $ht = $r.Bottom - $r.Top
            if ($title -eq "" -and $w -gt 200 -and $w -lt 700 -and $ht -gt 40 -and $ht -lt 400) {
                $script:found = [pscustomobject]@{ Left = $r.Left; Top = $r.Top; Width = $w; Height = $ht }
            }
        }
        return $true
    }
    [Win32.W]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

    if ($found) {
        $bmp = New-Object System.Drawing.Bitmap $found.Width, $found.Height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($found.Left, $found.Top, 0, 0, (New-Object System.Drawing.Size $found.Width, $found.Height))
        $bmp.Save($Shot)
        Write-Output ((Get-Date -Format "HH:mm:ss") + " toast " + $found.Left + "," + $found.Top + " " + $found.Width + "x" + $found.Height + " saved " + $Shot)
        if ($Click) {
            $cx = [int]($found.Left + $found.Width / 2)
            $cy = [int]($found.Top + $found.Height / 2)
            [Win32.W]::SetCursorPos($cx, $cy) | Out-Null
            Start-Sleep -Milliseconds 200
            [Win32.W]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
            [Win32.W]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
            Write-Output ("clicked toast at " + $cx + "," + $cy)
        }
        exit 0
    }

    Start-Sleep -Milliseconds 250
}

Write-Output "no toast window seen"
exit 1
