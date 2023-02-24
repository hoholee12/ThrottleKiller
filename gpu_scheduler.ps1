
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
$deltabias = 30			# gpudefault, if |CPU - GPU| < 20
$loadforcegpulimit = 60	# if cpuload >= 60, force gpulimit
$powerforcethrottle = 80 # if total power < 80, force gpulimit
$smoothness = 10		# smoothness for upper moving average. if 10, 9(old) + 1(new) / 10 = avg.
$delaychange = 0		# delay once from sudden gpulimit
$delaychange2 = 2		# delay once from sudden gpudefault
$throttlechange = 8		# delay once from throttle clear
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
$global:load = 0
$global:delta = 0
$global:delta3d = 0
$global:gpuswitch = 0		# if 0 gpu limit, 1 gpu default
$global:switchdelay = 0
$global:switchdelay2 = 0
$global:switchdelay3 = 0
$global:policyflip = 0		# keep gpulimit until game end
$global:msgswitch = 0
$global:maxcpu = 0
$global:totalpwr = 0		# cpu power + gpu clock
$global:currpwr_n = $smoothness
$global:currpwr_v = 100 * $global:currpwr_n
$global:currpwr = $global:currpwr_v / $global:currpwr_n
$global:cputhrottle = 0
$global:cpulimitval = 100
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
teamviewer
tv
tslgame
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

function cpulimit{
	if($global:cputhrottle -ne 2){
		$global:cpulimitval = 100
	}
	else{
		$global:cpulimitval = 99
	}
	powercfg /setdcvalueindex $guid0 $guid1 $guid2 $global:cpulimitval
	powercfg /setacvalueindex $guid0 $guid1 $guid2 $global:cpulimitval
}

function gpudefault{
	if($global:cputhrottle -eq 0){
		if($global:policyflip -eq 0){
			if($global:switchdelay2 -ge $delaychange2 -Or $global:delta -le $limit){
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
}

# first time
nvidiaInspector -setBaseClockOffset:0,0,0 -setMemoryClockOffset:0,0,0

while($true){
	$sw = [Diagnostics.Stopwatch]::StartNew()
	checkFiles_myfiles
	checkSettings "blacklist_programs"

	$global:result = does_procname_exist
	
	# scale cpu usage based on clockspeed
	# correct cpu load
	$maxcputmp = (Get-Counter -Counter "\Processor Information(_Total)\% Processor Performance"`
	-ErrorAction SilentlyContinue).CounterSamples.CookedValue
	if($maxcputmp -gt $global:maxcpu){
		$global:maxcpu = $maxcputmp
	}
	
	# cpu load
	$global:load = 0
	foreach($item in (Get-Counter "\Processor(*)\% Processor Time" -ErrorAction SilentlyContinue).`
	CounterSamples.CookedValue){
		if($global:load -lt $item){
			$global:load = $item
		}
	}
	
	# gpu load(for the running gpu)
	$global:delta3d = 0
	foreach($item in (Get-Counter "\GPU Engine(*engtype_3D)\Utilization Percentage"`
	-ErrorAction SilentlyContinue).CounterSamples.CookedValue){
		if($global:delta3d -lt $item){
			$global:delta3d = $item
		}
	}
	
	# check gpu copy usage to ident if game is running
	# gpu copy usage stays dead zero if not used.
	$global:delta = 0
	foreach($item in (Get-Counter "\GPU Engine(*engtype_Copy)\Utilization Percentage"`
	-ErrorAction SilentlyContinue).CounterSamples.CookedValue){
		if($global:delta -lt $item){
			$global:delta = $item
		}
	}
	
	# estimate total power for throttle check
	$maxpwrtempered = 0
	$maxtmp = 100 * $maxcputmp / $global:maxcpu
	$maxtmp = $maxtmp + (100 - $maxtmp) * (100 - $global:load) / 100
	# more weight if less than current
	if($maxtmp -lt $global:currpwr){
		$global:currpwr_v = $global:currpwr + $maxtmp * ($global:currpwr_n - 1)
	}
	else{
		$global:currpwr_v = $global:currpwr * ($global:currpwr_n - 1) + $maxtmp
	}
	$global:currpwr = $global:currpwr_v / $global:currpwr_n
	#$global:currpwr = $global:currpwr + (100 - $global:currpwr) * (100 - $global:load) / 100
	if($global:totalpwr -lt $global:currpwr){
		$maxpwrtempered = 1
		$global:totalpwr = $global:currpwr
	}
	
	# cputhrottle flag clears when throttle ends
	if($global:cputhrottle -ne 0 -And (($maxpwrtempered -eq 0 -And $global:currpwr -ge ($global:totalpwr`
	* $powerforcethrottle / 100) -And $global:switchdelay3 -gt $throttlechange) -Or $global:delta`
	-le $limit)){
		msg("throttling cleared.")
		$global:cputhrottle = 0
		cpulimit
	}
	$global:switchdelay3++
	
	# abs of delta
	$global:deltafinal = $global:load - $global:delta3d
	if($global:deltafinal -lt 0){
		$global:deltafinal = $global:delta3d - $global:load
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
	elseif($global:delta -le $limit){
		$global:msgswitch = 0
		# if gpu idle, gpudefault
		$global:policyflip = 0
		gpudefault
	}
	elseif($global:delta -gt $limit){
		if($maxpwrtempered -eq 0 -And $global:currpwr -lt ($global:totalpwr * $powerforcethrottle / 100)){
			# cpu usage is over limit
			# while cpu power is not max
			# this means that cpu is throttling.
			$global:switchdelay3 = 0
			if($global:cputhrottle -eq 0){
				$global:msgswitch = 0
				$global:throttle_str = $global:process_str
				msg("cpu is throttling!!! - gpulimit")
				$global:cputhrottle = 1
				gpulimit
				cpulimit
			}
			elseif($global:cputhrottle -eq 1){
				$global:msgswitch = 0
				$global:throttle_str = $global:process_str
				msg("cpu is still throttling!!! - cpulimit")
				$global:cputhrottle = 2
				gpulimit
				cpulimit
			}
		}
		else{
			if($global:load -ge $loadforcegpulimit){
				if($global:deltafinal -le $deltabias){
					$global:msgswitch = 0
					# delta between cpu and gpu is small
					# it is not a cpu intensive game then.
					$global:policyflip = 0
					gpudefault
				}
				elseif($global:load -gt $global:delta3d){
					$global:msgswitch = 0
					# delta between cpu and gpu is huge
					# cpu usage is way higher than gpu usage
					# it is a cpu intensive game.
					gpulimit
				}
				else{
					# more gpu power
					$global:msgswitch = 0
					$global:policyflip = 0
					gpudefault
				}
			}
			else{
				# most non cpu intensive games will utilize more gpu power
				$global:msgswitch = 0
				$global:policyflip = 0
				gpudefault
			}
		}
	}
	
	$sw.Stop()
	if($isdebug -eq $true){
		msg("cpu usage = " + $global:load + ", gpu usage = " + $global:delta3d + ", gpu delta = "`
		+ $global:delta + ", cpu power = " + $global:currpwr + "/" + ($global:totalpwr`
		* $powerforcethrottle / 100))
		#msg("gpuswitch = " + $global:gpuswitch + ", switchdelay = " + $global:switchdelay`
		#+ ", switchdelay2 = " + $global:switchdelay2)
	}
	start-sleep ($sleeptime - $sw.Elapsed.Seconds)
}
