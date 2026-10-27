param([string]$Executable)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$exe=Join-Path $PSScriptRoot '../artifacts/TeleGramBlog-1.2-win-x64/TeleGramBlog.App.exe'
if($Executable){$exe=(Resolve-Path -LiteralPath $Executable).Path}
$p=Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
try {
 Start-Sleep -Seconds 7
 $p.Refresh()
 if($p.HasExited){throw "App exited: $($p.ExitCode)"}
 $w=[System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
 $elements=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $names=@($elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 $names | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot '../artifacts/login-ui.json')
 if($names -contains 'Connection settings'){throw 'Developer settings still visible'}
 $phone=$elements | Where-Object {$_.Current.Name -eq 'Log in with phone number'} | Select-Object -First 1
 if(!$phone){throw 'QR is not the default login mode'}
 $phone.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 400
 $elements=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $qr=$elements | Where-Object {$_.Current.Name -eq 'Log in with QR code'} | Select-Object -First 1
 if(!$qr){throw 'Phone fallback not displayed'}
 $qr.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 400
 $elements=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $preview=$elements | Where-Object {$_.Current.Name -like 'Explore the design preview*'} | Select-Object -First 1
 if(!$preview){throw 'Preview button not found'}
 $preview.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Seconds 3
 $elements=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $names=@($elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 $names | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot '../artifacts/preview-ui.json')
 if(!($names -contains 'Design inspiration')){throw 'Preview topic not displayed'}
 $next=$elements | Where-Object {$_.Current.Name -eq 'Next page'} | Select-Object -First 1
 $next.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 800
 $elements=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $names=@($elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names | Where-Object {$_ -like 'Page 2*'})){throw 'Next page failed'}
 $details=$elements | Where-Object {$_.Current.Name -eq 'View image'} | Select-Object -First 1
 $details.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 Start-Sleep -Milliseconds 800
 $elements=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 $names=@($elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names -contains 'Post details')){throw 'Post details failed'}
 [pscustomobject]@{QrDefault='PASS';PhoneFallback='PASS';NoApiForm='PASS';Preview='PASS';NextPage='PASS';PostDetails='PASS';Title=$p.MainWindowTitle;Screenshot='Unavailable: sandbox desktop capture has no valid screen handle'} | Format-List
} finally {if(!$p.HasExited){Stop-Process -Id $p.Id}}

