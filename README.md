# Aorus Fan Control

**NOTE: Everything must be run from an admin prompt.**

This tool was tested on a Gigabyte Aorus 15G laptop. It was made possible by 
[@avdaga's work here](https://gitlab.com/avdaga/controlcenter-deepfan-fix/-/tree/master).

While hacked dlls sort of work, they can break the custom curve UI in the Control Center or even keep it from starting 
altogether.

This method simply overrides the Control Center's profile whenever it is run. The next time the Control Center is used or the 
machine reboots, the settings are lost. And so if one wants to persist them, one has to run this on startup and has to make sure 
that it runs after the Control Center.

## Usage

```
afc profile.txt
```

Any profile has to contain exactly 15 fan curve points. (Quite a few examples in this repo.)

The density of the points matters, as you can try out yourself with the example 
profiles in this repo called `densehigh.txt` and `denselow.txt`. In my tests, with `densehigh.txt`, it took the fans 
5 sec. to spin up when hitting 90 degrees vs. 30 sec. with `denselow.txt`. So having a lot of points 
for low temperatures probably isn't a good idea.

And so the default profile is set up in a way that it ramps up relatively quickly, stays at 50% or 100% if necessary 
and will only return to 15% when the temperature has been low for a while. (I didn't choose to go to 100% immediately 
because if there are sustained loads, it would keep fluctuating between 100% and 15%.)

## Creating a login task for Task Scheduler:

```
create_login_task profile.txt
```

If the task already exists, it will be overwritten - in case you change your mind about 
which profile to use. It is run 1 minute after login, to ensure that all the Gigabyte things 
have run.

If you don't want the command prompt to pop up at that point, you'll have to edit the task 
in Task Scheduler and tick "Hidden" as well as "Run whether user is logged on or not".

## Development Notes

This is the first thing I've written in C#, so if the code doesn't look pretty, that's why. ;)

## TODO

If another developer has time to do any of these - please let me know! The goal is 
to replace the Gigabyte Control Center with a minimalist set of tools.

- Ability to switch between CPU/GPU profiles.
- Use backlight to indicate num/caps lock status.
- Use the "fan" function key to switch between two fan profiles.


Shortcuts for function keys to switch between fan profiles?
Maybe replicate this using wmi from npmjs?
Electron for an easily extensible, TS-based UI?

Fan speed: 0-229
Max. fan curve indices: 0-14 (At least that's what Gigabyte uses and there's probably limited space wherever this data gets pushed.)
     !!! Need to supply speeds for all indices, otherwise unpredictable behavior (well... maybe people who know the 
         ins and outs of the drivers know why it behaves the way it does) happens !!!
Temperature range: Probably 40-100 but it only makes sense to have 40-89 configurable.

