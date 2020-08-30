# Aorus Laptop Fan Control

## Changelog

### 2020-08-04

Refined algorithm that decides when to spin up. It tends to ignore brief spikes now. As 
the fans take about a second to ramp up, they wouldn't be of help anyway unless the CPU/GPU 
stays hot for more than 2-3 seconds.

And so now it collects temperatures every 200 ms and only ramps up if the average temperature 
wants to trigger a fan speed increase for 2 seconds.

## Prerequisites

- Node.js (if you don't use it yourself, a global install via the NodeSource repo is probably best 
(people who regularly work with it tend to use `nvm`), especially considering that that is needed 
to set up a systemd unit anyway.)
- Installation of [this kernel module](https://github.com/nix-community/acpi_call). (Enables issuing of fan control commands.)

Linux pros - feel free to contribute to the following guide! 🙂

If you've never installed a kernel module, one way is like the following (If you have secure boot 
enabled, you need to do [this](https://gist.github.com/dop3j0e/2a9e2dddca982c4f679552fc1ebb18df) first. 
Plus, `make` might complain if you're missing compilers but it worked out of the box on Mint for me.):

```
sudo make dkms-add
sudo make dkms-build
sudo make dkms-install
sudo modprobe acpi_call
```

Once run, `sudo modprobe acpi_call` can be run by itself after a reboot to reinstall the 
module.

And so if you want to run this tool on startup, `sudo modprobe acpi_call` needs to run 
before this script.

## Usage

Clone this repo and in this directory (`linux`), do the following:

```
sudo node . CPU.txt GPU.txt

# or to print some information...
sudo node . --status
sudo node . --debug CPU.txt GPU.txt
```

## Creating startup services

Personally, I used systemd for it. For `acpi_call` at `/etc/systemd/system/acpi_call.service`:

```
[Unit]
Description=acpi_call

[Service]
Type=oneshot
ExecStart=modprobe acpi_call

[Install]
WantedBy=default.target
```

And one for this script which requires a global Node.js installation e.g. via the NodeSource repository. Then at 
`etc/systemd/system/alfc.service`:

```
[Unit]
Description=Aorus Laptop Fan Control
After=acpi_call

[Service]
WorkingDirectory=/<path to where you cloned/downloaded repo>/linux
ExecStart=/usr/bin/node . CPU.txt GPU.txt

[Install]
WantedBy=default.target
```

Once this is in place, you can also still experiment with the settings 
by editing `CPU.txt` and `GPU.txt` and then running `sudo systemctl 
restart alfc.service` in `/etc/systemd/system`.

You can also use the `--debug` switch in `ExecStart` of the `alfc` unit and 
look at logs using `sudo systemctl status -n <number of lines> alfc.service`.

## Differences to the Windows version

- This needs to keep running, since it actively controls the fans.
- No need to specify 15 curve points, just the temperatures at which you 
want the fans to spin up to which speed.

No worries about CPU usage. Since the temperatures are only checked every 
two seconds, this script consumes next to nothing.

Also no worries about temperature only being checked every two seconds - 
some of the profiles this laptop ships with react a lot slower than that.

### Reason for the differences

I don't know why (let me know if you do) but contrary to Windows, setting the fan 
curve points doesn't work. **All** ACPI calls I've tried worked **except** for the 
one to set those points.

### Notes on how it works and more on the differences

CPU and GPU curves are both evaluated against the current temperatures and 
whichever results in the higher fan speed is applied for both fans. This is done 
because of the mostly shared heat pipes.

With some more fine-tuning, this could potentially turn out even better than the 
Windows version, since it e.g. allows for control over how quickly the fans 
should ramp up or down. Currently, ramping up happens from one "cycle" (two seconds) 
to the next while ramping down is done only once the fan speed is supposed to be lowered 
for three consecutive cycles. This is to avoid erratic fan behavior.

Plus, no built-in "smart" behavior that you have to try to work with by putting 
multiple curve points close together. Just straightforward declaration of certain 
thresholds.

## Wishlist

- To further reduce erratic fan behavior: Temperature should probably be retrieved 
more often (maybe use the object instead of the method for that?) and the decision on 
what fan speed would currently need to be applied be based on the average of the 
last 4 data points or so.

- More elegant? => Would it be possible to simply keep write/read handles to 
`/proc/acpi/call` open instead of using shell commands to write/read to/from it?
Maybe [similar to this](https://stackoverflow.com/a/25437387/5040168).
