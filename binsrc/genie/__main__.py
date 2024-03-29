#! /usr/bin/env python3

import argparse
import fcntl
import os
import subprocess
import sys
import time

import nsenter

import apparmor
import binfmts
import configuration
import helpers
import host
import resolved

# Global variables
version = "2.5"

verbose = False
login = None

lockfile_fp = None


# Init lock functions
def bottle_init_lock():
    """Lock the bottle init process to one instance only."""
    global lockfile_fp

    lockfile_fp = open('/run/genie.init.lock', 'a')

    try:
        fcntl.lockf(lockfile_fp, fcntl.LOCK_EX | fcntl.LOCK_NB)
        lockfile_fp.seek(0)
        lockfile_fp.truncate()
        lockfile_fp.write(str(os.getpid()))
        lockfile_fp.flush()
        running = False
    except IOError:
        with open('/run/genie.init.lock', 'r') as fp:
            running = fp.read()

    return running


def bottle_init_unlock():
    """Unlock the bottle init process."""
    fcntl.lockf(lockfile_fp, fcntl.LOCK_UN)
    lockfile_fp.close()

    os.remove("/run/genie.init.lock")


# Command line parser
def parse_command_line():
    """Create the command-line option parser and parse arguments."""
    parser = argparse.ArgumentParser(
        description="Handles transitions to the \"bottle\" namespace for systemd under WSL.",
        epilog="For more information, see https://github.com/arkane-systems/genie/")

    # Version command
    parser.add_argument('-V', '--version', action='version',
                        version='%(prog)s ' + version)

    # Verbose option
    parser.add_argument('-v', '--verbose', action='store_true',
                        help="display verbose progress messages")

    # Specify username option
    parser.add_argument('-a', '--as-user', action='store',
                        help="specify user to run shell or command as (use with -s or -c)", dest='user')

    # Commands
    group2 = parser.add_argument_group('commands')
    group = group2.add_mutually_exclusive_group(required=True)

    group.add_argument('-i', '--initialize', action='store_true',
                       help='initialize the bottle (if necessary) only')
    group.add_argument('-s', '--shell', action='store_true',
                       help='initialize the bottle (if necessary), and run a shell in it')
    group.add_argument('-l', '--login', action='store_true',
                       help='initialize the bottle (if necessary), and open a logon prompt in it')
    group.add_argument(
        '-c', '--command', help='initialize the bottle (if necessary), and run the specified command in it\n(preserves working directory)', nargs=argparse.REMAINDER)
    group.add_argument('-u', '--shutdown', action='store_true',
                       help='shut down systemd and exit the bottle')
    group.add_argument('-r', '--is-running', action='store_true',
                       help='check whether systemd is running in genie, or not')
    group.add_argument('-b', '--is-in-bottle', action='store_true',
                       help='check whether currently executing within the bottle, or not')

    group.add_argument('-%', '--parser-test',
                       action='store_true', help=argparse.SUPPRESS)

    return parser.parse_args()


# Subordinate functions.
def pre_systemd_action_checks(sdp):
    """Things to check before performing a systemd-requiring action."""

    if sdp == 0:
        # no bottle exists; this means we should recurse and start one
        initcommand = ["genie", "-i"]

        if verbose:
            initcommand.append("-v")

        init = subprocess.run(initcommand)

        # continue when subprocess done
        if init.returncode != 0:
            sys.exit(
                f"could not initialise bottle, exit code = {init.returncode}")

        # Refresh systemd pid
        sdp = helpers.find_systemd()

    state = helpers.get_systemd_state(sdp)

    if 'stopping' in state:
        sys.exit("genie: systemd is shutting down, cannot proceed")

    if 'initializing' in state or 'starting' in state:
        # wait for it
        print("genie: systemd is starting up, please wait...", end="", flush=True)

        timeout = configuration.system_timeout()

        while ('running' not in state) and timeout > 0:
            time.sleep(1)
            state = helpers.get_systemd_state(sdp)

            print(".", end="", flush=True)

        timeout -= 1

        print("")

        if timeout <= 0:
            print("genie: WARNING: timeout waiting for bottle to start")

    if 'degraded' in state:
        print('genie: WARNING: systemd is in degraded state, issues may occur!')

    if not ('running' in state or 'degraded' in state):
        sys.exit("genie: systemd in unsupported state '"
                 + state + "'; cannot proceed")


def set_secure_path():
    """Set up secure path, saving original if specified."""
    # Default original path.
    # TODO: Should reference system drive by letter
    originalPath = '/mnt/c/Windows/System32'

    if configuration.clone_path():
        originalPath = os.environ['PATH']

    # Set the local path
    os.environ['PATH'] = configuration.secure_path()

    # Create the path file
    with open('/run/genie.path', 'w') as pathfile:
        print(originalPath, file=pathfile)
        pathfile.close()


def stash_environment():
    """Save a copy of the original environment (specified variables only)."""
    # Get variables to stash
    names = configuration.clonable_envars()

    # Do the stashing.
    with open('/run/genie.env', 'w') as envfile:

        # Start with the flag variable that's always added.
        print("INSIDE_GENIE=yes", file=envfile)

        for n in names:
            if n in os.environ:
                print(f"{n}={os.environ[n]}", file=envfile)

        envfile.close()


# Commands
# Parser test
def do_parser_test(arguments):
    """Parser test option."""
    print("genie: congratulations! you found the hidden parser test option!")
    print(arguments)


# Initialize bottle
def inner_do_initialize():
    """Initialize the genie bottle (inner function)."""
    sdp = helpers.find_systemd()

    if sdp != 0:
        sys.exit("genie: bottle is already established (systemd running)")

    # FIRST: As a first step in initing systemd, delete any old runtime pid file
    # if such exists.
    if os.path.exists('/run/genie.systemd.pid'):
        os.remove('/run/genie.systemd.pid')

    # Set secure path, and stash original environment.
    set_secure_path()
    stash_environment()

    # Check and warn if not multi-user.target.
    if configuration.target_warning():
        target = helpers.get_systemd_target()

        if target != 'multi-user.target':
            print(
                f"genie: WARNING: systemd default target is {target}; targets other than multi-user.target may not work")
            print("genie: WARNING: if you wish to use a different target, this warning can be disabled in the config file")
            print("genie: WARNING: if you experience problems, please change the target to multi-user.target")

    # Now that the WSL hostname can be set via .wslconfig, we're going to make changing
    # it automatically in genie an option, enable/disable in genie.ini. Defaults to on
    # for backwards compatibility and because not doing so when using bridged networking is
    # a Bad Idea.
    if configuration.update_hostname():
        host.update(verbose)

    # If configured to, create the resolv.conf symlink for systemd-resolved.
    if configuration.resolved_stub():
        resolved.configure(verbose)

    # Update binfmts config file.
    flags = binfmts.check_flags(verbose)
    binfmts.write_interop_file(verbose, flags)

    # Unmount the binfmts fs before starting systemd, so systemd can mount it
    # again with all the trimmings.
    binfmts.umount(verbose)

    # Define systemd startup chain.
    startupChain = ["daemonize", helpers.get_unshare_path(), "-fp", "--propagation", "shared", "--mount-proc", "--"]

    # Check whether AppArmor is available in the kernel.
    if apparmor.exists():

        # If so, configure AppArmor.
        nsName = apparmor.configure(verbose)

        # Add AppArmor to the startup chain.
        if nsName is not None:
            startupChain = startupChain + \
                ["aa-exec", "-n", nsName, "-p", "unconfined", "--"]

    else:
        if verbose:
            print(
                "genie: AppArmor not available in kernel; attempting to continue without AppArmor namespace")

    # Update startup chain with systemd command.
    startupChain.append("systemd")

    # Run systemd in a container
    if verbose:
        print("genie: starting systemd with command line: ")
        print(' '.join(startupChain))

    # This requires real UID/GID root as well as effective UID/GID root
    suid = os.getuid()
    sgid = os.getgid()

    os.setuid(0)
    os.setgid(0)

    subprocess.run(startupChain)

    os.setuid(suid)
    os.setgid(sgid)

    # Wait for systemd to be up (polling, sigh.)
    sdp = 0
    print("Waiting for systemd...", end="", flush=True)

    while sdp == 0:
        time.sleep(0.5)
        sdp = helpers.find_systemd()

        print(".", end="", flush=True)

    # Wait for systemd to be in running state.
    state = 'initializing'
    timeout = configuration.system_timeout()

    while ('running' not in state and 'degraded' not in state) and timeout > 0:
        time.sleep(1)
        state = helpers.get_systemd_state(sdp)

        print("!", end="", flush=True)

        timeout -= 1

    print("")

    if 'running' not in state:
        print(
            f"genie: systemd did not enter running state ({state}) after {configuration.system_timeout()} seconds")
        print("genie: this may be due to a problem with your systemd configuration")
        print("genie: information on problematic units is available at https://github.com/arkane-systems/genie/wiki/Systemd-units-known-to-be-problematic-under-WSL")
        print("genie: a list of failed units follows:\n")

        with nsenter.Namespace(sdp, 'pid'):
            subprocess.run(["systemctl", "--failed"])

    # LAST: Now that systemd exists, write out its (external) pid.
    # We do not need to store the inside-bottle pid anywhere for obvious reasons.
    with open('/run/genie.systemd.pid', 'w') as pidfile:
        print(sdp, file=pidfile)
        pidfile.close()


def do_initialize():
    """Initialize the genie bottle."""
    if verbose:
        print("genie: starting bottle")

    # Secure the bottle init lock
    running = bottle_init_lock()
    if running:
        # Wait for other process to have started the bottle
        # The last step is the pid file being created, so we wait
        # for that, then return.
        if verbose:
            print(
                f"genie: already initializing, pid={running}, waiting...", end="", flush=True)

        # Allow 10% startup margin
        timeout = configuration.system_timeout() * 1.1

        while not os.path.exists('/run/genie.systemd.pid') and timeout > 0:
            time.sleep(1)
            print(".", end="", flush=True)
            timeout -= 1

        print("")

        if timeout <= 0:
            print("genie: WARNING: timeout waiting for bottle to start")

        return

    # Do the actual functionality of the thing.
    inner_do_initialize()

    # Unlock the init lock
    bottle_init_unlock()


# Run inside bottle.
def do_shell():
    """Start a shell inside the bottle, initializing it if necessary."""

    if verbose:
        print("genie: starting shell")

    pre_systemd_action_checks(helpers.find_systemd())

    sdp = helpers.find_systemd()

    if sdp == 1:
        # we're already inside the bottle
        sys.exit("genie: already inside the bottle; cannot proceed")

    # At this point, we should be outside a bottle, one way or another.
    # Get the bottle namespace
    with nsenter.Namespace(sdp, 'pid'):
        subprocess.run("machinectl shell -q " + login + "@.host", shell=True)


def do_login():
    """Start a login prompt inside the bottle, initializing it if necessary."""

    if verbose:
        print("genie: starting login prompt")

    pre_systemd_action_checks(helpers.find_systemd())

    sdp = helpers.find_systemd()

    if sdp == 1:
        # we're already inside the bottle
        sys.exit("genie: already inside the bottle; cannot proceed")

    # At this point, we should be outside a bottle, one way or another.
    # Get the bottle namespace
    with nsenter.Namespace(sdp, 'pid'):
        subprocess.run("machinectl login .host", shell=True)


def do_command(commandline):
    """Run a command in a user session inside the bottle, initializing it if necessary."""

    if verbose:
        print("genie: running command " + ' '.join(commandline))

    if len(commandline) == 0:
        sys.exit("genie: no command specified")

    sdp = helpers.find_systemd()

    if sdp == 1:
        # we're already inside the bottle
        ic = subprocess.run(commandline)
        return ic.returncode

    pre_systemd_action_checks(sdp)

    sdp = helpers.find_systemd()

    command = ["machinectl", "shell", "-q", login + "@.host",
               "/usr/lib/genie/runinwsl", os.getcwd()] + commandline

    with nsenter.Namespace(sdp, 'pid'):
        sp = subprocess.run(command)
        return sp


# Shut down bottle.
def do_shutdown():
    """Shutdown the genie bottle and clean up."""
    sdp = helpers.find_systemd()

    if sdp == 0:
        sys.exit("genie: no bottle exists")

    if sdp == 1:
        sys.exit("genie: cannot shut down bottle from inside bottle")

    state = helpers.get_systemd_state(sdp)

    if 'starting' in state or 'stopping' in state:
        sys.exit(
            f"genie: bottle is currently {state}; please wait until it is in a stable state")

    if verbose:
        print("genie: running systemctl poweroff within bottle")

    with nsenter.Namespace(sdp, 'pid'):
        subprocess.run(["systemctl", "poweroff"])

    # Wait for systemd to exit.
    print("Waiting for systemd to exit...", end="", flush=True)

    timeout = configuration.system_timeout()

    while helpers.find_systemd() != 0 and timeout > 0:
        time.sleep(1)
        print(".", end="", flush=True)

        timeout -= 1

    print("")

    if (timeout <= 0):
        print(
            f"genie: systemd did not exit after {configuration.system_timeout()} seconds")
        print("genie: this may be due to a problem with your systemd configuration")
        print("genie: attempting to continue")

    # Reverse the processes we performed to prepare the bottle as the post-shutdown
    # cleanup, only in reverse.
    if apparmor.exists():
        apparmor.unconfigure(verbose)

    binfmts.mount(verbose)

    # If configured to, remove the resolv.conf symlink for systemd-resolved.
    if configuration.resolved_stub():
        resolved.unconfigure(verbose)

    if configuration.update_hostname():
        host.restore(verbose)


# Status checks.
def do_is_running():
    """Display whether the bottle is running or not."""
    sdp = helpers.find_systemd()

    if sdp == 0:
        print("stopped")
        sys.exit(1)

    state = helpers.get_systemd_state(sdp)

    if 'running' in state:
        print("running")
        sys.exit(0)

    if 'initializing' in state or 'starting' in state:
        print("starting")
        sys.exit(2)

    if 'stopping' in state:
        print("stopping")
        sys.exit(3)

    if 'degraded' in state:
        print("running (systemd errors)")
        sys.exit(4)

    print(f"unknown {state}")
    sys.exit(5)


def do_is_in_bottle():
    """Display whether we are currently executing within or without the genie bottle."""
    sdp = helpers.find_systemd()

    if sdp == 1:
        print("inside")
        sys.exit(0)

    if sdp == 0:
        print("no-bottle")
        sys.exit(2)

    print("outside")
    sys.exit(1)


# Entrypoint
def entrypoint():
    """Entrypoint of the application."""
    global verbose
    global login

    helpers.prelaunch_checks()
    configuration.load()
    arguments = parse_command_line()

    # Set globals
    verbose = arguments.verbose
    login = helpers.get_login_session_user()

    # Check user
    if arguments.user is not None:

        # Abort if user specified and not -c or -s
        if not (arguments.shell or (arguments.command is not None)):
            sys.exit(
                "genie: error: argument -a/--as-user can only be used with -c/--command or -s/--shell")

        # Check if arguments.user is a real user
        helpers.validate_is_real_user(arguments.user)

        login = arguments.user

        if verbose:
            print(f"genie: executing as user {login}")

    # Decide what to do.
    if arguments.parser_test:
        do_parser_test(arguments)
    elif arguments.initialize:
        do_initialize()
    elif arguments.shell:
        do_shell()
    elif arguments.login:
        do_login()
    elif arguments.command is not None:
        do_command(arguments.command)
    elif arguments.shutdown:
        do_shutdown()
    elif arguments.is_running:
        do_is_running()
    elif arguments.is_in_bottle:
        do_is_in_bottle()
    else:
        sys.exit("genie: impossible argument - how did we get here?")


entrypoint()

# End of file
