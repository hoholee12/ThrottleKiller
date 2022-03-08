#user config
$limit = 1
$sleeptime = 5

#gpu config
$clockoffset = -950
$memoffset = -1000

#internal stuff
$delta = 0
$switching = 1
#logging stuff
function msg ([string]$setting_string){
	#print by date and time
	$setting_string = ((get-date -format "yy-MM-dd hh:mm:ss: ") + $setting_string)
	$setting_string
	$setting_string >> "C:\nvidiaInspector\gpu_scheduler.log"
}


while($true){
	$delta = (((Get-Counter "\GPU Engine(*engtype_Copy)\Utilization Percentage").CounterSamples | where CookedValue).CookedValue | measure -sum).sum
	msg("delta: " + $delta)
	if ($delta -ge $limit){
		if ($switching -eq 0){
			C:\nvidiaInspector\nvidiaInspector.exe -setBaseClockOffset:0,0,$clockoffset -setMemoryClockOffset:0,0,$memoffset
			$switching = 1
			msg("gpu is in game mode")
		}
	}elseif($delta -lt $limit){
		if($switching -eq 1){
			C:\nvidiaInspector\nvidiaInspector.exe -setBaseClockOffset:0,0,0 -setMemoryClockOffset:0,0,$memoffset
			$switching = 0
			msg("gpu is sleeping")
		}
	}
	start-sleep $sleeptime
}