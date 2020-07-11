# adaptive power management script (for laptops with intel gpu) written by dj_manual

# run it via task scheduler
#  PowerShell.exe -windowstyle hidden -executionpolicy remotesigned <scriptlocation>\xtu_scheduler.ps1 <settingslocation>
#
#  <scriptlocation>: where did you put this script?
#  <settingslocation>: put it on your location of choice, ex) "c:\my_awesome_location"
#
#  (Run whether user is logged on or not is VERY UNRELIABLE)


#this script requires intel xtucli.exe!!!
#read cpu clock.txt and ready up everything prior to configuring this script

#config files for adding special_programs, programs_running_cfg_cpu, programs_running_cfg_xtu
#				YOU CAN EDIT CONFIG REALTIME!!!: its in <settingslocation>\xtu_scheduler_config\



#	INITIALIZERS

# for .NET messagebox
Add-Type -AssemblyName PresentationCore, PresentationFramework

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

# your program = index
$global:special_programs = @{}

# find your own handmade powerplans here:
#  HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes
# index = cpu setting
$global:programs_running_cfg_cpu = @{}

# index = gpu setting
$global:programs_running_cfg_xtu = @{}

# nice settings
$global:programs_running_cfg_nice = @{}

$global:loop_delay = @{}		#seconds
$global:boost_cycle_delay = @{}	#minimum cycle delay before reset:
								#	boost_cycle_delay * loop_delay = minimum seconds before reset
								#
								#	longer delay => less throttling
								#	shorter delay => more battery life

$global:ac_offset = @{}			#ac + ac_offset = dc
								# lowers clockspeed to compensate charging heat


#logging stuff
if((Test-Path ($loc + "xtu_scheduler_config\xtu_scheduler.log")) -eq $True){
	remove-item ($loc + "xtu_scheduler_config\xtu_scheduler.log")
}
function msg ([string]$setting_string){
	if((Test-Path ($loc + "xtu_scheduler_config")) -ne $True) {
		New-Item -path $loc -name "xtu_scheduler_config" -ItemType "directory"
	}

	#print by date and time
	$setting_string = ((get-date -format "yy-MM-dd hh:mm:ss: ") + $setting_string)
	$setting_string
	$setting_string >> ($loc + "xtu_scheduler_config\xtu_scheduler.log")
}

# https://docs.microsoft.com/en-us/dotnet/api/system.windows.messagebox?redirectedfrom=MSDN&view=netframework-4.8
function msgbox ([string]$setting_string){
	msg($setting_string)
	$MessageIcon = [System.Windows.MessageBoxImage]::Warning
	$ButtonType = [System.Windows.MessageBoxButton]::OK
	[System.Windows.MessageBox]::Show($setting_string, "XTU scheduler", $ButtonType, $MessageIcon)

}

# create config files if not exist
function checkFiles ([string]$setting_string, [string]$value_string){
	if((Test-Path ($loc + "xtu_scheduler_config\" + $setting_string + ".txt")) -ne $True){
		if((Test-Path ($loc + "xtu_scheduler_config")) -ne $True) {
			New-Item -path $loc -name "xtu_scheduler_config" -ItemType "directory"
			#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
			msg("created directory: " + $loc + "xtu_scheduler_config")
		}
		
		New-Item -path ($loc + "xtu_scheduler_config") -name ($setting_string + ".txt"`
		) -ItemType "file" -value $value_string
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg("created file: " + $setting_string + " in " + $loc + "xtu_scheduler_config")
	}
}


#reference inside config area below vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv

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



# settings file created by default: (0 will be the base clockspeed! key start from 0 and increment by 1)
function checkFiles_myfiles{
	checkFiles "programs_running_cfg_cpu"`
"0 = 65
1 = 98
2 = 100
3 = 65
4 = 95
5 = 100
6 = 65
7 = 100"

	checkFiles "programs_running_cfg_xtu"`
"0 = 7.5
1 = 5.5
2 = 4.5
3 = 7.5
4 = 6.5
5 = 4.5
6 = 7.5
7 = 7.5"

	# adjust priority
	# idle, belownormal, normal, abovenormal, high, realtime
	checkFiles "programs_running_cfg_nice"`
"0 = idle
1 = high
2 = high
3 = high
4 = high
5 = idle
6 = realtime
7 = high"

	checkFiles "special_programs"`
"'jdownloader2' = 0
'github' = 0
'steam' = 0
'origin' = 0
'mbam' = 0
'shellexperiencehost' = 0
'svchost' = 0
'subprocess' = 0
'gtavlauncher' = 0
'acad' = 1
'launcher' = 7
'tesv' = 1
'fsx' = 1
'Journey' = 1
'nullDC' = 1
'pcsxr' = 1
'ppsspp' = 1
'Project64' = 1
'ace7game' = 1
'pcars' = 1
'doom' = 1
'gtaiv' = 4
'nfs' = 1
'dirt' = 1
'grid' = 1
'studio64' = 2
'arcade64' = 2
'djmax' = 2
'streaming_client' = 2
'moonlight' = 2
'pcsx2' = 2
'dolphin' = 2
'vmware-vmx' = 2
'virtualbox' = 2
'dosbox' = 2
'cemu' = 2
'citra' = 2
'rpcs3' = 7
'drt' = 3
'dirtrally2' = 3
'tombraider' = 3
'rottr' = 3
'bf' = 4
'gta5' = 4
'borderlands2' = 4
'katamari' = 4
'bold' = 4
'setup' = 5
'minecraft' = 5
'cl' = 5
'link' = 5
'ffmpeg' = 5
'7z' = 5
'vegas' = 5
'bandizip' = 5
'handbrake' = 5
'mpc-hc' = 5
'consoleapplication' = 5
'macsfancontrol' = 6
'lubbosfancontrol' = 6
'bootcamp' = 6
'obs' = 6
'remoteplay' = 6
'discord' = 6"

	checkFiles "loop_delay" "loop_delay = 5"
	checkFiles "boost_cycle_delay" "boost_cycle_delay = 6"
	checkFiles "ac_offset" "ac_offset = 1"
	
}

#Config Area Here^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^



# stuff
$guid0 = '381b4222-f694-41f0-9685-ff5bb260df2e'		# you can change to any powerplan you want as default!
$guid1 = '54533251-82be-4824-96c1-47b60b740d00'		# processor power management
$guid2 = 'bc5038f7-23e0-4960-96da-33abaf5935ec'		# processor high clockspeed limit
$guid3 = '893dee8e-2bef-41e0-89c6-b55d0929964c'		# processor low clockspeed limit

$guid4 = '44f3beca-a7c0-460e-9df2-bb8b99e0cba6'		# intel graphics power management
$guid5 = '3619c3f2-afb2-4afc-b0e9-e7fef372de36'		# submenu of intel graphics power management
#intel graphics settings
#	1 = Balanced(Maximum Battery Life is useless)
#	2 = Maximum Performance(seems to remove long term throttling...)

#loop dat shit
foreach($temp in $processor_power_management_guids.Keys){
	powercfg /attributes $guid1 $temp -ATTRIB_HIDE
	powercfg /setdcvalueindex $guid0 $guid1 $temp $processor_power_management_guids[$temp]
	powercfg /setacvalueindex $guid0 $guid1 $temp $processor_power_management_guids[$temp]
}
powercfg /attributes $guid4 $guid5 -ATTRIB_HIDE
# apply settings
powercfg /setactive $guid0

checkFiles_myfiles

# used for checking whether settings file was modified
$global:lastModifiedDate = @{}
$global:found_hash = @{}		#copy $found_hash after calling findFiles
$global:isDateDifferent = $False	#flag for findFiles
$global:reapplySettings = $False	#flag for reapplySettings

# find settings file
function findFiles ($setting_string){
	$file = Get-Content ($loc + "xtu_scheduler_config\" + $setting_string + ".txt")
	$global:lastModifiedDate.add($setting_string, (Get-Item ($loc + "xtu_scheduler_config\"`
	+ $setting_string + ".txt")).LastWriteTime)
	if ($? -eq $True)
	{
		$global:found_hash = @{}
		foreach ($line in $file)
		{
			$global:found_hash.add($line.split("=")[0].trim("'", " "),`
			$line.split("=")[1].trim("'", " "))
		}
		# equivalent to 'eval'
		set-variable ("global:" + $setting_string) $global:found_hash
	}
}

function printSettings (){
	#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
	msg("special_programs count: " + $special_programs.Count)
	msg("programs_running_cfg count: " + $programs_running_cfg_cpu.Count)
	msg("loop_delay: " + $loop_delay.loop_delay + ", boost_cycle_delay: " + $boost_cycle_delay.boost_cycle_delay)
	msg("general pace: " + ([int]$boost_cycle_delay.boost_cycle_delay * [int]$loop_delay.loop_delay) + "second(s)")

}

function checkSettings ($setting_string){
	$currentModifiedDate = (Get-Item ($loc + "xtu_scheduler_config\" + $setting_string + ".txt"`
	)).LastWriteTime
	if($global:lastModifiedDate[$setting_string] -ne $currentModifiedDate){
		$global:isDateDifferent = $True
		$global:lastModifiedDate.Remove($setting_string)
		
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg($setting_string + " has been modified, reloading...")
		$global:reapplySettings = $True
		findFiles $setting_string
		
		printSettings
	}
	else{
		$global:isDateDifferent = $False
	}
}

findFiles "programs_running_cfg_cpu"
findFiles "programs_running_cfg_xtu"
findFiles "programs_running_cfg_nice"
findFiles "special_programs"
findFiles "loop_delay"
findFiles "boost_cycle_delay"
findFiles "ac_offset"

#initial xtu check
if((get-command xtucli -errorAction SilentlyContinue) -eq $null){
	#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
	msgbox("XTUCLI CMDLET DOES NOT EXIST!! this program will not operate without Intel(r) Extreme Tuning Utility.")
	exit
}

# initial cpu setting
$cpu_init = $programs_running_cfg_cpu['0']
$cpu_max = 100

# initial gpu setting(make sure nothing is running on boot that uses xtu besides this script,
# and you should disable all xtu profiles as well)
$xtu_init = $programs_running_cfg_xtu['0']
$xtu_max = ((& xtucli -t -id 59 | select-string "59" | %{ -split $_ | select -index 5} | out-string
) -replace "x",'').trim()


#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
msg(" **** xtu_scheduler initial information ****")
msg("using settings file located in: " + $loc + "xtu_scheduler_config\")
msg("cpu_init: " + $cpu_init + ", cpu_max: " + $cpu_max)
msg("xtu_init: " + $xtu_init + ", xtu_max: " + $xtu_max)
if([float]$xtu_init -gt [float] $xtu_max){
	msgbox("xtu settings may have been altered too low, check your gpu settings and restart this program.")
	msg("xtu settings may not be applied properly, or at all.")
}
printSettings


function xtuproc($arg0){
	if ([float]$arg0 -le [float]$xtu_max)
	{
		$xtuproc = start-process xtucli ("-t -id 59 -v " + $arg0) -PassThru
		$xtuproc.PriorityClass = "idle"
	}
}

function cpuproc($arg0, $arg1){
	powercfg /setdcvalueindex $guid0 $guid1 $guid2 $arg0
	powercfg /setacvalueindex $guid0 $guid1 $guid2 ($arg0 - $ac_offset.ac_offset)		# hotter when plugged in
	
	#intel graphics settings
	#	1 = Balanced
	#	2 = Maximum Performance
	# intel graphics
	powercfg /setdcvalueindex $guid0 $guid4 $guid5 $arg1
	powercfg /setacvalueindex $guid0 $guid4 $guid5 $arg1
	# apply settings
	powercfg /setactive $guid0
}


# initial powerplan to whatever guid0 is
cpuproc $cpu_init 1
xtuproc $xtu_init
#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
msg("initial settings applied - cpu_init: " + $cpu_init + ", xtu_init: " + $xtu_init)


# initial cpu max speed
function checkMaxSpeed(){

	$cpu = Get-WmiObject -class Win32_Processor
	$global:max = $cpu['CurrentClockSpeed']
}

checkMaxSpeed

# throttle switch
$global:sw1 = 0
# throttle offset shift
$global:th_offset = -1
# throttle cycle delay
$global:th_cycle = 0

# minimum cycle delay before reset
$global:sw2 = 0		#0 = off, 1 = wait, 2 = countdown
$global:cycle = 0
# wait on first detection
$global:cycle2 = 0
$global:cycle2_copy = 0

# *legit* special_programs switch
$global:sw3 = 0

# loop delay backup
$loop_delay_backup = $loop_delay.loop_delay

while ($True)
{
	checkFiles_myfiles
	checkSettings "programs_running_cfg_cpu"
	checkSettings "programs_running_cfg_xtu"
	checkSettings "programs_running_cfg_nice"
	checkSettings "special_programs"
	checkSettings "loop_delay"
	checkSettings "boost_cycle_delay"
	checkSettings "ac_offset"
	#	init may have been changed
	$cpu_init = $programs_running_cfg_cpu['0']
	$xtu_init = $programs_running_cfg_xtu['0']
	
	
	#check if theres enough profile
	if($special_programs.Count -le 2){
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msgbox("not enough profiles on 'special_programs'. at least 3 is required")
		start-sleep $loop_delay.loop_delay
		continue
	}
	
	$special_programs_running = $False
	# there may be multiple target apps open. make a list of keys that fit the desc
	$xkey = @{}
	
	foreach($key in $special_programs.Keys)		#   $key value remains globally after break
	{
		$temp = Get-Process -ErrorAction SilentlyContinue -Name ($key + '*')
		if ($temp -ne $null)
		{
			$special_programs_running = $True
			
			#boost priority!!
			foreach($boost in $temp){
				try{
				$boost.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::`
				[string]$programs_running_cfg_nice[$special_programs[$key]]
				}
				catch{}
			}
			
			$xkey.add($key, 0)
		}
		start-sleep -m 20
	}
	
	#idle, belownormal, realtime priority will be regarded as idle processes,
	#and disregarded powerop when other main processes are also running!
	$maxcpukey = ""
	$maxcpuratio = 0
	
	foreach($key in $xkey.Keys){
		#if normal, above normal, high... get key and break loop
		if([string]$programs_running_cfg_nice[$special_programs[$key]] -ne "idle" -and`
		[string]$programs_running_cfg_nice[$special_programs[$key]] -ne "belownormal" -and`
		[string]$programs_running_cfg_nice[$special_programs[$key]] -ne "realtime"){
			$maxcpukey = $key
			break
		}
		
		#if idle, belownormal, realtime.. get the key with the highest cpu ratio
		else{
			if([int]$programs_running_cfg_cpu[$special_programs[$key]] -gt [int]$maxcpuratio){
				$maxcpukey = $key
				$maxcpuratio = $programs_running_cfg_cpu[$special_programs[$key]]
			}
		
		}
		
	}
	$key = $maxcpukey
	
	
	#temp = name of the process were looking for
	#temp2 = value of 'programs_running_cfg_cpu'
	
	$temp2 = powercfg /query $guid0 $guid1 $guid2
	$temp2 = Out-String -InputObject $temp2
	$temp2 = $temp2.SubString($temp2.Length - 6, 6).trim()
	$temp2 = '{0:d}' -f [int]("0x" + $temp2)
	
	#check cpu load, clock
	$cpu = Get-WmiObject -class Win32_Processor
	$load = $cpu['LoadPercentage']
	$clock = $cpu['CurrentClockSpeed']
	
	
	#if short boost no longer needed while waiting...
	if($global:cycle2 -ne $global:cycle2_copy){
		$global:cycle2 = 0
		$global:cycle2_copy = $global:cycle2
	}
	
	#throttling counter starts only when throttling starts
	if($global:sw1 -ne 1){
		$global:th_cycle = 0
	}
	
	#reapplySettings: always reapply when settings changed
	if($global:reapplySettings -eq $True){
		#if throttling commenced in init settings...
		if($cpu_init -match $programs_running_cfg_cpu[$special_programs[$key]] -eq $True -And`
		$global:sw2 -eq 0){
			cpuproc $programs_running_cfg_cpu[$special_programs[$key]] 1
		}
		else{
			cpuproc $programs_running_cfg_cpu[$special_programs[$key]] 2
		}
		xtuproc $programs_running_cfg_xtu[$special_programs[$key]]
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg("settings reapplied - cpu_init: " + $programs_running_cfg_cpu[$special_programs[$key]] + ", xtu_init: "`
		+ $programs_running_cfg_xtu[$special_programs[$key]])
		$global:reapplySettings = $False
	}
	
	#if throttling has kicked in, shift to another profile for a brief time
	elseif($load -gt $processor_power_management_guids['06cadf0e-64ed-448a-8927-ce7bf90eb35d'] -And`
	[int]$global:th_cycle++ % [int]$boost_cycle_delay.boost_cycle_delay -eq 0 -And`	# delay every throttle
	[int]$clock -lt [int]$global:max){		#2700mhz != 2701mhz, might be a turboboost clock
	
		# keep shifting to another profile
		#if init
		if($global:th_offset -eq -1){
			$global:th_offset = [int]$special_programs[$key]
			
		}
		#right
		$th_offset_temp_r = [int]([int]$global:th_offset + [int]1)
		if([int]$th_offset_temp_r -lt 0){		#if negative, add another count
			$th_offset_temp_r = $th_offset_temp_r % $programs_running_cfg_cpu.Count + $programs_running_cfg_cpu.Count
		}
		else{
			$th_offset_temp_r = $th_offset_temp_r % $programs_running_cfg_cpu.Count
		}
		#left
		$th_offset_temp_l = [int]([int]$global:th_offset - [int]1)
		if([int]$th_offset_temp_l -lt 0){		#if negative, add another count
			$th_offset_temp_l = $th_offset_temp_l % $programs_running_cfg_cpu.Count + $programs_running_cfg_cpu.Count
		}
		else{
			$th_offset_temp_l = $th_offset_temp_l % $programs_running_cfg_cpu.Count
		}
		
		#check which sides bigger
		if([int]$programs_running_cfg_cpu[[string]$th_offset_temp_l] -gt`
		[int]$programs_running_cfg_cpu[[string]$th_offset_temp_r]){
			$global:th_offset = $th_offset_temp_l
		}
		else{
			$global:th_offset = $th_offset_temp_r
		}
		
		cpuproc $programs_running_cfg_cpu[[string]$global:th_offset] 2
		xtuproc $programs_running_cfg_xtu[[string]$global:th_offset]
		
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg("throttling detected, cpuload: " + $load + ", currentspeed: " + $clock + ", maxspeed: " + $global:max`
		+ ", profile switch to: " + $global:th_offset)
		
		$global:sw1 = 1
		checkMaxSpeed		# check max speed here
	}
	
	# reset offset after throttling stopped
	elseif($load -le $processor_power_management_guids['06cadf0e-64ed-448a-8927-ce7bf90eb35d'] -And`
	[int]$clock -ge [int]$global:max -And`
	$global:sw1 -eq 1 -And`
	$global:th_offset -ne -1){
	
		#print information<<<<<<<<<
		msg("throttling is clear.")
		
		#if throttling commenced in init settings...
		if($cpu_init -match $programs_running_cfg_cpu[$special_programs[$key]] -eq $True -And`
		$global:sw2 -eq 0){
			cpuproc $programs_running_cfg_cpu[$special_programs[$key]] 1
		}
		else{
			cpuproc $programs_running_cfg_cpu[$special_programs[$key]] 2
		}
		
		xtuproc $programs_running_cfg_xtu[$special_programs[$key]]
		
		$global:th_offset = -1
		$global:th_cycle = 0		#related to pace
		$global:sw1 = 0
		checkMaxSpeed		# check max speed here
	}
	
	#if init settings as default...
	#there may be multiple init entries with different priority settings.
	elseif($cpu_init -match $programs_running_cfg_cpu[$special_programs[$key]] -eq $True -And`
	$global:sw1 -eq 0){
	
		#copied from throttling code
		#
		#might be unconfigured game. apply Maximum Performance on graphics settings for a brief time
		# upper bound is 80
		if($load -gt $processor_power_management_guids['12a0ab44-fe28-4fa9-b3bd-4b64f44960a6'] -And`
		[int]$clock -eq [int]$global:max -And`		#one more check to ensure when to boost
		$global:sw2 -eq 0 -And`
		$global:sw3 -eq 0){		# *legit* special_programs lock
		
			# wait on first detection
			#first encounter, start waiting
			if($global:cycle2 -eq 0){
				$global:cycle2 = [int]$boost_cycle_delay.boost_cycle_delay + 1
				#copy cycle2, minus 1 because of decrement after loop
				$global:cycle2_copy = [int]$global:cycle2 - 1
			}
			
			# 0 is off, 1 is final
			elseif($global:cycle2 -eq 1){
			
				#reset
				$global:cycle2 = 0
				$global:cycle2_copy = $global:cycle2
				
				#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
				msg("lightgme: setting graphics to max perf, possibly a light game...?")
				
				cpuproc $cpu_init 2
				$global:sw2 = 1
				checkMaxSpeed		# check max speed here
			}
			else{
				#copy cycle2, minus 1 because of decrement after loop
				$global:cycle2_copy = [int]$global:cycle2 - 1
			}
		}
		
		#if lowerbound while not yet count, start counting
		elseif ($load -le $processor_power_management_guids['12a0ab44-fe28-4fa9-b3bd-4b64f44960a6'] -And`
		$global:sw2 -eq 1){
			$global:sw2 = 2
			$global:cycle = [int]$boost_cycle_delay.boost_cycle_delay
		
		}
		#if upperbound while counting... restart counting
		elseif ($load -gt $processor_power_management_guids['12a0ab44-fe28-4fa9-b3bd-4b64f44960a6'] -And`
		$global:sw2 -eq 2){
			$global:sw2 = 1
			
		}
		
		#reset after boost
		# lower bound is 50
		elseif ($load -le $processor_power_management_guids['12a0ab44-fe28-4fa9-b3bd-4b64f44960a6'] -And`
		$global:sw2 -eq 2){
			if($global:cycle -eq 0){
				#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
				msg("lightgme: no known applications running atm... back to init")
			
				cpuproc $cpu_init 1
				$global:sw2 = 0
				checkMaxSpeed		# check max speed here
			}
			#start counting when lowerbound
			else{
				$global:cycle--
			
			}
		}
		
		
		elseif ($temp2 -match $programs_running_cfg_cpu[$special_programs[$key]] -eq $False -And`
			$global:sw2 -eq 0){		#if boost is waiting, this must not happen.
			
			#if special program is running...
			if ($special_programs_running -eq $True)
			{
				#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
				msg("fakeinit: no known applications running atm... back to init")
				
				$global:sw3 = 0		# *legit* special_programs not running
			}
			else{
				#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
				msg("external: no known applications running atm... back to init")
			}
			
			cpuproc $cpu_init 1
			xtuproc $xtu_init
			
			$loop_delay.loop_delay = $loop_delay_backup
			checkMaxSpeed		# check max speed here
		}
	
	}
	
	#if its not init settings...
	elseif ($temp2 -match $programs_running_cfg_cpu[$special_programs[$key]] -eq $False -And`
	$global:sw1 -eq 0){
	
		#disable short boost triggers
		if($global:sw2 -eq 1){
			#print information<<<<<<<<<<<<<<<<
			msg("lightgme: wait interrupted.")
			$global:sw2 = 0
			$global:cycle = 0
			$global:cycle2 = 0
			$global:cycle2_copy = $global:cycle2
		}
		
		#print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg("current powersettings followed by: " + $key + ", setcpuspeed: "`
		+ $programs_running_cfg_cpu[$special_programs[$key]] + ", setxtuspeed: "`
		+ $programs_running_cfg_xtu[$special_programs[$key]])
		
		cpuproc $programs_running_cfg_cpu[$special_programs[$key]] 2
		xtuproc $programs_running_cfg_xtu[$special_programs[$key]]

		$global:sw3 = 1		# *legit* special_programs running
		
		$loop_delay.loop_delay = $loop_delay_backup
		checkMaxSpeed		# check max speed here
	}
	
	# wait on first detection
	#decrement after loop
	if($global:cycle2 -ne 0){
		$global:cycle2--
	
	}
	
	start-sleep $loop_delay.loop_delay
}
