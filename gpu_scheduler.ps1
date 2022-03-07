#user config
$totaloffset_up = 20
$totaloffset_down = 10
$limit = 50
$sleeptime = 5

#gpu config
$clockoffset = -950
$memoffset = -1000

#internal stuff
$total = 0
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
	$delta = $(Get-WmiObject -class Win32_Processor)['LoadPercentage']
	if ($total -le $delta -And $total -le 100){
		$total += $totaloffset_up
	}elseif($total -gt $delta -And $total -gt 0){
		$total -= $totaloffset_down
	}
	msg("total: " + $total + ", delta: " + $delta)
	if ($total -gt $limit -Or $delta -gt $limit){
		if ($switching -eq 0){
			C:\nvidiaInspector\nvidiaInspector.exe -setBaseClockOffset:0,0,$clockoffset -setMemoryClockOffset:0,0,$memoffset
			$switching = 1
			msg("gpu is in game mode")
		}
	}elseif($total -le $limit -And $delta -le $limit){
		if($switching -eq 1){
			C:\nvidiaInspector\nvidiaInspector.exe -setBaseClockOffset:0,0,0 -setMemoryClockOffset:0,0,0
			$switching = 0
			msg("gpu is sleeping")
		}
	}
	start-sleep $sleeptime
}