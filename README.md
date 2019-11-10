# genie

[![ko-fi](https://www.ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/I3I1VA18)

A quick way into a systemd "bottle" for WSL

What does that even mean?

Well, this gives you a way to run systemd as pid 1, with all the trimmings, inside WSL 2. It does this by creating a pid namespace, the eponymous poor-man's-container "bottle", starting up systemd in there, and entering it, and providing some helpful shortcuts to do so.

If you want to try it, please read this entire document first, _especially_ the BUGS section.

For those familiar with or coming here from my first cut (https://randomactsofcoding.wordpress.com/2019/06/13/systemd-on-wsl2/) at attempting to get _systemd_ working, this is the revised one after being schooled on the topic by @therealkenc, over here https://github.com/microsoft/WSL/issues/994 .

## NOTE: WSL 2 ONLY

Note: it is only possible to run _systemd_ (and thus _genie_ ) under WSL 2; WSL 1 does not support the system calls required to do so. If you are running inside a distro configured as WSL 1, even if your system supports WSL 2, genie will fail to operate properly.

## INSTALLATION

You will first need to _apt install_ the _dbus_, _policykit-1_ and _daemonize_ packages. You will also need to install .NET Core 3.0 inside WSL, following the instructions here: https://dotnet.microsoft.com/download/linux-package-manager/debian9/runtime-3.0.0 . Genie also has a dependency on hostess ( https://github.com/cbednarski/hostess ), a copy of which has been placed in the wsl-translinux repo for your convenience and should install automatically.

Debian and Ubuntu LTS users can simply install from the wsl-translinux apt repository, here: https://packagecloud.io/arkane-systems/wsl-translinux . Most dependencies will install automatically, but since the .NET Core runtime is in its own repository, you will still need to install it first.

For Arch Linux users, there are prebuilt packages available at:

https://aur.archlinux.org/packages/genie-systemd/ and

https://aur.archlinux.org/packages/genie-systemd-git/

The former of which is prebuilt and the latter of which compiles it from source. Both install all needed dependencies. Thanks to Arley Henostroza for providing these.

(Other installation methods forthcoming after revision.)

### ...OR BUILD IT YOURSELF

(Build instructions forthcoming after revision.)

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
  -c, --command <COMMAND>    Initialize the bottle (if necessary), and run the specified command in it.
```

So, it has three modes, all of which will set up the bottle and run systemd in it if it isn't already running for simplicity of use.

_genie -i_ will set up the bottle - including changing the WSL hostname by suffixing -wsl, to distinguish it from the Windows host -  run systemd, and then exit. This is intended for use if you want services running all the time in the background, or to preinitialize things so you needn't worry about startup time later on, and for this purpose is ideally run from Task Scheduler on logon.

_genie -s_ runs your login shell inside the bottle; basically, Windows-side, _wsl genie -s_ is your substitute for just _wsl_ to get started, or for the shortcut you get to start a shell in the distro. It follows login semantics, and as such does not preserve the current working directory.

_genie -c [command]_ runs _command_ inside the bottle, then exits. The return code is the return code of the command. It follows sudo semantics, and so does preserve the cwd.

## RECOMMENDATIONS

Once you have this up and running, I suggest disabling via systemctl the _getty@tty1_ service (since logging on and using WSL is done via ptsen, not ttys).

Further tips on usage from other genie users can be found on the wiki for this repo.

## DISTRIBUTIONS

Personally tested by me:

 * Debian 11 (bullseye)
 * Debian 10 (buster)
 * Debian 9 (stretch)
 
Reported working:

 * Ubuntu 18.04 (xenial) - **except** for _genie -s_ (use _genie -c bash_ instead). Please see case https://github.com/arkane-systems/genie/issues/28 for more details.
 * Ubuntu 19.04 (disco)

I have a report of this (mostly) working for Arch, but I also have reports of various odd issues with it not working or not fully working on Arch. I could very much use some help debugging here.

Note that this does not imply that it won't work on other distributions; merely that no-one's tried it and reported it back to me yet. If you do, please do.

## BUGS

1. This breaks _pstree_ and other _/proc_-walking tools that count on everything being a child of pid 1, because entering the namespace with a shell or other process leaves that process with a ppid of 0. To the best of my knowledge, I can't set the ppid of a process, and if I'm wrong about that, please send edification and pull requests to be gratefully accepted.

2. It is considerably clunkier than I'd like it to be, inasmuch as you have to invoke genie every time to get inside the bottle, either manually (replacing, for example, _wsl [command]_ with _wsl genie -c [command]_), or by using your own shortcut in place of the one WSL gives you for the distro, using which will put you _outside_ the bottle. Pull requests, etc.
