
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
$timerlimit = 5			# timer limit in seconds. if passed, the current running app will be blacklisted.
$deltabias = 20			# gpulimit, if |CPU - GPU| < 20
$loadforcegpulimit = 80	# if cpuload >= 80, force gpulimit (lower priority than deltabias)
$powerforcethrottl = 60 # if total power < 60, force gpulimit
$smoothness_pwr = 5		# smoothness for moving average of cpu power. if 5, 4(old) + 1(new) / 5 = avg.
$sharpness_load = -3	# sharpness for moving average of cpu/gpu load calc. if 3, 1(old) + 2(new) / 3 = avg. if -3, 2(old) + 1(new) / 3 = avg.
$delaycpu = 1			# delay from sudden gpulimit (only under deltabias)
$delaygpu = 0			# delay from sudden gpudefault (only under deltabias)
$throttlechange = 5		# delay from sudden throttle clear (will also be used for reducing frequent switches)
$isdebug = $false		# dont print debug stuff
$notnvidia = 1			# dont run nvidiainspector(i dont use nvidia gpu)

# for gpulimit
$clockoffset = -950
$memoffset = -1000

# for gpudefault(off)
$defclockoffset = 0
$defmemoffset = 0

$minpark = [math]::ceiling(100 / (Get-WmiObject Win32_Processor).NumberOfCores)
$upperlim = $minpark * ((Get-WmiObject Win32_Processor).NumberOfCores - 1)	# arbitrary upper delta limit
$deltalim = $minpark * ((Get-WmiObject Win32_Processor).NumberOfCores) / 2	# arbitrary under delta limit

# better cpu scheduler tuning
# powersettings(HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Power\PowerSettings)
$processor_power_management_guids = @{
"06cadf0e-64ed-448a-8927-ce7bf90eb35d" = -1					# processor high threshold; lower this for performance
"12a0ab44-fe28-4fa9-b3bd-4b64f44960a6" = -1					# processor low threshold; upper this for batterylife
"40fbefc7-2e9d-4d25-a185-0cfd8574bac6" = -1					# processor low plan(0:normal, 1:step, 2:rocket)
"465e1f50-b610-473a-ab58-00d1077dc418" = -1					# processor high plan(0:normal, 1:step, 2:rocket)
"4d2b0152-7d5c-498b-88e2-34345392a2c5" = -1					# scheduler timing in milliseconds
"0cc5b647-c1df-4637-891a-dec35c318583" = -1					# processor noparking minimum
"ea062031-0e34-4ff1-9b6d-eb1059334028" = -1					# processor noparking maximum
"2430ab6f-a520-44a2-9601-f7f23b5134b1" = -1
"2ddd5a84-5a71-437e-912a-db0b8c788732" = -1					# core parking interval
"36687f9e-e3a5-4dbf-b1dc-15eb381c6863" = -1					# processor energy performance policy
"3b04d4fd-1cc7-4f23-ab1c-d1337819c4bb" = -1					# allow throttle state
"447235c7-6a8d-4cc0-8e24-9eaf70b96e2b" = -1					# cpu state on parked(1:highest, 2:lowest)
"45bcc044-d885-43e2-8605-ee0ec6e96b59" = -1					# opportunistically increase state
"4b92d758-5a24-4851-a470-815d78aee119" = -1					# demote from idle threshold
"4bdaf4e9-d103-46d7-a5f0-6280121616ef" = -1					# core parking distribution threshold
"4e4450b3-6179-4e91-b8f1-5bb9938f81a1" = -1					# duty cycle or not
"5d76a2ca-e8c0-402f-a133-2158492d58ad" = -1					# idle disabled or not
"616cdaa5-695e-4545-97ad-97dc2d1bdd88" = -1					# minimum unparked cores when latency hint
"619b7505-003b-4e82-b7a6-4dd29c300971" = -1					# processor perf on latency hint
"6c2993b0-8f48-481f-bcc6-00dd2742aa06" = -1					# processor idle scaling type
"71021b41-c749-4d21-be74-a00f335d582b" = -1					# cores to park when fewer cores required
"75b0ae3f-bce0-45a7-8c89-c9611c25e100" = -1					# max cpu frequency
"7b224883-b3cc-4d79-819f-8374152cbe7c" = -1					# promote to deeper idle threshold
"7d24baa7-0b84-480f-840c-1b0743c00f5f" = -1					# n intervals to use when calculating next
"893dee8e-2bef-41e0-89c6-b55d0929964c" = -1					# lower bound for performance throttling!!!
"8baa4a8a-14c6-4451-8e8b-14bdbd197537" = -1					# autonomously set state or not
"943c8cb6-6f93-4227-ad87-e9a3feec08d1" = -1					# upper threshold until parked core is considered overutilized
"94D3A615-A899-4AC5-AE2B-E4D8F634367F" = -1					# cooling policy
"97cfac41-2217-47eb-992d-618b1977c907" = -1					# softpark latency
"984cf492-3bed-4488-a8f9-4286c97bf5aa" = -1
"9943e905-9a30-4ec1-9b99-44dd3b76f7a2" = -1					# deepest idle state that should be used(0~20)
}
$guid0 = 'scheme_current'		# powerplan(HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\
													# Control\Power\User\PowerSchemes)
$guid1 = '54533251-82be-4824-96c1-47b60b740d00'		# processor power management
$guid2 = 'bc5038f7-23e0-4960-96da-33abaf5935ec'		# processor high clockspeed limit
$guidy = '0cc5b647-c1df-4637-891a-dec35c318583'		# processor noparking minimum
$guidz = 'ea062031-0e34-4ff1-9b6d-eb1059334028'		# processor noparking maximum
$guid4 = '44f3beca-a7c0-460e-9df2-bb8b99e0cba6'		# intel graphics power management
$guid5 = '3619c3f2-afb2-4afc-b0e9-e7fef372de36'		# submenu of intel graphics power management
foreach($temp in $processor_power_management_guids.Keys){
	powercfg /attributes $guid1 $temp -ATTRIB_HIDE
	#copy DC setting to AC for maximum battery
	$dcsetting = (powercfg /query $guid0 $guid1 $temp | select-string -pattern "DC").tostring().split(' ')[-1]
	if($processor_power_management_guids[$temp] -ne -1){
		$dcsetting = $processor_power_management_guids[$temp]
	}
	powercfg /setacvalueindex $guid0 $guid1 $temp $dcsetting
	powercfg /setdcvalueindex $guid0 $guid1 $temp $dcsetting
}
# for intel gpu
#powercfg /attributes $guid4 $guid5 -ATTRIB_HIDE
#powercfg /setdcvalueindex $guid0 $guid4 $guid5 1
#powercfg /setacvalueindex $guid0 $guid4 $guid5 1

# for proper sleep
$guida = 'F15576E8-98B7-4186-B944-EAFA664402D9'		# network on standby
powercfg /attributes 'sub_none' $guida -ATTRIB_HIDE
powercfg /setdcvalueindex $guid0 'sub_none' $guida 0	# off
powercfg /setacvalueindex $guid0 'sub_none' $guida 0	# off


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
$global:cpuminpark = 0
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
msg("upperlim: " + $upperlim + " deltalim: " + $deltalim)

function gpuset($mode){
	if($notnvidia -ne 1){
		if($mode -eq 1){
			nvidiaInspector -setBaseClockOffset:0,0,$clockoffset -setMemoryClockOffset:0,0,$memoffset
		}
		else{
			nvidiaInspector -setBaseClockOffset:0,0,$defclockoffset -setMemoryClockOffset:0,0,$defmemoffset
		}
	}
}

# main logic
function gpulimit{
	if($global:switchdelay -ge $global:delaychange -Or ($global:switchbound -eq 1 -And`
	$global:switchdelay -ge 0)){
		if($global:gpuswitch -eq 0){
			# prevent from switching too fast
			$global:switchindicator = $throttlechange
			$global:delaychange = $delaycpu + $throttlechange
			$global:delaychange2 = $delaygpu + $throttlechange
			
			gpuset(1)
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
					
					gpuset(0)
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

$global:firsttime = $true

#for cpuminpark
# cpulimit(0) -> limit cores
# cpulimit(1) -> dont limit cores
# cpulimit(2) -> keep status quo(for cpu throttle)
function cpulimit($idleness){
	$prevlimit = $global:cpulimitval
	$prevminpark = $global:cpuminpark
	
	#cpulimit
	if($global:cputhrottle -ne 2 -Or $global:result -eq $true){
		$global:cpulimitval = 100
	}
	else{
		$global:cpulimitval = 99
	}
	#cpuminpark
	if($idleness -eq 1 -And $global:result -ne $true){
		$global:cpuminpark = 0
	}
	elseif($idleness -ne 2){
		$global:cpuminpark = 100 - $minpark
	}
	
	#apply
	if($global:firsttime -eq $true -Or $prevlimit -ne $global:cpulimitval -Or $prevminpark -ne $global:cpuminpark){
		powercfg /setdcvalueindex $guid0 $guid1 $guid2 $global:cpulimitval
		powercfg /setacvalueindex $guid0 $guid1 $guid2 $global:cpulimitval
		powercfg /setdcvalueindex $guid0 $guid1 $guidy $global:cpuminpark
		powercfg /setacvalueindex $guid0 $guid1 $guidy $global:cpuminpark
		powercfg /setdcvalueindex $guid0 $guid1 $guidz ($global:cpuminpark + $minpark)
		powercfg /setacvalueindex $guid0 $guid1 $guidz ($global:cpuminpark + $minpark)
		# set powerplan active
		powercfg /setactive $guid0
		msg("cpulimit adjusted to "+$global:cpulimitval+", minpark "+($global:cpuminpark + $minpark))
		$global:firsttime = $false
	}
}

# add to blacklist
function bmsg{
	# print by date and time
	$mystring = "`n" + $global:process_str
	$mystring | out-file $loc"\gpu_scheduler_config\blacklist_programs.txt" -Encoding ASCII -Append
}

# first time
cpulimit(1)
gpuset(0)

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
	if($sharpness_load -ge 2){
		$global:load = ($global:prev_load + $global:load * ($sharpness_load - 1)) / $sharpness_load
	}
	elseif($sharpness_load -le -2){
		#less than -1
		$global:load = ($global:prev_load * (($sharpness_load * -1) - 1) + $global:load) / ($sharpness_load * -1)
	}
	else{
		$global:load = ($global:prev_load + $global:load) / 2
	}
	$global:prev_load = $global:load
	
	# check gpu copy usage to ident if game is running
	# gpu copy usage stays dead zero if not used.
	$global:delta = 0
	$counterSamples = Get-Counter "\GPU Engine(*engtype_Copy)\Utilization Percentage"`
	-ErrorAction SilentlyContinue
	foreach ($item in $counterSamples.CounterSamples.CookedValue) {
		if ($global:delta -lt $item) {
			$global:delta = $item
		}
	}
	#foreach($item in (Get-Counter "\GPU Engine(*engtype_Copy)\Utilization Percentage"`
	#-ErrorAction SilentlyContinue).CounterSamples.CookedValue){
	#	if($global:delta -lt $item){
	#		$global:delta = $item
	#	}
	#}
	
	# gpu load(for the running gpu)
	$global:delta3d = $global:delta
	$counterSamples = Get-Counter "\GPU Engine(*engtype_3D)\Utilization Percentage"`
	-ErrorAction SilentlyContinue
	foreach ($item in $counterSamples.CounterSamples.CookedValue) {
		if ($global:delta3d -lt $item) {
			$global:delta3d = $item
		}
	}
	$counterSamples = Get-Counter "\GPU Engine(*engtype_Graphic*)\Utilization Percentage"`
	-ErrorAction SilentlyContinue
	foreach ($item in $counterSamples.CounterSamples.CookedValue) {
		if ($global:delta3d -lt $item) {
			$global:delta3d = $item
		}
	}
	#if($global:gpuswitch -eq 0){
	#	$global:delta3d *= 2	# prevent gpu usage from fluctuating
	#}
	if($sharpness_load -ge 2){
		$global:delta3d = ($global:prev_delta3d + $global:delta3d * ($sharpness_load - 1)) / $sharpness_load
	}
	elseif($sharpness_load -le -2){
		#less than -1
		$global:delta3d = ($global:prev_delta3d * (($sharpness_load * -1) - 1) + $global:delta3d) / ($sharpness_load * -1)
	}
	else{
		$global:delta3d = ($global:prev_delta3d + $global:delta3d) / 2
	}
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
		cpulimit(2)
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
		cpulimit(0)		#full cores on blacklisted apps
		gpudefault
	}
	elseif($global:delta -le $limit -And`
	(($global:delta3d -lt $upperlim -And $global:load -lt $upperlim -And $global:cpuminpark -eq 0) -Or`
	($global:delta3d -lt $deltalim -And $global:load -lt $deltalim -And $global:cpuminpark -ne 0))){
		# cpuminpark = 0 means cpu idle
		$global:msgswitch = 0
		# if gpu idle, gpudefault
		$global:policyflip = 0
		$global:switchbound = 1
		cpulimit(1)		#limit cores on idle
		gpudefault
	}
	else{
		# game is running
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
				cpulimit(2)
				gpulimit
			}
			elseif($global:cputhrottle -eq 1){
				$global:msgswitch = 0
				$global:throttle_str = $global:process_str
				$global:cputhrottle = 2
				$global:reason = "cpu is still throttling!!! - cpulimit"
				cpulimit(2)
				gpulimit
			}
		}
		else{
			if($global:load -ge $loadforcegpulimit){
				if($global:deltafinal -le $deltabias){
					# high cpuload but cpu and gpu load diff is small
					$global:msgswitch = 0
					$global:policyflip = 0
					$global:reason = "(high cpuload) cpu and gpu load diff is small"
					cpulimit(0)
					gpudefault
				}
				elseif($global:load -gt $global:delta3d){
					# cpu oriented game
					$global:msgswitch = 0
					$global:reason = "(high cpuload) cpu oriented game"
					cpulimit(0)
					gpulimit
				}
			}
			elseif($global:deltafinal -le $deltabias){
				# cpu and gpu load diff is small (likely gpu oriented)
				$global:msgswitch = 0
				$global:policyflip = 0
				$global:reason = "cpu and gpu load diff is small"
				cpulimit(0)
				gpudefault
			}
			elseif($global:load -gt $global:delta3d){
				# cpu and gpu load diff is large and cpu load is greater
				$global:msgswitch = 0
				$global:reason = "cpu load is greater"
				cpulimit(0)
				gpulimit
			}
			else{
				# probably gpu oriented game
				$global:msgswitch = 0
				$global:policyflip = 0
				$global:reason = "probably gpu oriented game"
				cpulimit(0)
				gpudefault
			}
		}
	}
	
	if($isdebug -eq $true){
		msg("cpu usage = " + [math]::ceiling($global:load) + ", gpu usage = " + [math]::ceiling(`
		$global:delta3d) + ", gpu delta = " + [math]::ceiling($global:delta) + ", cpu power = "`
		+ [math]::ceiling($global:currpwr) + "/" + [math]::ceiling(($global:totalpwr`
		* $powerforcethrottl / 100)))
		#msg("gpuswitch = " + $global:gpuswitch + ", switchdelay = " + $global:switchdelay`
		#+ ", switchdelay2 = " + $global:switchdelay2)
	}
	
	$sw.Stop()
	$remainingtime = ($sleeptime - $sw.Elapsed.Seconds)
	if($remainingtime -gt 0){
		start-sleep $remainingtime
	}
	# attempt to figure out if app hogs the cpu in its entirety.
	# if timer cycle is delayed way too far, toss it into the blacklist, so that it starts with full cores next time.
	if($sw.Elapsed.Seconds -gt $timerlimit -And $global:result -eq $false){
		cpulimit(0)
		bmsg
		msg($global:process_str + ": chucked this cpu hog into the blacklist.")
	}
}
