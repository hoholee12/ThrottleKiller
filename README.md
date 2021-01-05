# Auto_PowerManager
my final year school project

## Disclaimer

for xtucli, any versions above 6.1.2.11 seems to have been deprecated.


you can get the old version here:

https://files03.tchspt.com/tempd/XTU-Setup-6.1.2.11.exe

## HOW TO USE?
ConsoleApplication41 is the Core program, WindowsFormsApplication5 is the GUI program.

task scheduling the core program, and putting GUI program in the startmenu folder is recommended.

make sure you only have a intel(R) integrated GPU and have intel(R) extreme tuning utility installed on your system. (otherwise there is no point in using this software)

core program will run by itself without any support. you can use GUI program to control the core program, and get status information.

be my guest!

## screenshots
![alt text](https://github.com/hoholee12/Auto_PowerManager/blob/master/asdf.png?raw=true)

## abstract
```
ABSTRACT

	People always seek for better performance at an affordable price. Instead 
  of purchasing better but expensive parts, most people pursue other affordable
  options such as overclocking, space organization, and optimization to further
  increase the performance of the existing hardware. Among these options, 
  overclocking is easily doable on a desktop PC, but most laptops equipped
  with mid-to-low-end Intel GPUs do not enjoy such privileges, due to heat 
  dissipation constraint. Depending on the temperature and power usage, throttling 
  rarely happens on desktop PCs with discrete GPU and a far better cooling solution,
  but it is common for systems with limited cooling solutions with integrated Intel GPU.

	This paper aims to provide a mechanism that will make systems with such issues less
  prone to throttling, through research of algorithms for throttle detection and control,
  and a software development plan based on this research. Although the throttling system
  of laptops has improved a lot recently, this paper aims to provide a software solution
  for older generations that still do not. When implemented as a user software, uniform
  information can be extracted through the operating system and its prevalent drivers, 
  and appropriate tuning can be performed. It may also have the advantage of being portable
  to many systems.
	Based on above, we create a core software that monitors the clock rate and temperature
  in real time, processes the monitored data based on a series of algorithms, and applies
  the appropriate clock rate for each programs. As a result of testing, heavy programs
  such as game applications had much less chance of significant stutter and choppy 
  framerate due to throttling.

keyword: throttling
```
