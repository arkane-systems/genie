# genie

[![ko-fi](https://www.ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/I3I1VA18)

## A quick way into a systemd "bottle" for WSL

What does that even mean?

Well, this gives you a way to run systemd as pid 1, with all the trimmings, inside WSL 2. It does this by creating a pid namespace, the eponymous poor-man's-container "bottle", starting up systemd in there, and entering it, and providing some helpful shortcuts to do so.

If you want to try it, please read this entire document first, _especially_ the BUGS section.

## NOTE: WSL 2 ONLY

Note: it is only possible to run _systemd_ (and thus _genie_ ) under WSL 2; WSL 1 does not support the system calls required to do so. If you are running inside a distro configured as WSL 1, even if your system supports WSL 2, genie will fail to operate properly.

## INSTALLATION

If there is a package available for your distribution, this is the recommended method of installing genie.

### Debian

Dependent packages on Debian are _daemonize_, _dbus_, _dotnet-runtime-5.0_, _policykit-1_, _systemd_ and _systemd-container_ . For the most part, these are either already installed or in the distro and able to be installed automatically. You need _debhelper_ and _dotnet-sdk-5.0_ (and optionally _pbuilder_) to build the Debian package, but not to simply build _genie_ or install locally.

The chief exception is _dotnet-runtime-5.0_ , for which you will need to follow the installation instructions here:

https://dotnet.microsoft.com/download/

To install, add the wsl-translinux repository here by following the instructions here:

https://packagecloud.io/arkane-systems/wsl-translinux

then install genie using the commands:

```bash
sudo apt update
sudo apt install -y systemd-genie
```

#### PLEASE NOTE

If you were using the gemfly.com repository (wsl-translinux.arkane-systems.net), that has now been discontinued since the whole bloody company seems to have disappeared from the internet.

If you cannot install from the packagecloud.io repository, especially near the end of the month, it's probably because we're over our bandwidth quota. Please download the package from the releases page and install it manually using _dpkg -i_ . Alternatively, wait a few days and try again.

### Other Distros

Debian is the "native" distribution for _genie_ , for which read, "what the author uses". Specifically, Debian stretch+, with _usrmerge_ installed. If you're using anything else, you may need to tweak the configuration file (see below) accordingly.

#### Other

We're actively looking for maintainers for everything else. If you can use .deb packages (especially if your distro is a Debian derivative), the Debian package may work for you. Otherwise, manually installing from the .tar.gz is probably the best I can suggest.

_I am unable to support distributions which there are not prebuilt packages for. I am actively seeking maintainers for these packages._

### ...OR BUILD IT YOURSELF

It is possible to build your own version of genie and install it locally. To do so, you will require _build-essential_ and _dotnet-sdk-5.0_ in addition to the other dependencies, all of which must be installed manually.

After cloning the repository, run

```
sudo make local
```

This will build genie and install it under _/usr/local_ .

## CONFIGURATION FILE

That would be the file _/etc/genie.ini_. This defines the secure path (i.e., those directories in which genie will look for the utilities it depends on), and also the explicit path to _unshare(1)_, required by _daemonize(1)_. Normally, it looks like this:

```
[genie]
secure-path=/lib/systemd:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
unshare=/usr/bin/unshare
update-hostname=true
```

The secure-path setting should be generic enough to cover all but the weirdest Linux filesystem layouts, but on the off-chance that yours stores binaries somewhere particularly idiosyncratic, you can change it here. Meanwhile, the _unshare_ setting is much more likely to be system-dependent; if you are getting errors running genie, please replace this with the output of `which unshare` before trying anything else.

The update-hostname setting controls whether or not genie updates the WSL hostname when creating the bottle. By default, genie updates a hostname _foo_ to _foo-wsl_, to prevent hostname clashes between the host Windows machine and the WSL distribution, especially when communicating back and forth between the two. However, as recent Insider builds allow the hostname of the WSL distributions to be set in __.wslconfig_, this option has been provided to disable genie's intervention and keep the original hostname.

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
  --shutdown, -u             Shut down systemd and exit the bottle.
```

So, it has three modes, all of which will set up the bottle and run systemd in it if it isn't already running for simplicity of use.

_genie -i_ will set up the bottle - including changing the WSL hostname by suffixing -wsl, to distinguish it from the Windows host -  run systemd, and then exit. This is intended for use if you want services running all the time in the background, or to preinitialize things so you needn't worry about startup time later on, and for this purpose is ideally run from Task Scheduler on logon.

_genie -s_ runs your login shell inside the bottle; basically, Windows-side, _wsl genie -s_ is your substitute for just _wsl_ to get started, or for the shortcut you get to start a shell in the distro. It follows login semantics, and as such does not preserve the current working directory.

_genie -l_ opens a login session within the bottle. This permits you to log in to the WSL distribution as any user. The login prompt will return when you log out; to terminate the session, press ^] three times within one second. It follows login semantics, and as such does not preserve the current working directory.

_genie -c [command]_ runs _command_ inside the bottle, then exits. The return code is the return code of the command. It follows sudo semantics, and so does preserve the cwd.

Meanwhile, _genie -u_ , run from outside the bottle, will shut down systemd cleanly and exit the bottle. This uses the _systemctl poweroff_ command to simulate a normal Linux system shutting down. It is suggested that this be used before shutting down Windows or restarting the Linux distribution to ensure a clean shutdown of systemd services.

While not compulsory, it is recommended that you shut down and restart the WSL distro before using genie again after you have used _genie -u_. See BUGS, below, for more details.

## RECOMMENDATIONS

Once you have this up and running, I suggest disabling via systemctl the _getty@tty1_ service (since logging on and using WSL is done via ptsen, not ttys).

Further tips on usage from other genie users can be found on the wiki for this repo.

## BUGS

1. It is considerably clunkier than I'd like it to be, inasmuch as you have to invoke genie every time to get inside the bottle, either manually (replacing, for example, _wsl [command]_ with _wsl genie -c [command]_), or by using your own shortcut in place of the one WSL gives you for the distro, using which will put you _outside_ the bottle. Pull requests, etc.

2. genie is not idempotent; i.e., it is possible that changes made by genie or by systemd inside the bottle will not be perfectly reverted when the genie bottle is shut down with _genie -u_ . As such, it is recommended that you terminate the entire wsl session with _wsl -t <distro>_ or _wsl --shutdown_ in between stopping and restarting the bottle, or errors may occur.
