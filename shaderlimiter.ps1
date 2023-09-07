#
# shader file size limiter for intel gpus(shaderlimiter.ps1) - by hoholee12
#
# run it via task scheduler
#  PowerShell.exe -windowstyle hidden -executionpolicy remotesigned <scriptlocation>\shaderlimiter.ps1 <scriptlocation>
#
# - intel gpu driver doesnt divide shaders into separate files, its just one file for one application
# - this results in vulkan programs crashing if its too big(over 300MB<)
# - this script attempts to limit growth of shader cache files in the background

$limit = 440401920		# around 420MB
$sleeptime = 10


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

$repeatflag = 0
$backupswitch = 0

while($true){
	$sw = [Diagnostics.Stopwatch]::StartNew()
	(get-childitem "$env:userprofile\AppData\LocalLow\Intel\ShaderCache\").refresh() # update directory meta
	$files = get-childitem "$env:userprofile\AppData\LocalLow\Intel\ShaderCache\" | sort-object length -descending
	for($i = 0; $i -lt $files.count; $i++){
		$filename = $files[$i].fullname
		$origdir = $files[$i].directory.fullname
		$name = $files[$i].name
		$filesize = $files[$i].length
		
		# backup file if its bigger
		if($filesize -le $limit){
			try{
				# some or no file exists(try to backup two files)
				if((test-path "$env:userprofile\AppData\LocalLow\Intel\$name.0") -eq $false`
				-Or (test-path "$env:userprofile\AppData\LocalLow\Intel\$name.1") -eq $false){
					copy-item -path $filename -destination "$env:userprofile\AppData\LocalLow\Intel\$name.0" -force -erroraction continue
					copy-item -path $filename -destination "$env:userprofile\AppData\LocalLow\Intel\$name.1" -force -erroraction continue
					msg("filename: " + $name + " filesize: " + $filesize + " backed up successfully")
				}
				# all files exist(try to backup every odd file)
				else{
					$baklength0 = (get-item "$env:userprofile\AppData\LocalLow\Intel\$name.0").length
					$baklength1 = (get-item "$env:userprofile\AppData\LocalLow\Intel\$name.1").length
					$baklength = $baklength0
					if($baklength0 -lt $baklength1){
						$baklength = $baklength1
					}
					# backup file if prev backup is smol
					if($baklength -lt $filesize){
						try{
							# back up one file each time
							if($baklength0 -lt $baklength1){
								copy-item -path $filename -destination "$env:userprofile\AppData\LocalLow\Intel\$name.0" -force -erroraction continue
							}
							else{
								copy-item -path $filename -destination "$env:userprofile\AppData\LocalLow\Intel\$name.1" -force -erroraction continue
							}
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
				$baklength0 = 0
				$baklength1 = 0
				$myfail = 0
				if((test-path "$env:userprofile\AppData\LocalLow\Intel\$name.0") -eq $true){
					$baklength0 = (get-item "$env:userprofile\AppData\LocalLow\Intel\$name.0").length
				}
				else{
					msg("filename: " + "$name.0" + " doesnt exist")
					$myfail++
				}
				
				if((test-path "$env:userprofile\AppData\LocalLow\Intel\$name.1") -eq $true){
					$baklength1 = (get-item "$env:userprofile\AppData\LocalLow\Intel\$name.1").length
				}
				else{
					msg("filename: " + "$name.1" + " doesnt exist")
					$myfail++
				}
				
				if($myfail -eq 2){
					msg("filename: " + $name + " no previous backups exist. delete file manually to start limiting")
				}
				else{
					
					[IO.File]::OpenWrite($filename).close()
					# restore file if its bigger

					if($baklength0 -gt $baklength1){
						$backupswitch = 0
					}
					else{
						$backupswitch = 1
					}
					$backup = get-item "$env:userprofile\AppData\LocalLow\Intel\$name.$backupswitch"
					if($backup.length -lt $filesize){
						try{
							copy-item -path $backup.fullname -destination "$origdir\$name" -force -erroraction continue
							msg("filename: " + "$name.$backupswitch" + " filesize: " + $backup.length + " restored successfully")
							$repeatflag = 0
						}
						catch{
						}
					}
				}
			}
			catch{
				if($repeatflag -eq 0){
					msg("filename: " + $name + " is waiting to be restored")
					$repeatflag = 1
				}
			}
		}
	}
	$sw.Stop()
	
	start-sleep ($sleeptime - $sw.Elapsed.Seconds)
}