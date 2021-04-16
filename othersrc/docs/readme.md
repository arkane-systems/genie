# genie

[ ![ci](https://github.com/arkane-systems/genie/workflows/ci/badge.svg?branch=master) ](https://github.com/arkane-systems/genie/actions?query=workflow%3Aci+branch%3Amaster)

## A quick way into a systemd "bottle" for WSL

What does that even mean?

Well, this gives you a way to run systemd as pid 1, with all the trimmings, inside WSL 2. It does this by creating a pid namespace, the eponymous poor-man's-container "bottle", starting up systemd in there, and entering it, and providing some helpful shortcuts to do so.

If you want to try it, please read this entire document first, _especially_ the BUGS section.

## NOTE: WSL 2 ONLY

Note: it is only possible to run _systemd_ (and thus _genie_ ) under WSL 2; WSL 1 does not support the system calls required to do so. If you are running inside a distro configured as WSL 1, even if your system supports WSL 2, genie will fail to operate properly.

## INSTALLATION

**NOTE:** Before you install _genie_ for the first time, read **ALL** of this page. This will save you a great deal of trouble later on. Especially, please note that on many distributions you **will** encounter the problem described under "Warning: Timing Out" below when you first run genie, and will need to resolve it before your system will operate correctly.

**NOTE 2:** More information, tips & tricks, etc. are available on [the genie wiki](https://github.com/arkane-systems/genie/wiki). Please consult it before opening an issue.

If there is a package available for your distribution, this is the recommended method of installing genie.

### Debian

Dependent packages on Debian are _daemonize_, _dbus_, _dotnet-runtime-5.0_, _gawk_, _libc6_, _libstdc++6_, _policykit-1_, _systemd_, and _systemd-container_. For the most part, these are either already installed or in the distro and able to be installed automatically. You need _debhelper_ and _dotnet-sdk-5.0_ (and optionally _pbuilder_) to build the Debian package, but not to simply build _genie_ or install locally.

The chief exception is _dotnet-runtime-5.0_ , for which you will need to follow the installation instructions here:

https://dotnet.microsoft.com/download/

To install, add the wsl-translinux repository here by following the instructions here:

https://arkane-systems.github.io/wsl-transdebian/

then install genie using the commands:

```bash
sudo apt update
sudo apt install -y systemd-genie
```

(The packagecloud.io repository is now deprecated. If you are still using it, please update your system to use the repository above.)

#### Ubuntu & Other Debian Derivatives

Use the above Debian package. For current Ubuntu releases and the timing-out problem, see the problematic units listed on [the genie wiki](https://github.com/arkane-systems/genie/wiki).

### Arch

An Arch package (.zst) can be downloaded from the releases, to right. Install it manually, using `pacman -U <file>`.

### Fedora

A Fedora package (.rpm) can be downloaded from the releases, to right. Install it manually, using `dnf install <file>`.

### Other Distros

If your distribution supports any of the package formats available, you may wish to try downloading the relevant format and giving it a try. This will almost certainly need some tweaking to work properly.

Debian is the "native" distribution for _genie_ , for which read, "what the author uses". Specifically, Debian stretch+, with _usrmerge_ installed. If you're using anything else, you may need to tweak the configuration file (see below) accordingly.

#### TAR

There is a .tar.gz of a complete genie install available from the releases, to right. As a last resort, you can try untarring this (it contains every needed file, with the correct permissions, in the correct path from /) onto your system while root. Don't do this unless you're confident you know what you're doing, you're willing to go looking for any resulting issues yourself, and you aren't afraid of accidentally breaking things. You will need to install the dependencies listed above beforehand.

#### Maintainers Wanted!

We're actively looking for maintainers for everything that doesn't have a specific package. If you have the time, please contribute.

_I am unable to support distributions which there are not prebuilt packages for. I am actively seeking maintainers for these packages._

### ...OR BUILD IT YOURSELF

It is possible to build your own version of genie and install it locally. To do so, you will require _build-essential_ and _dotnet-sdk-5.0_ in addition to the other dependencies, all of which must be installed manually.

After cloning the repository, run

```
sudo make install-local
```

This will build genie and install it under _/usr/local_ .

## CONFIGURATION FILE

That would be the file _/etc/genie.ini_. This defines the secure path (i.e., those directories in which genie will look for the utilities it depends on), and the explicit path to _unshare(1)_, required by _daemonize(1)_, and three settings controlling genie behavior. Normally, it looks like this:

```
[genie]
secure-path=/lib/systemd:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
unshare=/usr/bin/unshare
update-hostname=true
clone-path=false
clone-env=WSL_DISTRO_NAME,WSL_INTEROP,WSLENV
systemd-timeout=180
```

The _secure-path_ setting should be generic enough to cover all but the weirdest Linux filesystem layouts, but on the off-chance that yours stores binaries somewhere particularly idiosyncratic, you can change it here. Meanwhile, the _unshare_ setting is much more likely to be system-dependent; if you are getting errors running genie, please replace this with the output of `which unshare` before trying anything else.

The _update-hostname_ setting controls whether or not genie updates the WSL hostname when creating the bottle. By default, genie updates a hostname _foo_ to _foo-wsl_, to prevent hostname clashes between the host Windows machine and the WSL distribution, especially when communicating back and forth between the two. However, as recent Insider builds allow the hostname of the WSL distributions to be set in _.wslconfig_, this option has been provided to disable genie's intervention and keep the original hostname.

The _clone-path_ setting controls whether the PATH outside the bottle should be cloned inside the bottle. This can be useful since
the outside-bottle path may include system-specific directories not mentioned in secure-path, and since the outside-bottle path includes a transformed version of the host machine's Windows path.

If this is set to true, the inside-bottle path will be set to the secure-path combined with the outside-bottle path, with duplicate entries removed. It is set to false by default, for backwards compatibility.

The _clone-env_ setting lists the environment variables which are copied from outside the bottle to inside the bottle. It defaults to only WSL_DISTRO_NAME, WSL_INTEROP, and WSLENV, needed for correct WSL operation, but any other environment variables which should be cloned can be added to this list. This replaces the former ability to copy additional environment variables by editing _/usr/libexec/dumpwslenv.sh_.

The _systemd-timeout_ setting controls how long (the number of seconds) genie will wait when initializing the bottle for _systemd_ to reach its "running" - i.e. fully operational, with all units required by the default target active - state. This defaults to 180 seconds.

## USAGE

```
genie:
  Handles transitions to the "bottle" namespace for systemd under WSL.

Usage:
  genie [options] [command]

Options:
  -v, --verbose <VERBOSE>    Display verbose progress messages
  --version                  Display version information

Commands:
  -i, --initialize           Initialize the bottle (if necessary) only.
  -s, --shell                Initialize the bottle (if necessary), and run a shell in it.
  -l, --login                Initialize the bottle (if necessary), and open a logon prompt in it.
  -c, --command <COMMAND>    Initialize the bottle (if necessary), and run the specified command in it.
  -u, --shutdown             Shut down systemd and exit the bottle.
  -r, --is-running           Check whether systemd is running in genie, or not.
  -b, --is-in-bottle         Check whether currently executing within the genie bottle, or not.
```

So, it has four modes, all of which will set up the bottle and run systemd in it if it isn't already running for simplicity of use.

_genie -i_ will set up the bottle - including changing the WSL hostname by suffixing -wsl, to distinguish it from the Windows host -  run systemd, and then exit. This is intended for use if you want services running all the time in the background, or to preinitialize things so you needn't worry about startup time later on, and for this purpose is ideally run from Task Scheduler on logon.

**NOTE:** It is never necessary to run _genie -i_ explicitly; the -s, -l, and -c commands will all set up the bottle if it has not already been initialized.

**NOTE 2:** genie -i DOES NOT enter the bottle for you. It is important to remember that the genie bottle functions like a container, with its own cgroups and separate pid and mount namespaces. While some systemd or systemd-service powered things may work when invoked from outside the bottle, this is ENTIRELY BY CHANCE, and is NOT A SUPPORTED SCENARIO. You must enter the bottle using `genie -s`, `genie -l` or `genie -c` first. Ways to do this automatically when you start a WSL session can be found on the repo wiki.**

_genie -s_ runs your login shell inside the bottle; basically, Windows-side, _wsl genie -s_ is your substitute for just _wsl_ to get started, or for the shortcut you get to start a shell in the distro. It follows login semantics, and as such does not preserve the current working directory.

_genie -l_ opens a login session within the bottle. This permits you to log in to the WSL distribution as any user. The login prompt will return when you log out; to terminate the session, press ^] three times within one second. It follows login semantics, and as such does not preserve the current working directory.

_genie -c [command]_ runs _command_ inside the bottle, then exits. The return code is the return code of the command. It follows sudo semantics, and so does preserve the cwd.

Meanwhile, _genie -u_ , run from outside the bottle, will shut down systemd cleanly and exit the bottle. This uses the _systemctl poweroff_ command to simulate a normal Linux system shutting down. It is suggested that this be used before shutting down Windows or restarting the Linux distribution to ensure a clean shutdown of systemd services.

_genie -r_ and _genie -b_ are informational commands for use in checking the state of the system and/or scripting genie. The former checks whether genie (and an associated systemd(1) instance) is currently running. It returns the string "running" and exit code 0 if one is found; it returns the string "stopped" and exit code 1 if one is not. The latter checks whether the current command is executing inside the bottle. It returns the string "inside" and exit code 0 if so; it returns the string "outside" and exit code 1 if one is not. If no bottle exists, it returns the string "no-bottle" and exit code 2.

While running, genie stores the external PID of the systemd instance in the file _/run/genie.systemd.pid_ for use in user scripting. It does not provide a similar file for the internal PID for obvious reasons.

While not compulsory, it is recommended that you shut down and restart the WSL distro before using genie again after you have used _genie -u_. See BUGS, below, for more details.

### WARNING: TIMING OUT

If _genie_ (1.31+) seems to be blocked at the

`"Waiting for systemd...!!!!!"`

stage, this is because of the new feature in 1.31 that waits for all _systemd_ services/units to have started up before continuing, to ensure that they have started before you try and do anything that might require them. (I.e., it waits for the point at which a normal Linux system would have given you a login prompt.) It does this by waiting for _systemd_ to reach the "running" state.

If it appears to have blocked, wait until the timeout (by default, 60 seconds), at which point a list of units which have not started property will be displayed. Fixing or disabling those units such that _systemd_ can start properly will also allow _genie_ to start properly. Known-problematic units are listed on [the genie wiki](https://github.com/arkane-systems/genie/wiki).

## RECOMMENDATIONS

Once you have this up and running, I suggest disabling via systemctl the _getty@tty1_ service (since logging on and using WSL is done via ptsen, not ttys).

Further tips on usage from other genie users can be found on the wiki for this repo.

## BUGS

1. It is considerably clunkier than I'd like it to be, inasmuch as you have to invoke genie every time to get inside the bottle, either manually (replacing, for example, _wsl [command]_ with _wsl genie -c [command]_), or by using your own shortcut in place of the one WSL gives you for the distro, using which will put you _outside_ the bottle. Pull requests, etc.

But see also [RunInGenie](https://github.com/arkane-systems/RunInGenie)!

2. genie is not idempotent; i.e., it is possible that changes made by genie or by systemd inside the bottle will not be perfectly reverted when the genie bottle is shut down with _genie -u_ . As such, it is recommended that you terminate the entire wsl session with _wsl -t <distro>_ or _wsl --shutdown_ in between stopping and restarting the bottle, or errors may occur.
