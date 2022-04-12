
# run it via task scheduler
#  PowerShell.exe -windowstyle hidden -executionpolicy remotesigned <scriptlocation>\gpu_scheduler.ps1 <scriptlocation>
#
#  <scriptlocation>: you need to put this in the same directory as nvidiainspector.exe.
#
#  (Run whether user is logged on or not is VERY UNRELIABLE)


#user config
$limit = 0	#upper limit for copy usage
$sleeptime = 5
$limitcpu = 50	#dont underclock gpu when there is no need for cpu
$delaychange = 1 #delay from sudden gpu underclock
$delaychange2 = 3 #delay from sudden gpu normalclock

#gpu config
$clockoffset = -950
$memoffset = -1000

#better cpu scheduler tuning
$processor_power_management_guids = @{
"06cadf0e-64ed-448a-8927-ce7bf90eb35d" = 30			# processor high threshold; lower this for performance
"0cc5b647-c1df-4637-891a-dec35c318583" = 100
"12a0ab44-fe28-4fa9-b3bd-4b64f44960a6" = 15			# processor low threshold; upper this for batterylife
"40fbefc7-2e9d-4d25-a185-0cfd8574bac6" = 1
"45bcc044-d885-43e2-8605-ee0ec6e96b59" = 100
"465e1f50-b610-473a-ab58-00d1077dc418" = 2
"4d2b0152-7d5c-498b-88e2-34345392a2c5" = 15
"893dee8e-2bef-41e0-89c6-b55d0929964c" = 5			# processor low clockspeed limit
"94d3a615-a899-4ac5-ae2b-e4d8f634367f" = 1
"bc5038f7-23e0-4960-96da-33abaf5935ec" = 100		# processor high clockspeed limit
"ea062031-0e34-4ff1-9b6d-eb1059334028" = 100
}
$guid0 = '381b4222-f694-41f0-9685-ff5bb260df2e'		# you can change to any powerplan you want as default!
$guid1 = '54533251-82be-4824-96c1-47b60b740d00'		# processor power management
$guid2 = 'bc5038f7-23e0-4960-96da-33abaf5935ec'		# processor high clockspeed limit
$guid3 = '893dee8e-2bef-41e0-89c6-b55d0929964c'		# processor low clockspeed limit

$guid4 = '44f3beca-a7c0-460e-9df2-bb8b99e0cba6'		# intel graphics power management
$guid5 = '3619c3f2-afb2-4afc-b0e9-e7fef372de36'		# submenu of intel graphics power management
foreach($temp in $processor_power_management_guids.Keys){
	powercfg /attributes $guid1 $temp -ATTRIB_HIDE
	powercfg /setdcvalueindex $guid0 $guid1 $temp $processor_power_management_guids[$temp]
	powercfg /setacvalueindex $guid0 $guid1 $temp $processor_power_management_guids[$temp]
}
powercfg /attributes $guid4 $guid5 -ATTRIB_HIDE
powercfg /setactive $guid0

#internal stuff
$delta = 0
$deltacpu = 0
$switching = 1
$switching2 = 1
$switchdelay = 0
$switchdelay2 = 0
#script assumes nothing is running at start.

# check custom location for settings
$loc = ".\"
$arg_len = $args[0].Length
if($arg_len -ne 0){
	if($args[0][[int]$arg_len - 1] -ne "\"){
		$loc = $args[0] + "\"
	
	}
	else{
		$loc = $args[0]
	
	}

}
$env:path = $env:path + ";"+ $loc

# create config files if not exist
function checkFiles([string]$setting_string, [string]$value_string){
	if((Test-Path ($loc + "gpu_scheduler_config\" + $setting_string + ".txt")) -ne $True){
		if((Test-Path ($loc + "gpu_scheduler_config")) -ne $True) {
			New-Item -path $loc -name "gpu_scheduler_config" -ItemType "directory"
			#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
			msg("created directory: " + $loc + "gpu_scheduler_config")
		}
		
		New-Item -path ($loc + "gpu_scheduler_config") -name ($setting_string + ".txt"`
		) -ItemType "file" -value $value_string
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg("created file: " + $setting_string + " in " + $loc + "gpu_scheduler_config")
	}
}

function checkFiles_myfiles{
	checkFiles "blacklist_programs"`
"ff7remake"
}

function checkSettings($setting_string){
	$currentModifiedDate = (Get-Item ($loc + "gpu_scheduler_config\" + $setting_string + ".txt"`
	)).LastWriteTime
	if($global:lastModifiedDate[$setting_string] -ne $currentModifiedDate){
		$global:isDateDifferent = $True
		$global:lastModifiedDate.Remove($setting_string)
		
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg($setting_string + " has been modified, reloading...")
		$global:reapplySettings = $True
		findFiles $setting_string
		
	}
	else{
		$global:isDateDifferent = $False
	}
}

# used for checking whether settings file was modified
$global:lastModifiedDate = @{}
$global:found_hash = @{}		#copy $found_hash after calling findFiles

function findFiles($setting_string){
	$file = Get-Content ($loc + "gpu_scheduler_config\" + $setting_string + ".txt")
	$global:lastModifiedDate.add($setting_string, (Get-Item ($loc + "gpu_scheduler_config\"`
	+ $setting_string + ".txt")).LastWriteTime)
	if ($? -eq $True)
	{
		$global:found_hash = @{}
		foreach ($line in $file)
		{
			$global:found_hash.add($line, 0)
		}
		# equivalent to 'eval'
		set-variable ("global:" + $setting_string) $global:found_hash
	}
}

findFiles "blacklist_programs"

#for foreground detection
Add-Type @"
  using System;
  using System.Runtime.InteropServices;
  public class Foreground {
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}
"@

$global:process_str = ""
function does_procname_exist{
	try{
		$error.clear()
		$fg = [Foreground]::GetForegroundWindow()
		$ret = get-process | ? { $_.mainwindowhandle -eq $fg }
		$global:process_str = $ret.processname.ToLower()
		if($error){
			$global:process_str = ""
			return $false
		}
		foreach($key in $blacklist_programs.Keys){
			if($global:process_str.Contains($key.ToLower())){
				return $true
			}
		}
		#foreach($key in $blacklist_programs.Keys)		#   $key value remains globally after break
		#{
		#	$temp = Get-Process -ErrorAction SilentlyContinue -Name ($key + '*')
		#	if ($temp -ne $null)
		#	{
		#		return $true
		#	}
		#}
	}
	catch{}
	return $false
}

#logging stuff
function msg([string]$setting_string){
	#print by date and time
	$setting_string = ((get-date -format "yy-MM-dd hh:mm:ss: ") + $setting_string)
	$setting_string >> ($loc + "gpu_scheduler_config\gpu_scheduler.log")
}
msg("script started. starting location: " + $loc)	#log script location


$maxcpu = (Get-WmiObject -class Win32_Processor)['MaxClockSpeed']
#main logic
while($true){
	$sw = [Diagnostics.Stopwatch]::StartNew()
	checkFiles_myfiles
	checkSettings "blacklist_programs"

	$result = does_procname_exist
	$getproc = Get-WmiObject -class Win32_Processor
	$deltacpu = $getproc['LoadPercentage']
	$curcpu = $getproc['CurrentClockSpeed']
	$deltacpu = $deltacpu*100/$maxcpu*$curcpu/100
	if($result -eq $false -And $deltacpu -gt $limitcpu){
		$delta = (((Get-Counter "\GPU Engine(*engtype_Copy)\Utilization Percentage").CounterSamples | where CookedValue).CookedValue | measure -sum).sum
		if ($delta -gt $limit){
			msg("delta: " + $delta)	#keep logging delta when game mode
			if ($switching -eq 0 -And $switchdelay -ge $delaychange -Or $switching2 -eq 0){
				nvidiaInspector -setBaseClockOffset:0,0,$clockoffset -setMemoryClockOffset:0,0,$memoffset
				$switching = 1
				$switching2 = 1
				msg($global:process_str + ": gpu is in game mode")
			}
			$switchdelay++
		}
		else{
			if($switching -eq 1 -Or $switching2 -eq 0){
				nvidiaInspector -setBaseClockOffset:0,0,0 -setMemoryClockOffset:0,0,0
				$switching = 0
				$switching2 = 1
				msg("delta: " + $delta)	#log delta only once on standby
				msg("gpu is sleeping")
			}
			$switchdelay = 0
		}
		$switchdelay2 = 0
	}
	else{
		if($switching2 -eq 1 -And $switchdelay2 -ge $delaychange2){
			nvidiaInspector -setBaseClockOffset:0,0,0 -setMemoryClockOffset:0,0,0
			$switching2 = 0
			msg("gpu is sleeping")
			if ($global:process_str.Contains(" ") -eq $false){	#nothing is on focus
				msg($global:process_str + ": blacklisted or lightweight program found.")
			}
			msg("cpu usage: " + $deltacpu)
		}
		$switchdelay2++
	}
	$sw.Stop()
	start-sleep ($sleeptime - $sw.Elapsed.Seconds)
}