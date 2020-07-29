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
