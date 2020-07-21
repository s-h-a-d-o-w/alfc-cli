# Aorus Laptop Fan Control

**NOTE: Everything must be run from an admin prompt.**

This tool was tested on a Gigabyte Aorus 15G. It was made possible by 
the discussion [here](https://www.reddit.com/r/gigabyte/comments/h0zpfg/aero_15_deep_fan_controlcenter_fix/).

While hacked dlls sort of work, they can break the custom curve UI in the Control Center or even keep it from starting 
altogether.

This method simply overrides the Control Center's profile whenever it is run. The next time the Control Center is used or the 
machine reboots, the settings are lost. And so if you want to persist them, check out how to create a login task 
below.

## Usage

```
alfc <profile name>.txt
```

Sample profiles can simply be taken from the root of this repo. (Note that `densehigh.txt` and 
`denselow.txt` are purely for experimenting - see "Customizing profiles" below.)

## Creating a login task for Task Scheduler

```
create_login_task profile.txt
```

If the task already exists, it will be overwritten - in case you change your mind about 
which profile to use. It is run 1 minute after login, to ensure that all Gigabyte things 
have already run.

If you don't want the command prompt to pop up at that point, you'll have to edit the task 
in Task Scheduler and tick "Hidden" as well as "Run whether user is logged on or not".
(If someone knows an automated way to achieve this - contributions are welcome. 🙂 )

## Customizing profiles

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

## Development Notes

This is the first thing I've written in C#, so if the code doesn't look pretty, that's why. ;)

## TODO

If someone has time to do any of these - please let me know! The goal is 
to replace the Gigabyte Control Center with a minimalist set of tools:

- Ability to switch between CPU/GPU profiles.
- Use backlight to indicate num/caps lock status.
- Use the "fan" function key to switch between two fan profiles.
- (Unsure about the color profiles. It looks like an ICC profile is assigned to 
the display anyway. Then again - maybe they're using a combination of GPU LUT and 
that ICC profile.)
