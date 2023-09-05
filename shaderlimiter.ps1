#
# shader file size limiter for intel gpus(shaderlimiter.ps1) - by hoholee12
#
# run it via task scheduler
#  PowerShell.exe -windowstyle hidden -executionpolicy remotesigned <scriptlocation>\shaderlimiter.ps1 <scriptlocation>
#
# - intel gpu driver doesnt divide shaders into separate files, its just one file for one application
# - this results in vulkan programs crashing if its too big(over 300MB<)
# - this script attempts to limit growth of shader cache files in the background

$limit = 471859200		# around 450MB
$sleeptime = 5


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

# logging stuff

function msg([string]$setting_string){
	# print by date and time
	$setting_string = ((get-date -format "yy-MM-dd hh:mm:ss: ") + $setting_string)
	$setting_string >> ($loc + "shaderlimiter.log")
}
msg("script started. starting location: " + $loc + " the shader file size limit is: " + $limit)	# log script location

while($true){
	$sw = [Diagnostics.Stopwatch]::StartNew()
	
	$files = get-childitem "$env:userprofile\AppData\LocalLow\Intel\ShaderCache\" | sort-object length -descending
	for($i = 0; $i -lt $files.count; $i++){
		$filename = $files[$i].fullname
		$origdir = $files[$i].directory.fullname
		$name = $files[$i].name
		$filesize = $files[$i].length
		if($filesize -le $limit){
			try{
				[IO.File]::OpenWrite($filename).close()
				# backup file if its bigger
				if((test-path "$env:userprofile\AppData\LocalLow\Intel\$name") -eq $false){
					copy-item -path $filename -destination "$env:userprofile\AppData\LocalLow\Intel" -force -erroraction continue
					msg("filename: " + $name + " filesize: " + $filesize + " backed up successfully")
				}
				else{
					$backup = get-item "$env:userprofile\AppData\LocalLow\Intel\$name"
					if($backup.length -lt $filesize){
						try{
							copy-item -path $filename -destination "$env:userprofile\AppData\LocalLow\Intel" -force -erroraction continue
							msg("filename: " + $name + " filesize: " + $filesize + " backed up successfully")
						}
						catch{
						}
					}
				}
			}
			catch{
			}
		}
		else{
			try{
				[IO.File]::OpenWrite($filename).close()
				# restore file if its bigger
				$deletethis = $false
				if((test-path "$env:userprofile\AppData\LocalLow\Intel\$name") -eq $false){
					remove-item $filename
					msg("filename: " + $name + " filesize: " + $backup.length + " too big, deleted")
				}
				else{
					$backup = get-item "$env:userprofile\AppData\LocalLow\Intel\$name"
					if($backup.length -lt $filesize){
						try{
							copy-item -path $backup.fullname -destination $origdir -force -erroraction continue
							msg("filename: " + $name + " filesize: " + $backup.length + " restored successfully")
						}
						catch{
						}
					}
				}
			}
			catch{
			}
		}
	}
	$sw.Stop()
	
	start-sleep ($sleeptime - $sw.Elapsed.Seconds)
}