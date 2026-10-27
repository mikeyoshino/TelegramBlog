param([string]$Executable='artifacts/TeleGramBlog-1.6-win-x64/TeleGramBlog.App.exe')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
$p=Start-Process -FilePath (Resolve-Path -LiteralPath $Executable).Path -ArgumentList '--design-preview' -PassThru -WindowStyle Hidden
try {
 Start-Sleep -Seconds 7
 $w=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition) | Where-Object {$_.Current.ProcessId -eq $p.Id -and $_.Current.Name -eq 'TeleGramBlog'} | Select-Object -First 1
 function Elements { $w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) }
 function Button([string]$name) {Elements | Where-Object {$_.Current.Name -eq $name -and $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button} | Select-Object -First 1}
 function Click([string]$name) { $b=Button $name;if(!$b){throw "Missing button: $name"};$b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
 function Has([string]$name) { [bool](Elements | Where-Object {$_.Current.Name -like $name} | Select-Object -First 1) }
 if(!(Has 'กลุ่มของฉัน')){throw 'Groups page missing'}
 $group=Button 'Creative Exchange'
 if(!$group.Current.IsKeyboardFocusable){throw 'Group is not keyboard focusable'}
 $group.SetFocus()
 $keyboard='Controls keyboard-focusable; OS focus unavailable'
 if([System.Windows.Automation.AutomationElement]::FocusedElement.Current.ProcessId -eq $p.Id){
   [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
   if([System.Windows.Automation.AutomationElement]::FocusedElement.Current.ProcessId -ne $p.Id){throw 'Tab focus left app'}
   $group.SetFocus();[System.Windows.Forms.SendKeys]::SendWait('{ENTER}');$keyboard='Tab and Enter verified'
 } else {Click 'Creative Exchange'}
 Start-Sleep -Seconds 1
 if(!(Has 'เลือกหัวข้อเพื่อดูโพสต์')){throw 'Forum overview not shown'}
 Click 'Design inspiration';Start-Sleep -Seconds 2
 if(!(Has '*54 โพสต์ที่โหลดแล้ว*')){throw 'Loaded-only count missing'}
 $search=Elements | Where-Object {$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit -and $_.Current.Name -like '*ค้นหาโพสต์*'} | Select-Object -First 1
 if(!$search){throw 'Scoped post search missing'}
 $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('__no_match__')
 Start-Sleep -Milliseconds 500
 if(!(Has 'ไม่พบโพสต์ที่ตรงกับคำค้น')){throw 'Search-empty state missing'}
 if(Has 'ยังไม่มีโพสต์สื่อในหัวข้อนี้'){throw 'Contradictory empty state'}
 Click 'ล้างคำค้น';Start-Sleep -Milliseconds 500
 Click 'ยุบหรือขยายหัวข้อ';Start-Sleep -Milliseconds 300
 if(!(Has 'Design inspiration')){throw 'Collapsing sidebar lost selected topic'}
 Click 'ยุบหรือขยายหัวข้อ'
 Click 'ภาพรวมกลุ่ม';Start-Sleep -Milliseconds 500
 if(!(Has 'เลือกหัวข้อเพื่อดูโพสต์')){throw 'Back to overview failed'}
 Click 'กลุ่มทั้งหมด';Start-Sleep -Milliseconds 500
 if(!(Has 'รีเฟรชรายการกลุ่ม') -and !(Has '*รีเฟรชรายการกลุ่ม*')){throw 'Back to groups failed'}
 Click 'เมนูบัญชี';Start-Sleep -Milliseconds 500
 $desktop=[System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
 if(!($desktop | Where-Object {$_.Current.ProcessId -eq $p.Id -and $_.Current.Name -eq 'ออกจากระบบ Telegram'} | Select-Object -First 1)){throw 'Account menu does not contain logout'}
 [pscustomobject]@{Groups='PASS';Overview='PASS';Posts='PASS';SearchStates='PASS';Sidebar='PASS';BackNavigation='PASS';AccountMenu='PASS';Keyboard=$keyboard} | Format-List
} finally {if(!$p.HasExited){Stop-Process -Id $p.Id}}
