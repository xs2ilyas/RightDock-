param(
    [string]$Action = "list",
    [string]$Window = "",
    [string]$Name = "",
    [string]$AutomationId = "",
    [string]$Value = "",
    [string]$Shot = "",
    [int]$X = -1,
    [int]$Y = -1
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type -Namespace Win32 -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int max);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
'@
[Win32.U]::SetProcessDPIAware() | Out-Null

$proc = Get-Process RightDock -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output "RightDock not running"; exit 1 }

function Get-TopWindows {
    $list = New-Object System.Collections.ArrayList
    $target = $proc.Id
    $cb = [Win32.U+EnumProc]{
        param($h, $l)
        $pid2 = 0
        [Win32.U]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $target -and [Win32.U]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [Win32.U]::GetWindowText($h, $sb, 256) | Out-Null
            $r = New-Object Win32.U+RECT
            [Win32.U]::GetWindowRect($h, [ref]$r) | Out-Null
            [void]$list.Add([pscustomobject]@{
                Handle = $h
                Title = $sb.ToString()
                Left = $r.Left; Top = $r.Top; Width = ($r.Right - $r.Left); Height = ($r.Bottom - $r.Top)
            })
        }
        return $true
    }
    [Win32.U]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $list
}

$windows = Get-TopWindows

if ($Action -eq "list") {
    $windows | Format-Table -AutoSize | Out-String -Width 200
    exit 0
}

if ($Action -eq "clickxy" -or $Action -eq "rightclickxy") {
    $down = 0x0002; $up = 0x0004
    if ($Action -eq "rightclickxy") { $down = 0x0008; $up = 0x0010 }
    [Win32.U]::SetCursorPos($X, $Y) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32.U]::mouse_event($down, 0, 0, 0, [UIntPtr]::Zero)
    [Win32.U]::mouse_event($up, 0, 0, 0, [UIntPtr]::Zero)
    Write-Output ("clicked at " + $X + "," + $Y)
    exit 0
}

if ($Action -eq "keys") {
    [System.Windows.Forms.SendKeys]::SendWait($Value)
    Write-Output "keys sent"
    exit 0
}

if ($Action -eq "invokeglobal" -or $Action -eq "clickglobal") {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $el = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $el) { Write-Output "global element not found (name=$Name)"; exit 1 }
    if ($Action -eq "invokeglobal") {
        $pattern = $null
        if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
            $pattern.Invoke(); Write-Output "invoked global"; exit 0
        }
        if ($el.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
            $pattern.Toggle(); Write-Output "toggled global"; exit 0
        }
        Write-Output "no invoke or toggle pattern"; exit 1
    }
    $rect = $el.Current.BoundingRectangle
    $x = [int]($rect.Left + $rect.Width / 2)
    $y = [int]($rect.Top + $rect.Height / 2)
    [Win32.U]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32.U]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win32.U]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Write-Output ("clicked global at " + $x + "," + $y)
    exit 0
}

$win = $windows | Where-Object { $_.Title -eq $Window } | Select-Object -First 1
if (-not $win -and $Window -ne "") {
    $win = $windows | Where-Object { $_.Title -like "*$Window*" } | Select-Object -First 1
}
if (-not $win) { Write-Output "window not found: $Window"; $windows | Format-Table -AutoSize | Out-String -Width 200; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::FromHandle($win.Handle)

function Find-Element {
    $cond = $null
    if ($AutomationId -ne "") {
        $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    } elseif ($Name -ne "") {
        $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    } else { return $null }
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

if ($Action -eq "shot") {
    $bmp = New-Object System.Drawing.Bitmap $win.Width, $win.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($win.Left, $win.Top, 0, 0, (New-Object System.Drawing.Size $win.Width, $win.Height))
    $bmp.Save($Shot)
    Write-Output ("saved " + $Shot + " " + $win.Width + "x" + $win.Height)
    exit 0
}

if ($Action -eq "elements") {
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) {
        $type = $el.Current.ControlType.ProgrammaticName -replace "ControlType\.", ""
        if ($type -in @("Button","ToggleButton","TextBox","Text","MenuItem","TabItem","CheckBox","ComboBox","ListItem","ComboBoxItem")) {
            Write-Output ("{0} | name='{1}' | id='{2}'" -f $type, $el.Current.Name, $el.Current.AutomationId)
        }
    }
    exit 0
}

$el = Find-Element
if (-not $el) { Write-Output "element not found (name=$Name id=$AutomationId)"; exit 1 }

if ($Action -eq "info") {
    $r = $el.Current.BoundingRectangle
    Write-Output ("type=" + $el.Current.ControlType.ProgrammaticName + " rect=" + $r.Left + "," + $r.Top + " " + $r.Width + "x" + $r.Height)
    exit 0
}

if ($Action -eq "invoke") {
    $pattern = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke(); Write-Output "invoked"; exit 0
    }
    if ($el.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
        $pattern.Toggle(); Write-Output "toggled"; exit 0
    }
    Write-Output "no invoke or toggle pattern"; exit 1
}

if ($Action -eq "expand" -or $Action -eq "collapse") {
    $pattern = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
        if ($Action -eq "expand") { $pattern.Expand() } else { $pattern.Collapse() }
        Write-Output ($Action + "ed")
        exit 0
    }
    Write-Output "no expand collapse pattern"
    exit 1
}

if ($Action -eq "select") {
    $pattern = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
        Write-Output "selected"
        exit 0
    }
    Write-Output "no selection item pattern"
    exit 1
}

if ($Action -eq "selected") {
    $pattern = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern, [ref]$pattern)) {
        $items = $pattern.Current.GetSelection()
        if ($items.Length -eq 0) { Write-Output "(none)" } else {
            foreach ($item in $items) { Write-Output ("selected: " + $item.Current.Name) }
        }
        exit 0
    }
    Write-Output "no selection pattern"
    exit 1
}

if ($Action -eq "click") {
    $rect = $el.Current.BoundingRectangle
    if ($rect.IsEmpty) { Write-Output "element has no bounds"; exit 1 }
    $x = [int]($rect.Left + $rect.Width / 2)
    $y = [int]($rect.Top + $rect.Height / 2)
    [Win32.U]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32.U]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [Win32.U]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Write-Output ("clicked at " + $x + "," + $y)
    exit 0
}

if ($Action -eq "set") {
    $pattern = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$pattern)) {
        $pattern.SetValue($Value); Write-Output "value set"; exit 0
    }
    Write-Output "no value pattern"; exit 1
}
