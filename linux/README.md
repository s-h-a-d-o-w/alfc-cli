# Aorus Laptop Fan Control

## Prerequisites

- Node.js
- Installation of [this kernel module](https://github.com/s-h-a-d-o-w/acpi_call). (Enables issuing of fan control commands.)

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
node . CPU.txt GPU.txt

# or to print some information...
node . --status
node . --debug CPU.txt GPU.txt
```

## Differences to the Windows version

- This needs to keep running, since it actively controls the fans.
- No need to specify 15 curve points, just the temperatures at which you 
want the fans to spin up to which speed.

No worries about CPU usage. Since the temperatures are only checked every 
two seconds, this script consumes next to nothing.

### Reason for the differences

I don't know why (let me know if you do) but contrary to Windows, setting the fan 
curve points doesn't work. **All** ACPI calls I've tried worked **except** for the 
one to set those points.

### Notes on how it works and more on the differences

With some more fine-tuning, this could potentially turn out even better than the 
Windows version, since it e.g. allows for control over how quickly the fans 
should ramp up or down. Currently, ramping up happens from one "cycle" (two seconds) 
to the next while ramping down is done only once the fan speed is supposed to be lowered 
for three consecutive cycles. This is to avoid erratic fan behavior.

Plus, no built-in "smart" behavior that you have to try to work with by putting 
multiple curve points close together. Just straightforward declaration of certain 
thresholds.
