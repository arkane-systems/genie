# genie
A quick way into a systemd "bottle" for WSL

What does that even mean?

Well, this gives you a way to run systemd as pid 1, with all the trimmings, inside WSL 2. It does this by creating a pid namespace, the eponymous poor-man's-container "bottle", starting up systemd in there, and entering it, and providing some helpful shortcuts to do so.

If you want to try it, please read this entire document first, _especially_ the BUGS section.

For those familiar with or coming here from my first cut (https://randomactsofcoding.wordpress.com/2019/06/13/systemd-on-wsl2/) at attempting to get _systemd_ working, this is the revised one after being schooled on the topic by @therealkenc, over here https://github.com/microsoft/WSL/issues/994 .

## INSTALLATION

You will first need to _apt install_ the _dbus_, _policykit-1_ and _daemonize_ packages

Download genie.tar.gz from the releases page, untar it, and place it in _/usr/local/bin_ . Make sure that it is _chown root_, and _chmod u+s_; i.e., setuid root.

### ...OR BUILD IT YOURSELF

Or you can build it easily enough if you don't want to trust the binary. You need python 2.7, with PyInstaller and psutil installed (both available from pip). Simply clone the repository and run the included _./make_.

## USAGE

```
usage: genie [-h] [-v] [-V] (-i | -s | -c COMMAND [COMMAND ...])

Handles transitions to the "bottle" namespace for systemd under WSL.

optional arguments:
  -h, --help            show this help message and exit
  -v, --verbose         display verbose progress messages
  -V, --version         show program's version number and exit
  -i, --initialize      initialize the bottle (if necessary) only
  -s, --shell           initialize the bottle (if necessary) and run a shell
                        in it
  -c COMMAND [COMMAND ...], --command COMMAND [COMMAND ...]
                        initialize the bottle (if necessary), and run the
                        specified command in it
```

So, it has three modes, all of which will set up the bottle and run systemd in it if it isn't already running for simplicity of use.

_genie -i_ will set up the bottle and run systemd, and then exit. This is intended for use if you want services running all the time in the background, or to preinitialize things so you needn't worry about startup time later on, and for this purpose is ideally run from Task Scheduler on logon.

_genie -s_ runs your login shell inside the bottle; basically, Windows-side, _wsl genie -s_ is your substitute for just _wsl_ to get started, or for the shortcut you get to start a shell in the distro.

_genie -c [command]_ runs _command_ inside the bottle, then exits. The return code is the return code of the command.

## RECOMMENDATIONS

Once you have this up and running, I suggest disabling via systemctl the _getty@tty1_ service (since logging on and using WSL is done via ptsen, not ttys).

## BUGS

1. I've only tested this on Debian, because I only use Debian and only have so much time to tinker. Your distro may vary and you may have to hack about with this somewhat to make it work. Pull requests gratefully accepted.

2. At the moment, something (I suspect in the packaging) causes genie to segfault on exiting when invoked with the -s or -c switches _and_ the bottle did not already exist. This doesn't seem to break anything - and is easily avoided by preinitializing with _genie -i_ - but is obviously undesirable. Pull requests gratefully accepted.

3. This breaks _pstree_ and other _/proc_-walking tools that count on everything being a child of pid 1, because entering the namespace with a shell or other process leaves that process with a ppid of 0. To the best of my knowledge, I can't set the ppid of a process, and if I'm wrong about that, please send edification and pull requests to be gratefully accepted.

4. _genie -c_ doesn't preserve the working directory. I'm working on this one when I have a minute, but pull requests will...

5. It is considerably clunkier than I'd like it to be, inasmuch as you have to invoke genie every time to get inside the bottle, either manually (replacing, for example, _wsl [command]_ with _wsl genie -c [command]_), or by using your own shortcut in place of the one WSL gives you for the distro, using which will put you _outside_ the bottle. Pull requests, etc.

6. It is considerably clunkier than I'd like it to be because I'm not actually all that familiar with Linux namespaces, etc., don't do this sort of development much, and so forth. Pull req -- well, by now I think we've established the theme, yes?
