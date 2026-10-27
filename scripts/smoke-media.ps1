param([string]$Executable='artifacts/TeleGramBlog-1.6-win-x64/TeleGramBlog.App.exe')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$p=Start-Process -FilePath (Resolve-Path -LiteralPath $Executable).Path -ArgumentList '--design-preview' -PassThru -WindowStyle Hidden
try {
 Start-Sleep -Seconds 7
 $p.Refresh();if($p.HasExited){throw 'Application exited before media smoke test'}
 $w=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object {$_.Current.ProcessId -eq $p.Id -and $_.Current.Name -eq 'TeleGramBlog'} | Select-Object -First 1
 if(!$w){throw 'Application content window not found'}
 function Elements { $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) }
 function Invoke-Button([string]$name) {
   $button=Elements | Where-Object {$_.Current.Name -like $name -and $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button} | Select-Object -First 1
   if(!$button){throw "Button not found: $name"}
   $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
 }
 Invoke-Button 'Creative Exchange';Start-Sleep -Seconds 1
 Invoke-Button 'Design inspiration';Start-Sleep -Seconds 2
 $cover=Elements | Where-Object {$_.Current.Name -eq 'View video' -and $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button} | Select-Object -First 1
 if(!$cover){throw 'Video cover is not accessible'}
 $rect=$cover.Current.BoundingRectangle
 if($rect.Width -le 0 -or [Math]::Abs($rect.Height/$rect.Width-1.25) -gt 0.08){throw 'Card cover is not portrait 4:5'}
 Invoke-Button 'View video';Start-Sleep -Seconds 4
 $elements=Elements
 $names=@($elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 $names | ConvertTo-Json | Set-Content -Encoding UTF8 artifacts/media-ui.json
 if(!($names | Where-Object {$_ -like '*Ready to play*'})){throw 'Native video did not become ready; inspect artifacts/media-ui.json'}
 Invoke-Button 'Play';Start-Sleep -Milliseconds 500
 Invoke-Button 'Pause'
 $seek=Elements | Where-Object {$_.Current.Name -eq 'Seek'} | Select-Object -First 1
 if(!$seek){throw 'Seek control missing'}
 $range=$seek.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
 $middle=($range.Current.Minimum+$range.Current.Maximum)/2
 $range.SetValue($middle)
 Start-Sleep -Milliseconds 500
 if([Math]::Abs($range.Current.Value-$middle) -gt 0.5){throw 'Seek did not update playback position'}
 Invoke-Button 'Back';Start-Sleep -Milliseconds 500
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names -contains 'Design inspiration')){throw 'Back navigation failed'}
 Invoke-Button 'View album';Start-Sleep -Seconds 2
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names | Where-Object {$_ -like 'Image 1 of 3'})){$names | ConvertTo-Json | Set-Content -Encoding UTF8 artifacts/gallery-ui.json;throw 'Mixed album did not open with first photo'}
 Invoke-Button 'Select video 2';Start-Sleep -Seconds 3
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names | Where-Object {$_ -like 'Video 2 of 3*Ready to play*'})){throw 'Gallery video selection failed'}
 Invoke-Button 'Select photo 3';Start-Sleep -Milliseconds 800
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names | Where-Object {$_ -like 'Image 3 of 3'})){throw 'Gallery photo selection failed'}
 Invoke-Button 'Back';Start-Sleep -Milliseconds 500
 Invoke-Button 'Preview photo 3';Start-Sleep -Milliseconds 800
 Invoke-Button 'View album';Start-Sleep -Milliseconds 800
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names | Where-Object {$_ -eq 'Image 3 of 3'})){throw 'Card selection was not passed to details'}
 [pscustomobject]@{PortraitCover='PASS';VideoOpened='PASS';Play='PASS';Pause='PASS';Seek='PASS';Back='PASS';ImageOpened='PASS';MixedGallery='PASS';CardSelection='PASS';Sample='Generated 3-second H.264 clip'} | Format-List
} finally {if(!$p.HasExited){Stop-Process -Id $p.Id}}
