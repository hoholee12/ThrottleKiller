
# gpu_scheduler.ps1
# run it via task scheduler
#  PowerShell.exe -windowstyle hidden -executionpolicy remotesigned <scriptlocation>\gpu_scheduler.ps1 <scriptlocation>
#
#  <scriptlocation>: you need to put this in the same directory as nvidiainspector.exe.
#
#  (Run whether user is logged on or not is VERY UNRELIABLE)


# user config
$limit = 0				# GPU copy usage -> game is running if more than 0%
$sleeptime = 5			# wait 5 seconds before another run
$delaydelta = 10		# gpudefault = cpu-5:gpu+5, gpulimit = cpu+5:gpu-5
$deltabias = -20		# gpudefault = cpu-5:gpu-15, gpulimit = cpu+5:gpu-25
$loadforcegpulimit = 80	# if load >= 80, force gpulimit
$delaychange = 0		# do gpulimit directly
$delaychange2 = 0		# delay once from sudden gpudefault
$isdebug = $false		# dont print debug stuff

# gpu config
$clockoffset = -950
$memoffset = -1000

# better cpu scheduler tuning
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

# internal stuff
$global:delta = 0
$global:deltacpu = 0
$global:delta3d = 0
$global:gpuswitch = 0		# if 0 gpu limit, 1 gpu default
$global:switchdelay = 0
$global:switchdelay2 = 0
$global:policyflip = 0		# keep gpulimit until game end
$global:msgswitch = 0
$global:maxcpu = 100
$global:halfdelta = $delaydelta / 2.0
$global:cputhrottle = 0
$global:throttle_str = ""
$global:prev_process = ""
$global:status = 0			# 0 = gpudefault, 1 = gpulimit
# script assumes nothing is running at start.

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
			# print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
			msg("created directory: " + $loc + "gpu_scheduler_config")
		}
		
		New-Item -path ($loc + "gpu_scheduler_config") -name ($setting_string + ".txt"`
		) -ItemType "file" -value $value_string
		# print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg("created file: " + $setting_string + " in " + $loc + "gpu_scheduler_config")
	}
}

function checkFiles_myfiles{
	checkFiles "blacklist_programs"`
"moonlight
powerpnt
winword
excel
"
}

function checkSettings($setting_string){
	$currentModifiedDate = (Get-Item ($loc + "gpu_scheduler_config\" + $setting_string + ".txt"`
	)).LastWriteTime
	if($global:lastModifiedDate[$setting_string] -ne $currentModifiedDate){
		$global:isDateDifferent = $True
		$global:lastModifiedDate.Remove($setting_string)
		
		# print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
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
$global:found_hash = @{}		# copy $found_hash after calling findFiles

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

# for foreground detection
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
			$global:result = $false
			return $false
		}
		foreach($key in $blacklist_programs.Keys){
			if($global:process_str.Contains($key.ToLower())){
				$global:result = $true
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
	$global:result = $false
	return $false
}

# logging stuff
function msg([string]$setting_string){
	# print by date and time
	$setting_string = ((get-date -format "yy-MM-dd hh:mm:ss: ") + $setting_string)
	$setting_string >> ($loc + "gpu_scheduler_config\gpu_scheduler.log")
}
msg("script started. starting location: " + $loc)	# log script location

# main logic
function gpulimit{
	if($global:switchdelay -ge $delaychange){
		if($global:gpuswitch -eq 0){
			nvidiaInspector -setBaseClockOffset:0,0,$clockoffset -setMemoryClockOffset:0,0,$memoffset
			$global:status = 1
			if($global:result -eq $true){
				msg($global:process_str + ": gpulimit enabled.")
			}
			else{
				msg("gpulimit enabled.")
			}
		}
		$global:gpuswitch = 1
		$global:policyflip = 1	# flip here for switchdelay
	}
	$global:switchdelay++
	$global:switchdelay2 = 0
}

function gpudefault{
	if($global:policyflip -eq 0){
		if($global:switchdelay2 -ge $delaychange2 -Or $global:delta -le $limit -And $global:cputhrottle -eq 0){
			if($global:gpuswitch -eq 1){
				nvidiaInspector -setBaseClockOffset:0,0,0 -setMemoryClockOffset:0,0,0
				$global:status = 0
				if($global:result -eq $true){
					msg($global:process_str + ": gpudefault enabled.")
				}
				elseif($global:delta -le $limit){
					msg("gpudefault enabled.(gpu is off)")
				}
				else{
					msg("gpudefault enabled.")
				}
			}
			$global:gpuswitch = 0
		}
		$global:switchdelay2++
		$global:switchdelay = 0
	}
	else{
		if($global:result -eq $true){
			msg($global:process_str + ": gpudefault is bound by policyflip.")
		}
		else{
			msg("gpudefault is bound by policyflip.")
		}
	}
}

while($true){
	$sw = [Diagnostics.Stopwatch]::StartNew()
	checkFiles_myfiles
	checkSettings "blacklist_programs"

	$global:result = does_procname_exist
	# scale cpu usage based on clockspeed
	$maxcputmp = ((Get-Counter -Counter "\Processor Information(_Total)\% Processor Performance" -ErrorAction SilentlyContinue).`
	CounterSamples.CookedValue | measure -sum).sum
	if($global:maxcpu -lt $maxcputmp){
		$global:maxcpu = $maxcputmp
	}
	$global:load = ((Get-Counter "\Processor(_Total)\% Processor Time" -ErrorAction SilentlyContinue).`
	CounterSamples.CookedValue | measure -sum).sum
	$global:deltacpu = $global:load * $maxcputmp / $global:maxcpu
	# scale gpu usage based on clockspeed(assume gpudefault = gpulimit * 2)
	#$global:delta3d = ((Get-Counter "\GPU Engine(*engtype_3D)\Utilization Percentage" -ErrorAction SilentlyContinue).`
	#CounterSamples.CookedValue | measure -sum).sum / 2 * (2 - $global:gpuswitch) + $deltabias
	$global:delta3d = ((Get-Counter "\GPU Engine(*engtype_3D)\Utilization Percentage" -ErrorAction SilentlyContinue).`
	CounterSamples.CookedValue | measure -sum).sum + $deltabias
	$global:delta = ((Get-Counter "\GPU Engine(*engtype_Copy)\Utilization Percentage" -ErrorAction SilentlyContinue).`
	CounterSamples.CookedValue | measure -sum).sum
	
	if($global:status -eq 0){
		# gpudefault
		$global:deltacpu -= $global:halfdelta
		$global:delta3d += $global:halfdelta
	}
	else{
		# gpulimit
		$global:deltacpu += $global:halfdelta
		$global:delta3d -= $global:halfdelta
	}
	
	
	# cputhrottle flag clears when delay to gpudefault clears and process is finished
	if($global:switchdelay2 -ge $delaychange2 -And $global:throttle_str -ne $global:process_str){
		$global:cputhrottle = 0
	}
	
	if($global:result -eq $true){
		if($global:msgswitch -eq 0){
			if($global:process_str.Length -le 20){
				if($global:prev_process -ne $global:process_str){
					$global:prev_process = $global:process_str
					msg($global:process_str + ": blacklisted program found.")
				}
			}
		}
		$global:msgswitch = 1
		$global:policyflip = 0
		gpudefault
	}
	elseif($global:load -ge 100 -And $maxcputmp -lt 100){	# even less than base clockspeed
		#elseif($global:load -gt $processor_power_management_guids['06cadf0e-64ed-448a-8927-ce7bf90eb35d']`

		# cpu is throttling!!!
		$global:msgswitch = 0
		$global:cputhrottle = 1
		$global:throttle_str = $global:process_str
		msg("cpu is throttling!!!")
		gpulimit
	}
	elseif($global:delta -le $limit){
		$global:msgswitch = 0
		# if gpu idle, gpudefault
		$global:policyflip = 0
		gpudefault
	}
	elseif($global:delta -gt $limit){
		if($global:load -ge $loadforcegpulimit){
			$global:msgswitch = 0
			# if cpu heavy game, gpulimit
			gpulimit
		}
		elseif($global:deltacpu -le $global:delta3d){
			$global:msgswitch = 0
			# if no cpu but yes gpu, gpudefault
			$global:policyflip = 0
			gpudefault
		}
		elseif($global:deltacpu -gt $global:delta3d){
			$global:msgswitch = 0
			# if cpu heavy game, gpulimit
			gpulimit
		}	
	}
	
	$sw.Stop()
	if($isdebug -eq $true){
		msg("cpu usage = " + $global:deltacpu + ", gpu usage = " + $global:delta3d + ", gpu delta = " + $global:delta)
		#msg("gpuswitch = " + $global:gpuswitch + ", switchdelay = " + $global:switchdelay`
		#+ ", switchdelay2 = " + $global:switchdelay2)
	}
	start-sleep ($sleeptime - $sw.Elapsed.Seconds)
}
