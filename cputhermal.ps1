#load dll
[system.reflection.assembly]::loadfile("" + (get-location) + "\openhardwaremonitorlib.dll")

while($true){
	$ohm = new-object openhardwaremonitor.hardware.computer
	$ohm.cpuenabled = $true
	$ohm.open()

	#$ohm.hardware.sensors.sensortype
	$ohm.hardware.sensors.value
	
	start-sleep 5
}