<p align="center"><img src="./othersrc/docs/wslgenie.png"/></p>

 # genie

<!--
[ ![ci](https://github.com/arkane-systems/genie/workflows/ci/badge.svg?branch=master) ](https://github.com/arkane-systems/genie/actions?query=workflow%3Aci+branch%3Amaster)

-->

## A quick way into a systemd "bottle" for WSL

What does that even mean?

Well, this gives you a way to run systemd as pid 1, with all the trimmings, inside WSL 2. It does this by creating a pid namespace, the eponymous poor-man's-container "bottle", starting up systemd in there, and entering it, and providing some helpful shortcuts to do so.

## REQUIREMENTS

**NOTE:** Before you install _genie_ for the first time, read **ALL** of this page. This will save you a great deal of trouble later on. Especially, please note that on many distributions you **will** encounter the problem described under "Warning: Timing Out" below when you first run genie, and will need to resolve it before your system will operate correctly.

It is a good idea to set your systemd default target to _multi-user.target_ before installing genie. This is the target that genie is designed to work with, since the default _graphical.target_ used by many distributions includes services for the graphical desktop that would take, at minimum, considerable reconfiguration before operating properly under the WSL/WSLg environment.

Also read the [WSLg FAQ](https://github.com/arkane-systems/genie/wiki/WSLg-FAQ) and the [known-problematic systemd units list](https://github.com/arkane-systems/genie/wiki/Systemd-units-known-to-be-problematic-under-WSL) for known problems and known solutions to them.

More information, tips & tricks, etc. are available on [the genie wiki](https://github.com/arkane-systems/genie/wiki). Please consult it before opening an issue.

### NOTE: WSL 2 ONLY

Note: it is only possible to run _systemd_ (and thus _genie_ ) under WSL 2; WSL 1 does not support the system calls required to do so. If you are running inside a distro configured as WSL 1, even if your system supports WSL 2, genie will fail to operate properly.

## INSTALLATION

If there is a package available for your distribution, this is the recommended method of installing genie.

### Debian

Dependent packages on Debian are _daemonize_, _dbus_, _gawk_, _libc6_ (>= 2.2.5), _policykit-1_, _python3_ (>= 3.7), _python3-pip_, _systemd_ (>= 232-25), and _systemd-container_ (>= 232-25). These should all be in the distro and able to be installed automatically.

To install, add the wsl-translinux repository here by following the instructions here:

https://arkane-systems.github.io/wsl-transdebian/

then install genie using the commands:

```bash
sudo apt update
sudo apt install -y systemd-genie
```

#### Ubuntu & Other Debian Derivatives

Use the above Debian package. For current Ubuntu releases and the timing-out problem, see the problematic units listed on [the genie wiki](https://github.com/arkane-systems/genie/wiki).

<!--

### Arch

An Arch package (.zst) can be downloaded from the releases, to right. Install it manually, using `pacman -U <file>`.

### Fedora

A Fedora package (.rpm) can be downloaded from the releases, to right. Install it manually, using `dnf install <file>`.

-->

### Other Distros

If your distribution supports any of the package formats available, you may wish to try downloading the relevant format and giving it a try. This will almost certainly need some tweaking to work properly.

Debian is the "native" distribution for _genie_ , for which read, "what the author uses". Specifically, Debian buster+, with _usrmerge_ installed. If you're using anything else, you may need to tweak the configuration file (see below) accordingly.

#### TAR

There is a .tar.gz of a complete genie install available from the releases, to right. As a last resort, you can try untarring this (it contains every needed file, with the correct permissions, in the correct path from /) onto your system while root. Don't do this unless you're confident you know what you're doing, you're willing to go looking for any resulting issues yourself, and you aren't afraid of accidentally breaking things. You will need to install the dependencies listed above beforehand.

If you install from the tarball, you will need to enable the _wslg-xwayland.service_ and _wslg-xwayland.socket_ systemd units manually.

#### Maintainers Wanted!

We're actively looking for maintainers for everything that doesn't have a specific package. If you have the time, please contribute.

_I am unable to support distributions which there are not prebuilt packages for. I am actively seeking maintainers for these packages._

## CONFIGURATION FILE

That would be the file _/etc/genie.ini_. This defines the secure path (i.e., those directories in which genie will look for the utilities it depends on; make sure _unshare_, in particular, is available here), and seven settings controlling genie behavior. Normally, it looks like this:

```
[genie]
secure-path=/lib/systemd:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
update-hostname=true
update-hostname-suffix=-wsl
clone-path=false
clone-env=WSL_DISTRO_NAME,WSL_INTEROP,WSLENV,DISPLAY,WAYLAND_DISPLAY,PULSE_SERVER
systemd-timeout=240
resolved-stub=false
target-warning=true
```

The _secure-path_ setting should be generic enough to cover all but the weirdest Linux filesystem layouts, but on the off-chance that yours stores binaries somewhere particularly idiosyncratic, you can change it here.

The _update-hostname_ setting controls whether or not genie updates the WSL hostname when creating the bottle. By default, genie updates a hostname _foo_ to _foo-wsl_, to prevent hostname clashes between the host Windows machine and the WSL distribution, especially when communicating back and forth between the two.

However, as recent WSL builds allow the hostname of the WSL distributions to be set in _.wslconfig_, this option has been provided to disable genie's intervention and keep the original hostname. Additionally, the _update-hostname-suffix_ setting allows you to change the suffix added to the original hostname to create the WSL hostname.

**HOWEVER** if you are using [bridged networking](https://randombytes.substack.com/p/bridged-networking-under-wsl), which uses separate IP addresses for WSL, often acquired via DHCP, we _strongly_ recommend not disabling this feature. On many networks, acquiring an address for WSL via DHCP with the same hostname as your Windows machine will remove your Windows machine's IP address from DNS, with irritatingly vague consequences.

The _clone-path_ setting controls whether the PATH outside the bottle should be cloned inside the bottle. This can be useful since the outside-bottle path may include system-specific directories not mentioned in secure-path, and since the outside-bottle path includes a transformed version of the host machine's Windows path.

If this is set to true, the inside-bottle path will be set to the secure-path combined with the outside-bottle path, with duplicate entries removed. It is set to false by default, for backwards compatibility.

The _clone-env_ setting lists the environment variables which are copied from outside the bottle to inside the bottle. It defaults to only WSL_DISTRO_NAME, WSL_INTEROP, and WSLENV, needed for correct WSL operation, plus DISPLAY, WAYLAND_DISPLAY, and PULSE_SERVER, needed for WSLg but any other environment variables which should be cloned can be added to this list.

The _systemd-timeout_ setting controls how long (the number of seconds) genie will wait when initializing the bottle for _systemd_ to reach its "running" - i.e. fully operational, with all units required by the default target active - state. This defaults to 240 seconds.

_genie_ (1.44+) provides the _resolved-stub_ option to automatically back up the existing _/etc/resolv.conf_ and replace it with the symlink necessary to run _systemd-resolved_ in stub mode when initializing the bottle, and revert to the backup when the bottle terminates. (**NOTE:** This last is a courtesy and should NOT be interpreted as meaning idempotency is supported in any way; see _BUGS_ .) 1.43 performed this action by default; upgraders from 1.43 who wish to retain this behavior must set _resolved-stub=true_ in the configuration file.

By default, _genie_ (2.0+) warns you upon bottle initialization if the default systemd target is not _multi-user.target_, since this is the default with which _genie_ is designed to work. If you have configured a different systemd target to run correctly with _genie_, this warning can be disabled by setting _target-warning_ to false in the config file.

_genie_ (1.39+) also installs a pair of systemd units (_wslg-xwayland.service_ and _wslg-xwayland.socket_ and an override for _user-runtime-dir@.service_) to ensure that WSLg operates correctly from inside the bottle. If desired, these can be disabled and enabled independently of _genie_ itself.

## USAGE

```
usage: genie [-h] [-V] [-v] (-i | -s | -l | -c ... | -u | -r | -b)

Handles transitions to the "bottle" namespace for systemd under WSL.

optional arguments:
  -h, --help            show this help message and exit
  -V, --version         show program's version number and exit
  -v, --verbose         display verbose progress messages
  -i, --initialize      initialize the bottle (if necessary) only
  -s, --shell           initialize the bottle (if necessary), and run a shell in it
  -l, --login           initialize the bottle (if necessary), and open a logon prompt in it
  -c ..., --command ...
                        initialize the bottle (if necessary), and run the specified command in it
  -u, --shutdown        shut down systemd and exit the bottle
  -r, --is-running      check whether systemd is running in genie, or not
  -b, --is-in-bottle    check whether currently executing within the bottle, or not

For more information, see https://github.com/arkane-systems/genie/
```

So, it has four modes, all of which will set up the bottle and run systemd in it if it isn't already running for simplicity of use.

_genie -i_ will set up the bottle, run systemd, and then exit. This is intended for use if you want services running all the time in the background, or to preinitialize things so you needn't worry about startup time later on, and for this purpose is ideally run from Task Scheduler on logon.

**NOTE:** It is never necessary to run _genie -i_ explicitly; the -s, -l, and -c commands will all set up the bottle if it has not already been initialized.

**NOTE 2:** genie -i DOES NOT enter the bottle for you. It is important to remember that the genie bottle functions like a container, with its own cgroups and separate pid and mount namespaces. While some systemd or systemd-service powered things may work when invoked from outside the bottle, this is **ENTIRELY BY CHANCE**, and is **NOT A SUPPORTED SCENARIO**. You must enter the bottle using `genie -s`, `genie -l` or `genie -c` first. Ways to do this automatically when you start a WSL session can be found on the repo wiki.

_genie -s_ runs your login shell inside the bottle; basically, Windows-side, _wsl genie -s_ is your substitute for just _wsl_ to get started, or for the shortcut you get to start a shell in the distro. It follows login semantics, and as such does not preserve the current working directory.

_genie -l_ opens a login session within the bottle. This permits you to log in to the WSL distribution as any user. The login prompt will return when you log out; to terminate the session, press ^] three times within one second. It follows login semantics, and as such does not preserve the current working directory.

_genie -c [command]_ runs _command_ inside the bottle, then exits. The return code is the return code of the command. It follows sudo semantics, and so does preserve the cwd.

Meanwhile, _genie -u_ , run from outside the bottle, will shut down systemd cleanly and exit the bottle. This uses the _systemctl poweroff_ command to simulate a normal Linux system shutting down. It is suggested that this be used before shutting down Windows or restarting the Linux distribution to ensure a clean shutdown of systemd services.

**NOTE 3:** genie is not and cannot be idempotent. As such, it is strongly recommended that you do not restart genie or continue to use the WSL distro session after using _genie -u_. See **BUGS**, below.

_genie -r_ and _genie -b_ are informational commands for use in checking the state of the system and/or scripting genie.

The former checks whether genie (and an associated systemd(1) instance) are currently running. Possible output and return codes are:

  * _running_ (exit code 0) - the bottle (and systemd) are running normally
  * _stopped_ (exit code 1) - no bottle is present
  * _starting_ (exit code 2); and
  * _stopping_ (exit code 3) - the bottle is transitioning between states, please wait
  * _running (systemd errors)_ (exit code 4) - the bottle is up but some systemd services are reporting errors
  * _unknown_ (exit code 5) - unable to determine bottle state

The latter, meanwhile, checks whether the current command is executing inside the bottle. Possible output and return codes are:

  * _inside_ (exit code 0) - inside the bottle
  * _outside_ (exit code 1) - outside the bottle (bottle exists)
  * _no-bottle_ (exit code 2) - no bottle is present

While running, genie stores the external PID of the systemd instance in the file _/run/genie.systemd.pid_ for use in user scripting. It does not provide a similar file for the internal PID for obvious reasons.

While not compulsory, it is recommended that you shut down and restart the WSL distro before using genie again after you have used _genie -u_. See BUGS, below, for more details.

### WARNING: TIMING OUT

If _genie_ (1.31+) seems to be blocked at the

`"Waiting for systemd...!!!!!"`

stage, this is because of the new feature in 1.31 that waits for all _systemd_ services/units to have started up before continuing, to ensure that they have started before you try and do anything that might require them. (I.e., it waits for the point at which a normal Linux system would have given you a login prompt.) It does this by waiting for _systemd_ to reach the "running" state.

If it appears to have blocked, wait until the timeout (by default, 240 seconds), at which point a list of units which have not started property will be displayed. Fixing or disabling those units such that _systemd_ can start properly will also allow _genie_ to start properly. Known-problematic units are listed on [the genie wiki](https://github.com/arkane-systems/genie/wiki).

### RECOMMENDATIONS

Once you have this up and running, I suggest disabling via systemctl the _getty@tty1_ service (since logging on and using WSL is done via ptsen, not ttys).

Further tips on usage from other genie users can be found on the wiki for this repo.

## BUGS

1. It is considerably clunkier than I'd like it to be, inasmuch as you have to invoke genie every time to get inside the bottle, either manually (replacing, for example, _wsl [command]_ with _wsl genie -c [command]_), or by using your own shortcut in place of the one WSL gives you for the distro, using which will put you _outside_ the bottle. Pull requests, etc. But see also [RunInGenie](https://github.com/arkane-systems/RunInGenie)!

2. genie is not idempotent; i.e., it is possible that changes made by genie or by systemd inside the bottle will not be perfectly reverted when the genie bottle is shut down with _genie -u_ . (Linux pid/mount namespaces aren't perfect containers, and systemd units and other actions inside the bottle can and will change things that affect the outside of the bottle, possibly even across distros. And note that _genie -u_ calls _systemctl poweroff_ which believes that it is shutting down the entire machine; the in-bottle systemd is a full systemd installation, not a cut-down container install.) As such, it is **strongly recommended** that you terminate the entire wsl session with _wsl -t <distro>_ or _wsl --shutdown_ in between stopping and restarting the bottle, or errors may occur; we cannot support such scenarios.

3. As of 1.38, while WSLg operates correctly with _genie_ and GUI apps can be run from inside the bottle, Linux GUI apps started from the Windows Start Menu items created by WSLg will run outside the bottle. This is being worked on.

## OLDER VERSIONS

For information on versions of genie prior to 2.0, see the saved older README-1.44.md file in this repository.
