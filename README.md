# Voron Scripts for Aurora

## Checkout video demonstration
[![Video Demo](https://github.com/VoronFX/Aurora/raw/voron-scripts/Project-Aurora/Scripts/VoronScripts/Resources/VideoPreview.jpg)](https://www.youtube.com/watch?v=6Ub-lh2kmKg)

The goal of this fork is to help me debug my scripts for Aurora and probably make some personal fixes. 
The actual Aurora project you can find here: [Link to Aurora project](https://github.com/antonpup/Aurora)

# UPDATE
* Last update has a big breaking changes. Read about them and download new version here:  [Voron Scripts v1.0-beta.7](https://github.com/VoronFX/Aurora/releases/tag/vscripts-v1.0-beta.7)
* Old info about old scripts now is here: [Voron Scripts v1.0-beta.5](https://github.com/VoronFX/Aurora/tree/voron-scripts-aurora6) 

# About scripts
As you understand I did some scripts for my Logitech G910 Orion Spark.
All scripts were designed for my own use and by my own taste, but if you like them feel free to use and modify them.
You can even ask me to implement something on keyboard, if you can't. And if I will have time I will help you.

##PerfEffect
* Displays a load effect based on percent value. Analogue to Aurora's Percent Effect with lot of options and much more possibilities.
* Value can be taken from any WMI performance counter in system, plus some additional internal ones. E.g. load of CPU, cores of CPU, GPU, GPU memory, Ram, Disk, Network and other stuff.
* Value can be normalized and changed by custom math formula before displaying. 
* Has all original modes AllAtOnce, Progressive, ProgressiveGradual and additional CycledGradientShift.
* Has ability to blink at high values to show overload. 

##PingEffect
* Displays ping with animation of each request.
* Can display only current ping or graph of last pings.
* Each ping animation represents exactly each request that was made. Ping animation is as recent as possible, real ping request even can not yet complete until you start seeing it's animation. Hovewer in graph mode animation start's always after response was recieved.
* Colors and timings are highly customizible.
* Different hosts can be used for active application. And default host for other apps. 
For example, you can set script to show ping to game server while that game is active window.

# Pictures
![Gpu load on the left](https://raw.githubusercontent.com/VoronFX/Aurora/voron-scripts/Project-Aurora/Scripts/VoronScripts/Resources/gpu.gif)
![Load of cpu cores on top](https://raw.githubusercontent.com/VoronFX/Aurora/voron-scripts/Project-Aurora/Scripts/VoronScripts/Resources/cpu.gif)
![Ping display on F keys](https://raw.githubusercontent.com/VoronFX/Aurora/voron-scripts/Project-Aurora/Scripts/VoronScripts/Resources/pingpulser.gif)
![Load bars](https://raw.githubusercontent.com/VoronFX/Aurora/voron-scripts/Project-Aurora/Scripts/VoronScripts/Resources/bars.gif)

# Download and install
1. Download current profile version [here](https://github.com/VoronFX/Aurora/releases/latest).
2. Just extract files in archive to your profiles folder ("%USERPROFILE%\AppData\Roaming\Aurora\Profiles\")
3. Select Voron profile in Desktop profile if you want everything with color zones
4. Or just use those scripts you want
5. Checkout script files, you can adjust some setting inside
6. Enjoy your geek illumination! ^_^

# Requirements
* Aurora itself of minimum v0.6.1-dev version [Link to Aurora project](https://github.com/antonpup/Aurora)
* For older Aurora versions look at old scripts here: [Voron Scripts v1.0-beta.5](https://github.com/VoronFX/Aurora/tree/voron-scripts-aurora6) 

# Credit
* [Aurora](https://github.com/antonpup/Aurora) - The program that made it possible
* [hwmonitor](http://openhardwaremonitor.org/) - Some code used for monitoring gpu utilization
