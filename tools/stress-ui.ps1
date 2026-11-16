param(
    [string]$ProcessName = "RightDock",
    [int]$Cycles = 5,
    [int]$DelayMs = 120
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -Namespace Win32 -Name Resources -MemberDefinition @'
[DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr hProcess, uint uiFlags);
'@

$modules = @(
    "Dashboard", "Focus", "Tasks", "Reminders", "Notes", "Habits", "Medication",
    "Health & Lifestyle", "Calendar", "Statistics", "Quick Tools", "Learning"
)

$process = Get-Process $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $process) { throw "Process '$ProcessName' is not running." }

$root = [System.Windows.Automation.AutomationElement]::RootElement
$processCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
$windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $processCondition)

$dock = $null
foreach ($window in $windows) {
    if ($window.Current.Name -eq "Right Dock") { $dock = $window; break }
}
if (-not $dock) { throw "Right Dock window was not found. Reveal the dock first." }

function Get-ModuleButton([string]$name) {
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $typeCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Button)
    $condition = New-Object System.Windows.Automation.AndCondition($nameCondition, $typeCondition)
    return $dock.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Get-Metrics {
    $process.Refresh()
    return [pscustomobject]@{
        PrivateMB = [math]::Round($process.PrivateMemorySize64 / 1MB, 1)
        WorkingSetMB = [math]::Round($process.WorkingSet64 / 1MB, 1)
        Handles = $process.HandleCount
        Gdi = [Win32.Resources]::GetGuiResources($process.Handle, 0)
        User = [Win32.Resources]::GetGuiResources($process.Handle, 1)
    }
}

$before = Get-Metrics
$samples = @()

for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
    foreach ($name in $modules) {
        $button = Get-ModuleButton $name
        if (-not $button) { throw "Dock button '$name' was not found." }

        $invoke = $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
        Start-Sleep -Milliseconds $DelayMs
        $invoke.Invoke()
        Start-Sleep -Milliseconds $DelayMs
    }

    $samples += Get-Metrics
    $sample = $samples[-1]
    "cycle {0}: private {1} MB, working {2} MB, handles {3}, gdi {4}, user {5}" -f `
        $cycle, $sample.PrivateMB, $sample.WorkingSetMB, $sample.Handles, $sample.Gdi, $sample.User
}

$after = Get-Metrics
"before: private {0} MB, working {1} MB, handles {2}, gdi {3}, user {4}" -f `
    $before.PrivateMB, $before.WorkingSetMB, $before.Handles, $before.Gdi, $before.User
"after:  private {0} MB, working {1} MB, handles {2}, gdi {3}, user {4}" -f `
    $after.PrivateMB, $after.WorkingSetMB, $after.Handles, $after.Gdi, $after.User
"delta:  private {0:0.0} MB, handles {1}, gdi {2}, user {3}" -f `
    ($after.PrivateMB - $before.PrivateMB), ($after.Handles - $before.Handles), `
    ($after.Gdi - $before.Gdi), ($after.User - $before.User)