param([string]$Executable='artifacts/TeleGramBlog-1.7.1-win-x64/TeleGramBlog.App.exe')
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
   Write-Output "Invoke: $name"
   $condition=New-Object System.Windows.Automation.AndCondition -ArgumentList @((New-Object System.Windows.Automation.PropertyCondition -ArgumentList ([System.Windows.Automation.AutomationElement]::NameProperty,$name)),(New-Object System.Windows.Automation.PropertyCondition -ArgumentList ([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Button)))
   $button=$w.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
   if(!$button){throw "Button not found: $name"}
   $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
   Write-Output "Invoked: $name"
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
 if(!($names -contains 'Play')){throw 'Native video did not become ready; inspect artifacts/media-ui.json'}
 Invoke-Button 'Play';Start-Sleep -Milliseconds 500
 Invoke-Button 'Pause'
 $seek=Elements | Where-Object {$_.Current.Name -eq 'Seek'} | Select-Object -First 1
 Write-Output 'Located seek'
 if(!$seek){throw 'Seek control missing'}
 $range=$seek.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
 $middle=($range.Current.Minimum+$range.Current.Maximum)/2
 $range.SetValue($middle)
 Write-Output 'Seek set'
 Start-Sleep -Milliseconds 500
 if([Math]::Abs($range.Current.Value-$middle) -gt 0.5){throw 'Seek did not update playback position'}
 Invoke-Button 'Mute';Start-Sleep -Milliseconds 500
 $sliders=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition -ArgumentList ([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::Slider)))
 $volume=$sliders | Where-Object {$_.Current.ProcessId -eq $p.Id -and ($_.Current.AutomationId -eq 'VolumeSlider' -or $_.Current.Name -eq 'Volume')} | Select-Object -First 1
 if(!$volume){throw 'Native volume slider missing'}
 $volumeRange=$volume.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern);$volumeRange.SetValue(25);Start-Sleep -Milliseconds 200
 if([Math]::Abs($volumeRange.Current.Value-25) -gt 1){throw 'Volume did not update'}
 Invoke-Button 'กลับไปหัวข้อ';Start-Sleep -Milliseconds 500
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names -contains 'Design inspiration')){throw 'Back navigation failed'}
 Invoke-Button 'View album';Start-Sleep -Seconds 2
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names -contains '1 / 3')){$names | ConvertTo-Json | Set-Content -Encoding UTF8 artifacts/gallery-ui.json;throw 'Mixed album did not open with first photo'}
 Invoke-Button 'ซูมเข้า';Start-Sleep -Milliseconds 500
 if(!(@(Elements | ForEach-Object {$_.Current.Name}) -contains '1.25×')){throw 'Image zoom did not update'}
 Invoke-Button 'ปรับภาพให้พอดีพื้นที่';Start-Sleep -Milliseconds 500
 Invoke-Button 'ข้อมูลโพสต์';Start-Sleep -Milliseconds 300
 Invoke-Button 'ปิดข้อมูลโพสต์'
 Invoke-Button 'เต็มจอ';Start-Sleep -Milliseconds 600
 Invoke-Button 'ออกจากเต็มจอ';Start-Sleep -Milliseconds 600
 Invoke-Button 'เลือกวิดีโอ 2';Start-Sleep -Seconds 3
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names -contains '2 / 3') -or !($names -contains 'Play')){throw 'Gallery video selection failed'}
 Invoke-Button 'เลือกภาพ 3';Start-Sleep -Milliseconds 800
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names -contains '3 / 3')){throw 'Gallery photo selection failed'}
 $last=Elements | Where-Object {$_.Current.Name -eq 'สื่อถัดไป' -and $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button} | Select-Object -First 1
 if($last.Current.IsEnabled){throw 'Next must be disabled on the last item'}
 Invoke-Button 'กลับไปหัวข้อ';Start-Sleep -Milliseconds 500
 Invoke-Button 'Preview photo 3';Start-Sleep -Milliseconds 800
 Invoke-Button 'View album';Start-Sleep -Milliseconds 800
 $names=@(Elements | ForEach-Object {$_.Current.Name} | Where-Object {$_})
 if(!($names -contains '3 / 3')){throw 'Card selection was not passed to details'}
 [pscustomobject]@{PortraitCover='PASS';VideoOpened='PASS';Play='PASS';Pause='PASS';Seek='PASS';Volume='PASS';Back='PASS';ImageOpened='PASS';ZoomFit='PASS';Details='PASS';FullScreen='PASS';BoundedNavigation='PASS';MixedGallery='PASS';CardSelection='PASS';Sample='Generated 3-second H.264 clip'} | Format-List
} finally {if(!$p.HasExited){Stop-Process -Id $p.Id}}
