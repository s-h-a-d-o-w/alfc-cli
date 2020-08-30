# Superseded by the [version with a web-based UI](https://github.com/s-h-a-d-o-w/alfc)

The version with UI contains improvements to the fan control and create 
services instead of having to create tasks in task scheduler and whatnot. 


So this repo serves mostly just as an archive for how things started out. 
Might be useful for people who want to learn who to create such a tool 
and for my future self. 😉

# Aorus Laptop Fan Control (CLI)

## Linux => Go [HERE](./linux/README.md)

## Windows

**NOTE: Everything must be run from an admin command prompt.**

This tool was tested on a Gigabyte Aorus 15G. It was made possible by 
the discussion [here](https://www.reddit.com/r/gigabyte/comments/h0zpfg/aero_15_deep_fan_controlcenter_fix/).

While hacked dlls sort of work, they can break the custom curve UI in the Control Center or even keep it from starting 
altogether.

This method simply overrides the Control Center's profile whenever it is run. The next time the Control Center is used or the 
machine reboots, the settings are lost. And so if you want to persist them, check out how to create a login task 
below.

### Usage

```
alfc <profile name>.txt
```

Sample profiles can simply be taken from the root of this repo. (Note that `densehigh.txt` and 
`denselow.txt` are purely for experimenting - see "Customizing profiles" below.)

Current status (RPM and stored fan curve points):
```
alfc --status
```

### Creating tasks in Task Scheduler

1. Clone/download this repo
2. Download latest release .exe or build it yourself and put it into the root of the repo
3. Run: `create_tasks <profile name>.txt`

If the tasks already exists, they will be overwritten - in case you change your mind about 
which profile to use. They are run 1 minute after login or waking up from hibernation, to 
ensure that all Gigabyte things have already run.

If you don't want the command prompt to pop up at that point, you'll have to edit the tasks 
in Task Scheduler and tick "Hidden" as well as "Run whether user is logged on or not".
(If someone knows an automated way to achieve this - contributions are welcome. 🙂 )

### Customizing profiles

Any profile has to contain exactly 15 fan curve points. (Quite a few examples in this repo.)

The density of the points matters, as you can try out yourself with the example 
profiles in this repo called `densehigh.txt` and `denselow.txt`. In my tests, with `densehigh.txt`, it took the fans 
5 sec. to spin up when hitting 90 degrees vs. 30 sec. with `denselow.txt`. So having a lot of points 
for low temperatures probably isn't a good idea.

Which is why the default profile is set up in a way that it ramps up relatively quickly, stays at 50% or 100% 
if necessary and will only return to 15% when the temperature has been low for a while. 
(I didn't choose to go to 100% immediately because if there are sustained loads, it 
would keep fluctuating between 100% and 15%.)

Also - even though the CPU throttles at around 90 degrees, declaring points above 90 makes sense in order to 
get that point density up.

### Development Notes

This is the first thing I've written in C#, so if the code doesn't look pretty, that's why. ;)

I initially copy/pasted Gigabyte's code from the DLL but eventually refactored it because 
they handle WMI call exceptions inconsistently (sometimes ignoring them, sometimes printing them) 
and I think they should always be printed.

For those not familiar with it - `csc` is a stand-alone C# compiler that doesn't require 
for Visual Studio to be installed.

External dlls can be included through... [Hope I won't forget to finish this sentence.]

## Wishlist

### More features

If someone has time to do any of these - please let me know! The goal is 
to replace the Gigabyte Control Center with a minimalist set of tools:

- Ability to switch between CPU/GPU profiles.
- Use backlight to indicate num/caps lock status.
- Use the "fan" function key to switch between two fan profiles.

Steps in the direction of independence that can already be done relatively 
easily:

- Color profile installation: Using the precalibrated color profiles already 
works by either choosing the profile in the Control Center and then uninstalling 
the Control Center or simply installing an ICM file (from `color\P75\SHP14C5` 
when you extract the installer) yourself.
- Making Gigabyte's WMI classes (which are required for this tool) available 
without installing the Control Center (they are also removed when uninstalling): 
Copy the acpimof.dll from the extracted 
installer to "C:\Windows\SysWOW64", create a string value in the Registy at 
`Computer\HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WmiAcpi` called `MofImagePath`
that contains `C:\Windows\SysWOW64\acpimof.dll` and reboot. (For more on this, 
[see here](https://github.com/microsoft/Windows-driver-samples/tree/master/wmi/wmiacpi#installation))

### UI

This is something that may be feasible to do for myself. I'm thinking 
a Windows service based on Node.js that runs the C# WMI code via [Edge.js](https://github.com/tjanczuk/edge)
and offers a web interface. And on Linux, the service would trigger calls 
via shell like it already does.

Let's see whether it'll happen though... so many things to do, so little time. 😉
