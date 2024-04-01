#
# shader file size limiter for intel gpus(shaderlimiter.ps1) - by hoholee12
#
# run it via task scheduler
#  PowerShell.exe -windowstyle hidden -executionpolicy remotesigned <scriptlocation>\shaderlimiter.ps1 <scriptlocation>
#
# - intel gpu driver doesnt divide shaders into separate files, its just one file for one application
# - this results in vulkan programs crashing if its too big(over 300MB<)
# - this script attempts to limit growth of shader cache files in the background




# TODO: new plan
#		if program is unfocused
#			delete the shader(that already has info in backup)
#		if program is focused
#			check foreground app name
#			if the info exists
#				copy the file back immediately
#			else
#				to check which shader: make list of all shaders and their sizes.
#				check the size difference every cycle.
#				check growing shader file(timestamp?) and save the filename/appname to ini

$sleeptime = 10
$loopbeforeupdate = 2

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
	write-output($setting_string)
}

# create config files if not exist
function checkFiles([string]$setting_string, [string]$value_string){
	if((Test-Path ($loc + "shaderlimiter_cfg\" + $setting_string + ".txt")) -ne $True){
		if((Test-Path ($loc + "shaderlimiter_cfg")) -ne $True) {
			New-Item -path $loc -name "shaderlimiter_cfg" -ItemType "directory"
			# print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
			msg("created directory: " + $loc + "shaderlimiter_cfg")
		}
		
		New-Item -path ($loc + "shaderlimiter_cfg") -name ($setting_string + ".txt"`
		) -ItemType "file" -value $value_string
		# print information<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
		msg("created file: " + $setting_string + " in " + $loc + "shaderlimiter_cfg")
	}
}

#<procname> <shaderfilename>
function checkFiles_myfiles{
	checkFiles "shaderprog_list"`
"test 0000000000000
"
}

function checkSettings($setting_string){
	$currentModifiedDate = (Get-Item ($loc + "shaderlimiter_cfg\" + $setting_string + ".txt"`
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
	$file = Get-Content ($loc + "shaderlimiter_cfg\" + $setting_string + ".txt")
	$global:lastModifiedDate.add($setting_string, (Get-Item ($loc + "shaderlimiter_cfg\"`
	+ $setting_string + ".txt")).LastWriteTime)
	if ($? -eq $True)
	{
		$global:found_hash = @{}
		foreach ($line in $file)
		{
			$sp = $line.split()
			$global:found_hash.add($sp[0], $sp[1])
		}
		# equivalent to 'eval'
		set-variable ("global:" + $setting_string) $global:found_hash
	}
}

findFiles "shaderprog_list"

# for foreground detection
Add-Type @"
  using System;
  using System.Runtime.InteropServices;
  public class Foreground {
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}
"@

# always changes process_str for foreground process
# returns true if process is not one of the following: explorer, mmc, applicationframehost, nor its an error.
$global:process_str = ""
function is_foreground_proc{
	try{
		$error.clear()
		$fg = [Foreground]::GetForegroundWindow()
		$ret = get-process | ? { $_.mainwindowhandle -eq $fg }
		$global:process_str = $ret.processname.ToLower()
		if($error -Or ($global:process_str.Length -gt 20) -Or ($global:process_str -eq "explorer") -Or`
		($global:process_str -eq "mmc") -Or ($global:process_str -eq "applicationframehost")){
			$global:process_str = ""
			$global:result = $false
			return $false
		}
		return $true
		#foreach($key in $shaderprog_list.Keys)		#   $key value remains globally after break
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

# add to blacklist
$shaderprog_filename = ""
function bmsg{
	# print by date and time
	if($global:process_str.Length -ne 0){
		$mystring = "`n" + $global:process_str + " " + $shaderprog_filename
		$mystring | out-file $loc"\shaderlimiter_cfg\shaderprog_list.txt" -Encoding ASCII -Append -NoNewline
	}
}

# keep track of sizes of all shader files.
$global:shader_length_list = @{}

msg("script started. starting location: " + $loc)	# log script location

$repeatcounter = 0
$destination = "$env:userprofile\AppData\LocalLow\Intel\ShaderCache\"
$backuppath = "$env:userprofile\AppData\LocalLow\Intel\ShaderCacheBackup\"

while($true){
	$sw = [Diagnostics.Stopwatch]::StartNew()
	(get-childitem "$env:userprofile\AppData\LocalLow\Intel\ShaderCache\").refresh() # update directory meta

	checkFiles_myfiles
	checkSettings "shaderprog_list"
	
	# Check if the program is foreground or not
	# TODO: BOTH does_procname_exist and the following if-else block uses shaderprog_list. we need a different way to detect a program.
	if(is_foreground_proc){
		# existing program info
		# copy the shader back immediately
		if($shaderprog_list.ContainsKey($global:process_str)){
			$shaderprog = $shaderprog_list[$global:process_str]
			$backupfiles = get-childitem $backuppath | Where-Object { $_.Name -like $shaderprog }	# there should be 2, if the redundancy is working
			$actualfiletorestore = ""
			$maxsize = 0
			foreach($backupfile in $backupfiles){
				$backupfilename = $backupfile.Name
				$backupfilesize = $backupfile.Length
				if ($backupfilesize -gt $maxsize){
					$maxsize = $backupfilesize
					$actualfiletorestore = $backupfilename
				}
			}
			# shaderprog is the original filename of the shader file. actualfiletorestore is the backup filename with .0 or .1
			Copy-Item -Path ($backuppath + $actualfiletorestore) -Destination ($destination + $shaderprog) -Force -erroraction continue
			msg("Shader file copied back for focused program: " + $global:process_str)
		}
		else{
			# backup shader file doesn't exist(aka first time focused program is detected)
			# Check the size difference every cycle and save the filename/appname to ini(this is the only way to check which shader belongs to the focused program)
			$files = get-childitem $destination | sort-object LastWriteTime -descending
			foreach($file in $files){
				$filename = $file.Name
				$filesize = $file.Length
				if($global:shader_length_list.ContainsKey($filename)){
					$prevSize = $global:shader_length_list[$filename]
					if($filesize -gt $prevSize){
						if($loopbeforeupdate -gt $repeatcounter){
							# not yet reached the limit
							$repeatcounter++
							break
						}
						else{
							# reset counter; we finally found the shaderfilename for this specific program.
							$repeatcounter = 0
							$shaderprog_filename = $filename
							bmsg	# only add to log. we will refresh shaderproglist in next cycle anyway.
							# we backup twice for redundancy
							copy-item -path ($destination + $filename) -destination ($backuppath + $filename + ".0") -force -erroraction continue
							copy-item -path ($destination + $filename) -destination ($backuppath + $filename + ".1") -force -erroraction continue
							msg("filename: " + $filename + " filesize: " + $filesize + " backed up successfully")
							$global:shader_length_list = @{}	# clear the list
							break
						}
					}
				}
				else{
					$global:shader_length_list[$filename] = $filesize	# add to list
				}
			}
		}
	}
	else{
		# nothing is in focus
		# Delete shader files for any remaining shaderprog_list programs from the destination folder
		foreach($procname in $shaderprog_list.Keys){
			if($shaderprog_list.ContainsKey($procname)){
				$shaderprog = $shaderprog_list[$procname]
				$files = Get-ChildItem $destination | Where-Object { $_.Name -eq $shaderprog }
				foreach($file in $files){
					$filename = $file.Name
					$filesize = $file.Length
					Remove-Item -Path ($destination + $filename) -Force -ErrorAction Continue
					msg("Shader file deleted for unfocused program: " + $procname)
				}
			}
		}
	}

	$sw.Stop()
	Start-Sleep ($sleeptime - $sw.Elapsed.Seconds)
}