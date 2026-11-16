param(
    [string]$ProcessName = "RightDock",
    [int]$Seconds = 30
)

$process = Get-Process $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $process) { throw "Process '$ProcessName' is not running." }

$startCpu = $process.TotalProcessorTime.TotalSeconds
$start = Get-Date
Start-Sleep -Seconds $Seconds
$process.Refresh()

$elapsed = ((Get-Date) - $start).TotalSeconds
$cpu = $process.TotalProcessorTime.TotalSeconds - $startCpu

"Process: {0} (PID {1})" -f $process.ProcessName, $process.Id
"ElapsedSeconds: {0:0.0}" -f $elapsed
"WorkingSetMB: {0:0.0}" -f ($process.WorkingSet64 / 1MB)
"PrivateMB: {0:0.0}" -f ($process.PrivateMemorySize64 / 1MB)
"CPUSeconds: {0:0.000}" -f $cpu
"CPUPercentOneCore: {0:0.00}" -f (100.0 * $cpu / $elapsed)
"Handles: {0}" -f $process.HandleCount
"Threads: {0}" -f $process.Threads.Count