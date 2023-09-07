
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
$deltabias = 20			# gpulimit, if |CPU - GPU| < 20
$loadforcegpulimit = 80	# if cpuload >= 80, force gpulimit (lower priority than deltabias)
$powerforcethrottl = 60 # if total power < 60, force gpulimit
$smoothness_pwr = 5		# smoothness for moving average of cpu power. if 10, 9(old) + 1(new) / 10 = avg.
$sharpness_load = 5		# sharpness for moving average of cpu/gpu load calc. if 10, 1(old) + 9(new) / 10 = avg.
$delaycpu = 1			# delay from sudden gpulimit (only under deltabias)
$delaygpu = 0			# delay from sudden gpudefault (only under deltabias)
$throttlechange = 5		# delay from sudden throttle clear (will also be used for reducing frequent switches)
$isdebug = $false		# dont print debug stuff

# for gpulimit
$clockoffset = -950
$memoffset = -1000

# for gpudefault(off)
$defclockoffset = 0
$defmemoffset = 0

# better cpu scheduler tuning
# powersettings(HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Power\PowerSettings)
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
$guid0 = '381b4222-f694-41f0-9685-ff5bb260df2e'		# powerplan(HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\
													# Control\Power\User\PowerSchemes)
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
# for intel gpu
powercfg /attributes $guid4 $guid5 -ATTRIB_HIDE
powercfg /setdcvalueindex $guid0 $guid4 $guid5 1
powercfg /setacvalueindex $guid0 $guid4 $guid5 1

# for proper sleep
$guida = '501a4d13-42af-4429-9fd1-a8218c268e20'		# PCI Express
$guidb = 'ee12f906-d277-404b-b6da-e5fa1a576df5'		# Link State Power Management
powercfg /attributes $guida $guidb -ATTRIB_HIDE
powercfg /setdcvalueindex $guid0 $guida $guidb 1	# moderate
powercfg /setacvalueindex $guid0 $guida $guidb 1	# moderate

# set powerplan active
powercfg /setactive $guid0

# internal stuff
$global:prev_load = 0
$global:prev_delta3d = 0
$global:load = 0
$global:delta = 0
$global:delta3d = 0
$global:gpuswitch = 0		# if 0 gpu limit, 1 gpu default
$global:switchdelay = 0
$global:switchdelay2 = 0
$global:switchdelay3 = 0
$global:delaychange = $delaycpu
$global:delaychange2 = $delaygpu
$global:switchindicator = 0	# probably switching too much
$global:switchbound = 0		# ignore delay and switch immediately
$global:policyflip = 0		# keep gpulimit until game end
$global:msgswitch = 0
$global:maxcpu = 0
$global:totalpwr = 0		# cpu power + gpu clock
$global:currpwr_n = $smoothness_pwr
$global:currpwr_v = 100 * $global:currpwr_n
$global:currpwr = $global:currpwr_v / $global:currpwr_n
$global:cputhrottle = 0
$global:cpulimitval = 100
$global:throttle_str = ""
$global:prev_process = ""
$global:status = 0			# 0 = gpudefault, 1 = gpulimit
$global:reason = ""
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
.scr
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
	if($global:switchdelay -ge $global:delaychange -Or ($global:switchbound -eq 1 -And`
	$global:switchdelay -ge 0)){
		if($global:gpuswitch -eq 0){
			# prevent from switching too fast
			$global:switchindicator = $throttlechange
			$global:delaychange = $delaycpu + $throttlechange
			$global:delaychange2 = $delaygpu + $throttlechange
					
			nvidiaInspector -setBaseClockOffset:0,0,$clockoffset -setMemoryClockOffset:0,0,$memoffset
			$global:status = 1
			if($global:result -eq $true){
				msg($global:process_str + ": gpulimit enabled. " + $global:reason)
			}
			else{
				msg("gpulimit enabled. " + $global:reason)
			}
			msg("cpu usage = " + [math]::ceiling($global:load) + ", gpu usage = " + [math]::ceiling(`
			$global:delta3d) + ", gpu delta = " + [math]::ceiling($global:delta) + ", cpu power = "`
			+ [math]::ceiling($global:currpwr) + "/" + [math]::ceiling(($global:totalpwr`
			* $powerforcethrottl / 100)))
		}
		else{
			if($global:switchindicator -gt 0){
				$global:switchindicator--
			}
			else{						
				$global:delaychange = $delaycpu
				$global:delaychange2 = $delaygpu
			}
		}
		$global:gpuswitch = 1
		$global:policyflip = 1	# flip here for switchdelay
	}
	$global:switchdelay++
	$global:switchdelay2 = 0
	$global:switchbound = 0
}

function gpudefault{
	if($global:cputhrottle -eq 0){
		if($global:policyflip -eq 0){
			if($global:switchdelay2 -ge $global:delaychange2 -Or $global:delta -le $limit -Or`
			($global:switchbound -eq 1 -And $global:switchdelay2 -ge 0)){
				if($global:gpuswitch -eq 1){
					# prevent from switching too fast
					$global:switchindicator = $throttlechange
					$global:delaychange = $delaycpu + $throttlechange
					$global:delaychange2 = $delaygpu + $throttlechange
					
					nvidiaInspector -setBaseClockOffset:0,0,$defclockoffset -setMemoryClockOffset:0,0,$defmemoffset
					$global:status = 0
					if($global:result -eq $true){
						msg($global:process_str + ": gpudefault enabled. " + $global:reason)
					}
					elseif($global:delta -le $limit){
						msg("gpudefault enabled. (gpu is off)")
					}
					else{
						msg("gpudefault enabled. " + $global:reason)
					}
					msg("cpu usage = " + [math]::ceiling($global:load) + ", gpu usage = "`
					+ [math]::ceiling($global:delta3d) + ", gpu delta = " + [math]::ceiling(`
					$global:delta) + ", cpu power = " + [math]::ceiling($global:currpwr) + "/"`
					+ [math]::ceiling(($global:totalpwr * $powerforcethrottl / 100)))
				}
				else{
					if($global:switchindicator -gt 0){
						$global:switchindicator--
					}
					else{						
						$global:delaychange = $delaycpu
						$global:delaychange2 = $delaygpu
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
	$global:switchbound = 0
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

# first time
nvidiaInspector -setBaseClockOffset:0,0,$defclockoffset -setMemoryClockOffset:0,0,$defmemoffset

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
	
	# total cpu load
	$global:load = 0
	foreach($item in (Get-Counter "\Processor(*)\% Processor Time" -ErrorAction SilentlyContinue).`
	CounterSamples.CookedValue){
		if($global:load -lt $item){
			$global:load = $item
		}
	}
	$kernelload = 0
	foreach($item in (Get-Counter "\Processor(*)\% Privileged Time" -ErrorAction SilentlyContinue).`
	CounterSamples.CookedValue){
		if($kernelload -lt $item){
			$kernelload = $item
		}
	}
	$global:load += $kernelload
	if($global:load -gt 100){
		$global:load = 100
	}
	$global:load = ($global:prev_load + $global:load * ($sharpness_load - 1)) / $sharpness_load
	$global:prev_load = $global:load
	
	# check gpu copy usage to ident if game is running
	# gpu copy usage stays dead zero if not used.
	$global:delta = 0
	foreach($item in (Get-Counter "\GPU Engine(*engtype_Copy)\Utilization Percentage"`
	-ErrorAction SilentlyContinue).CounterSamples.CookedValue){
		if($global:delta -lt $item){
			$global:delta = $item
		}
	}
	# gpu load(for the running gpu)
	$global:delta3d = $global:delta
	foreach($item in (Get-Counter "\GPU Engine(*)\Utilization Percentage"`
	-ErrorAction SilentlyContinue).CounterSamples.CookedValue){
		if($global:delta3d -lt $item){
			$global:delta3d = $item
		}
	}
	if($global:gpuswitch -eq 0){
		$global:delta3d *= 2	# prevent gpu usage from fluctuating
	}
	$global:delta3d = ($global:prev_delta3d + $global:delta3d * ($sharpness_load - 1)) / $sharpness_load
	$global:prev_delta3d = $global:delta3d
	
	# estimate total power for throttle check
	$maxpwrtempered = 0
	$maxtmp = 100 * $maxcputmp / $global:maxcpu
	$maxtmp = $maxtmp + (100 - $maxtmp) * (100 - $global:load) / 100
	# more weight if less than current
	#if($maxtmp -lt $global:currpwr){
	#	$global:currpwr_v = $global:currpwr + $maxtmp * ($global:currpwr_n - 1)
	#}
	#else{
	#	$global:currpwr_v = $global:currpwr * ($global:currpwr_n - 1) + $maxtmp
	#}
	$global:currpwr_v = $global:currpwr * ($global:currpwr_n - 1) + $maxtmp
	$global:currpwr = $global:currpwr_v / $global:currpwr_n
	#$global:currpwr = $global:currpwr + (100 - $global:currpwr) * (100 - $global:load) / 100
	if($global:totalpwr -lt $global:currpwr){
		$maxpwrtempered = 1
		$global:totalpwr = $global:currpwr
	}
	
	# cputhrottle flag clears when throttle ends
	if($global:cputhrottle -ne 0 -And (($maxpwrtempered -eq 0 -And $global:currpwr -ge ($global:totalpwr`
	* $powerforcethrottl / 100) -And $global:switchdelay3 -gt $throttlechange) -Or $global:delta`
	-le $limit)){
		if($global:cputhrottle -ne 0){
			msg("throttling cleared.")
		}
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
		$global:switchbound = 1
		gpudefault
	}
	elseif($global:delta -le $limit){
		$global:msgswitch = 0
		# if gpu idle, gpudefault
		$global:policyflip = 0
		$global:switchbound = 1
		gpudefault
	}
	elseif($global:delta -gt $limit){
		if($maxpwrtempered -eq 0 -And $global:currpwr -lt ($global:totalpwr`
		* $powerforcethrottl / 100)){
			# cpu usage is over limit while cpu power is not max
			# this means that cpu is throttling.
			$global:switchdelay3 = 0
			$global:switchbound = 1
			if($global:cputhrottle -eq 0){
				$global:msgswitch = 0
				$global:throttle_str = $global:process_str
				$global:cputhrottle = 1
				$global:reason = "cpu is throttling!!!"
				gpulimit
				cpulimit
			}
			elseif($global:cputhrottle -eq 1){
				$global:msgswitch = 0
				$global:throttle_str = $global:process_str
				$global:cputhrottle = 2
				$global:reason = "cpu is still throttling!!! - cpulimit"
				gpulimit
				cpulimit
			}
		}
		else{
			if($global:load -ge $loadforcegpulimit){
				if($global:deltafinal -le $deltabias){
					# high cpuload but cpu and gpu load diff is small
					$global:msgswitch = 0
					$global:policyflip = 0
					$global:reason = "(high cpuload) cpu and gpu load diff is small"
					gpudefault
				}
				elseif($global:load -gt $global:delta3d){
					# cpu oriented game
					$global:msgswitch = 0
					$global:reason = "(high cpuload) cpu oriented game"
					gpulimit
				}
			}
			elseif($global:deltafinal -le $deltabias){
				# cpu and gpu load diff is small (likely gpu oriented)
				$global:msgswitch = 0
				$global:policyflip = 0
				$global:reason = "cpu and gpu load diff is small"
				gpudefault
			}
			elseif($global:load -gt $global:delta3d){
				# cpu and gpu load diff is large and cpu load is greater
				$global:msgswitch = 0
				$global:reason = "cpu load is greater"
				gpulimit
			}
			else{
				# probably gpu oriented game
				$global:msgswitch = 0
				$global:policyflip = 0
				$global:reason = "probably gpu oriented game"
				gpudefault
			}
		}
	}
	
	$sw.Stop()
	if($isdebug -eq $true){
		msg("cpu usage = " + [math]::ceiling($global:load) + ", gpu usage = " + [math]::ceiling(`
		$global:delta3d) + ", gpu delta = " + [math]::ceiling($global:delta) + ", cpu power = "`
		+ [math]::ceiling($global:currpwr) + "/" + [math]::ceiling(($global:totalpwr`
		* $powerforcethrottl / 100)))
		#msg("gpuswitch = " + $global:gpuswitch + ", switchdelay = " + $global:switchdelay`
		#+ ", switchdelay2 = " + $global:switchdelay2)
	}
	start-sleep ($sleeptime - $sw.Elapsed.Seconds)
}
