#! /usr/bin/env python3

import argparse
import configparser
import fcntl
import os
import shutil
import subprocess
import sys
import time

import nsenter
import psutil
from python_hosts import Hosts, HostsEntry

### Global variables
version = "2.0"

verbose = False
login = None
config = None

lockfile_fp = None

### Helper functions

def apparmor_configure():
    """Configure an AppArmor namespace for the genie bottle."""
    
    # If the AppArmor filesystem is not mounted, mount it.
    if not os.path.exists ('/sys/kernel/security/apparmor'):
        if verbose:
            print ("genie: mounting AppArmor filesystem")

        sp = subprocess.run (['mount', '-t', 'securityfs', 'securityfs', '/sys/kernel/security'])
        if sp.returncode != 0:
            print ("genie: failed to mount AppArmor filesystem; attempting to continue without AppArmor")
            return None

    # Create AppArmor namespace for genie bottle.
    nsName = 'genie-' + get_wsl_distro_name()

    if verbose:
        print (f"genie: creating AppArmor namespace '{nsName}'")
    
    if not os.path.exists ('/sys/kernel/security/apparmor/policy/namespaces'):
        print ("genie: could not find AppArmor filesystem; attempting to continue without AppArmor")
        return None

    os.mkdir ('/sys/kernel/security/apparmor/policy/namespaces/' + nsName)
    
    return nsName

def apparmor_unconfigure():
    """Clean up the AppArmor namespace when the bottle is stopped."""

    nsName = 'genie-' + get_wsl_distro_name()

    if verbose:
        print (f"genie: deleting AppArmor namespace '{nsName}'")

    if os.path.exists ('/sys/kernel/security/apparmor/policy/namespaces/' + nsName):
        try:
            os.rmdir ('/sys/kernel/security/apparmor/policy/namespaces/' + nsName)
        except OSError as e:
            print (f"genie: failed to delete AppArmor namespace; attempting to continue; {e.strerror}" + e.strerror)
    else:
        if verbose:
            print ("genie: no AppArmor namespace to delete")

def binfmts_umount():
    """Unmount the binfmts filesystem, if it is mounted."""
    if os.path.exists ('/proc/sys/fs/binfmt_misc'):

        if verbose:
            print ("genie: unmounting binfmt_misc filesystem before proceeding")

        sp = subprocess.run (['umount', '/proc/sys/fs/binfmt_misc'])

        if sp.returncode != 0:
            print ("genie: failed to unmount binfmt_misc filesystem; attempting to continue")
        
    else:
        if verbose:
            print ("no binfmt_misc filesystem present")

def binfmts_mount():
    """Mount the binfmt_misc filesystem, if it is not mounted."""

    # Having unmounted the binfmts fs before starting systemd, we remount it as
    # a courtesy. But remember, genie is not guaranteed to be idempotent, so don't
    # rely on this, for the love of Thompson and Ritchie!

    if not os.path.exists ('/proc/sys/fs/binfmt_misc'):

        if verbose:
            print ("genie: remounting binfmt_misc filesystem as a courtesy")

        sp = subprocess.run (['mount', '-t', 'binfmt_misc', 'binfmt_misc', '/proc/sys/fs/binfmt_misc'])

        if sp.returncode != 0:
            print ("genie: failed to remount binfmt_misc filesystem; attempting to continue")

def bottle_init_lock():
    """Lock the bottle init process to one instance only."""
    global lockfile_fp

    lockfile_fp = open ('/run/genie.init.lock', 'a')

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
    fcntl.lockf (lockfile_fp, fcntl.LOCK_UN)
    lockfile_fp.close ()

    os.remove ("/run/genie.init.lock")

def config_clone_path():
    """Do we clone the outside-bottle path, or not?"""
    return config.getboolean ('genie', 'clone-path', fallback=False)

def config_resolved_stub():
    """Do we make the systemd-resolved stub, or not?"""
    return config.getboolean ('genie', 'resolved-stub', fallback=False)

def config_secure_path():
    """Get the configured secure path."""
    return config.get ('genie', 'secure-path', fallback='/lib/systemd:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin')

def config_system_timeout():
    """Return the configured timeout for systemd startup."""
    return config.getint ('genie', 'systemd-timeout', fallback=240)

def config_target_warning():
    """Warn when the systemd target is not set to 'multi-user.target'."""
    return config.getboolean ('genie', 'target-warning', fallback=True)

def config_update_hostname():
    """Update the hostname in the config file."""
    return config.getboolean ('genie', 'update-hostname', fallback=True)

def config_update_hostname_suffix():
    """Update the hostname suffix in the config file."""
    return config.get ('genie', 'update-hostname-suffix', fallback='-wsl')

def find_systemd():
    """Find the running systemd process and return its pid."""
    for proc in psutil.process_iter():
        if "systemd" in proc.name():
            return proc.pid

    return 0

def get_systemd_state(sdp):
    """Get the systemd state, whether we are within or without the bottle."""

    if sdp == 0:
        return "offline"

    with nsenter.Namespace (sdp, 'pid'):
        sc = subprocess.run (["systemctl", "is-system-running"], capture_output=True, text=True)
        return sc.stdout.rstrip()

def get_systemd_target():
    """Get the default systemd target."""
    return os.path.basename (os.path.realpath ('/etc/systemd/system/default.target'))

def get_unshare_path():
    """Find the path to the unshare utility."""
    return shutil.which ('unshare')

def get_wsl_distro_name():
    """Get the WSL distribution name."""
    return os.environ['WSL_DISTRO_NAME']

def hostname_update():
    """Update the hostname and mount over previous hostname."""
    if verbose:
        print ("genie: generating new hostname")

    external_hostname = os.uname().nodename

    if verbose:
        print (f"genie: external hostname is {external_hostname}")

    internal_hostname = external_hostname + config_update_hostname_suffix()

    # Create the hostname file
    with open ('/run/genie.hostname', 'w') as hostfile:
        print (internal_hostname, file=hostfile)
        hostfile.close()

    os.chmod ('/run/genie.hostname', 0o644)

    # Mount the hostname file
    if verbose:
        print (f"genie: setting new hostname to {internal_hostname}")

    sp = subprocess.run (['mount', '--bind', '/run/genie.hostname', '/etc/hostname'])
    
    if sp.returncode != 0:
        print ("genie: failed to bind hostname file; attempting to continue")
        return

    if verbose:
        print ("genie: updating hosts file")

    # Update hosts file (remove old hostname, add new hostname)
    modify_hosts_file_entries(external_hostname, internal_hostname)

def hostname_restore():
    """Restore the hostname."""
    internal_hostname = os.uname().nodename

    if verbose:
        print ("genie: dropping in-bottle hostname");
    
    # Drop the in-bottle hostname mount
    sp = subprocess.run (['umount', '/etc/hostname'])

    if sp.returncode != 0:
        print ("genie: failed to unmount hostname file; attempting to continue")
        return

    # Remove the hostname file
    os.remove ('/run/genie.hostname')

    # Reset hostname
    subprocess.run (['hostname', '-F', '/etc/hostname'])

    external_hostname = os.uname().nodename

    # Update hosts file (remove new hostname, add old hostname)
    modify_hosts_file_entries(internal_hostname, external_hostname)

def load_configuration():
    """Load the configuration from the config file ('/etc/genie.ini')."""
    global config

    config = configparser.ConfigParser()
    config.read ('/etc/genie.ini')

def modify_hosts_file_entries(old_name, new_name):
    """Modify the hosts file to replace old_name with new_name."""
    hosts = Hosts()

    entries = hosts.find_all_matching (name = old_name)

    # Iterate through all relevant entries
    for e in entries:
        # Iterate through all names
        new_names = []
        for n in e.names:
            # Modify name
            new_names.append (n.replace (old_name, new_name))
        new_entry = HostsEntry (entry_type= e.entry_type, address = e.address, names = new_names, comment = e.comment)

        # Replace old entry
        hosts.add ([new_entry], force=True)

    hosts.write()


def parse_command_line():
    """Create the command-line option parser and parse arguments."""
    parser = argparse.ArgumentParser (
        description = "Handles transitions to the \"bottle\" namespace for systemd under WSL.",
        epilog = "For more information, see https://github.com/arkane-systems/genie/")

    # Version command
    parser.add_argument ('-V', '--version', action='version', version='%(prog)s ' + version)

    # Verbose option
    parser.add_argument ('-v', '--verbose', action='store_true', help="display verbose progress messages")

    # Commands
    group = parser.add_mutually_exclusive_group (required=True)

    group.add_argument('-i', '--initialize', action='store_true', help='initialize the bottle (if necessary) only')
    group.add_argument('-s', '--shell', action='store_true', help='initialize the bottle (if necessary), and run a shell in it')
    group.add_argument('-l', '--login', action='store_true', help='initialize the bottle (if necessary), and open a logon prompt in it')
    group.add_argument('-c', '--command', help='initialize the bottle (if necessary), and run the specified command in it', nargs=argparse.REMAINDER)
    group.add_argument('-u', '--shutdown', action='store_true', help='shut down systemd and exit the bottle')
    group.add_argument('-r', '--is-running', action='store_true', help='check whether systemd is running in genie, or not')
    group.add_argument('-b', '--is-in-bottle', action='store_true', help='check whether currently executing within the bottle, or not')

    return parser.parse_args()

def prelaunch_checks():
    """Check that we are on the correct platform, and as the correct user."""

    # Is this Linux?
    if not sys.platform.startswith('linux'):
        sys.exit("genie: not executing on the Linux platform - how did we get here?")

    # Is this WSL 1?
    root_type = list(filter(lambda x: x.mountpoint == '/', psutil.disk_partitions()))[0].fstype
    if root_type == 'lxfs' or root_type == 'wslfs':
        sys.exit("genie: systemd is not supported under WSL 1.")

    # Is this WSL 2?
    if not os.path.exists('/run/WSL'):
        if not 'microsoft' in os.uname():
            sys.exit("genie: not executing under WSL 2 - how did we get here?")

    # Are we effectively root?
    if os.geteuid() != 0:
        sys.exit("genie: must execute as root - has the setuid bit gone astray?")

def pre_systemd_action_checks(sdp):
    """Things to check before performing a systemd-requiring action."""

    if sdp == 0:
        # no bottle exists; this means we should recurse and start one
        initcommand = [ "genie", "-i" ]

        if verbose:
            initcommand.append ("-v")

        init = subprocess.run (initcommand)

        # continue when subprocess done
        if init.returncode != 0:
            sys.exit (f"could not initialise bottle, exit code = {init.returncode}")

        # Refresh systemd pid
        sdp = find_systemd()

    state = get_systemd_state(sdp)

    if 'stopping' in state:
        sys.exit ("genie: systemd is shutting down, cannot proceed")

    if 'initializing' in state or 'starting' in state:
        # wait for it
        print ("genie: systemd is starting up, please wait...", end="", flush=True)

        timeout = config_system_timeout()

        while (not 'running' in state) and timeout > 0:
            time.sleep (1)
            state = get_systemd_state (sdp)

            print (".", end="", flush=True)

        timeout -= 1

        print ("")

        if timeout <= 0:
            print ("genie: WARNING: timeout waiting for bottle to start")

    if 'degraded' in state:
        print ('genie: WARNING: systemd is in degraded state, issues may occur!')

    if not ('running' in state or 'degraded' in state):
        sys.exit ("genie: systemd in unsupported state '" + state + "'; cannot proceed")

def resolved_configure():
    """Replace resolv.conf with the required stub symlink for systemd-resolved."""
    # We cannot check if the target (/run/systemd/resolve/stub-resolv.conf) exists,
    # since it will not be created until after systemd-resolved starts up. So we're
    # going to have to live with that uncertainty.

    # Check if source file (/etc/resolv.conf) exists
    if os.path.lexists ('/etc/resolv.conf'):
        # If so, move it to the backup file (/etc/resolv.conf.wsl)
        if verbose:
            print ("genie: backing up /etc/resolv.conf to /etc/resolv.conf.wsl")

        if os.path.exists ('/etc/resolv.conf.wsl'):
            os.remove ('/etc/resolv.conf.wsl')

        os.rename ('/etc/resolv.conf', '/etc/resolv.conf.wsl')
    
    # Create symlink from /etc/resolv.conf to /run/systemd/resolve/stub-resolv.conf
    if verbose:
        print ("genie: creating symlink /etc/resolv.conf -> /run/systemd/resolve/stub-resolv.conf")

    os.symlink ('/run/systemd/resolve/stub-resolv.conf', '/etc/resolv.conf')

def resolved_unconfigure():
    """Restore original resolv.conf."""
    # Check if /etc/resolv.conf exists, and if so, if it is a symlink
    if os.path.exists ('/etc/resolv.conf'):
        if os.path.islink ('/etc/resolv.conf'):
            # If so, remove it
            if verbose:
                print ("genie: removing symlink /etc/resolv.conf")

            os.remove ('/etc/resolv.conf')

            # Check if /etc/resolv.conf.wsl exists
            if os.path.exists ('/etc/resolv.conf.wsl'):
                # If so, move it to /etc/resolv.conf
                if verbose:
                    print ("genie: restoring /etc/resolv.conf from /etc/resolv.conf.wsl")
                
                os.rename ('/etc/resolv.conf.wsl', '/etc/resolv.conf')
            else:
                # If not, warn the user
                print ("genie: WARNING: /etc/resolv.conf.wsl does not exist; please restore /etc/resolv.conf manually")

        else:
            print ("genie: WARNING: /etc/resolv.conf is not a symlink")

    else:
        print ("genie: WARNING: /etc/resolv.conf does not exist")

def set_secure_path():
    """Set up secure path, saving original if specified."""
    # Default original path.
    # TODO: Should reference system drive by letter
    originalPath = '/mnt/c/Windows/System32'

    if config_clone_path():
        originalPath = os.environ['PATH']

    # Set the local path
    os.environ['PATH'] = config_secure_path()

    # Create the path file
    with open ('/run/genie.path', 'w') as pathfile:
        print (originalPath, file=pathfile)
        pathfile.close()

def stash_environment():
    """Save a copy of the original environment (specified variables only)."""
    # Get variables to stash
    names = (config.get ('genie', 'clone-env', fallback='WSL_DISTRO_NAME,WSL_INTEROP,WSLENV,DISPLAY,WAYLAND_DISPLAY,PULSE_SERVER')).split(',')

    # Do the stashing.
    with open ('/run/genie.env', 'w') as envfile:

        # Start with the flag variable that's always added.
        print ("INSIDE_GENIE=yes", file=envfile)
        
        for n in names:
            if n in os.environ:
                print (f"{n}={os.environ[n]}", file=envfile)

        envfile.close()

### Commands

def do_initialize():
    """Initialize the genie bottle."""
    if verbose:
        print ("genie: starting bottle")

    # Secure the bottle init lock
    running = bottle_init_lock()
    if running:
        # Wait for other process to have started the bottle
        # The last step is the pid file being created, so we wait
        # for that, then return.
        if verbose:
            print (f"genie: already initializing, pid={running}, waiting...", end="", flush=True)
        
        # Allow 10% startup margin
        timeout = config_system_timeout() * 1.1

        while not os.path.exists ('/run/genie.systemd.pid') and timeout > 0:
            time.sleep (1)
            print (".", end="", flush=True)
            timeout -= 1

        print ("")
    
        if timeout <= 0:
            print ("genie: WARNING: timeout waiting for bottle to start")

        return

    sdp = find_systemd()

    if sdp != 0:
        sys.exit ("genie: bottle is already established (systemd running)")

    # FIRST: As a first step in initing systemd, delete any old runtime pid file
    # if such exists.
    if os.path.exists ('/run/genie.systemd.pid'):
        os.remove ('/run/genie.systemd.pid')

    # Set secure path, and stash original environment.
    set_secure_path()
    stash_environment()

    # Check and warn if not multi-user.target.
    if config_target_warning():
        target = get_systemd_target()

        if target != 'multi-user.target':
            print (f"genie: WARNING: systemd default target is {target}; targets other than multi-user.target may not work")
            print ("genie: WARNING: if you wish to use a different target, this warning can be disabled in the config file")
            print ("genie: WARNING: if you experience problems, please change the target to multi-user.target")

    # Now that the WSL hostname can be set via .wslconfig, we're going to make changing
    # it automatically in genie an option, enable/disable in genie.ini. Defaults to on
    # for backwards compatibility and because not doing so when using bridged networking is
    # a Bad Idea.
    if config_update_hostname():
        hostname_update()

    # If configured to, create the resolv.conf symlink for systemd-resolved.
    if config_resolved_stub():
        resolved_configure()

    # Unmount the binfmts fs before starting systemd, so systemd can mount it
    # again with all the trimmings.
    binfmts_umount()

    # Define systemd startup chain.
    startupChain = ["daemonize", get_unshare_path(), "-fp", "--propagation", "shared", "--mount-proc", "--"]

    # Check whether AppArmor is available in the kernel.
    if os.path.exists('/sys/module/apparmor'):

        # If so, configure AppArmor.
        nsName = apparmor_configure()

        # Add AppArmor to the startup chain.
        if nsName is not None:
            startupChain = startupChain + ["aa-exec", "-n", nsName, "-p", "unconfined", "--"]

    else:
        if verbose:
            print ("genie: AppArmor not available in kernel; attempting to continue without AppArmor namespace");

    # Update startup chain with systemd command.
    startupChain.append ("systemd")

    # Run systemd in a container
    if verbose:
        print ("genie: starting systemd with command line: ")
        print (' '.join(startupChain))

    # This requires real UID/GID root as well as effective UID/GID root
    suid = os.getuid()
    sgid = os.getgid()

    os.setuid(0)
    os.setgid(0)

    subprocess.run (startupChain)

    os.setuid(suid)
    os.setgid(sgid)

    # Wait for systemd to be up (polling, sigh.)
    sdp = 0
    print ("Waiting for systemd...", end="", flush=True)

    while sdp == 0:
        time.sleep (0.5)
        sdp = find_systemd()

        print (".", end="", flush=True)

    # Wait for systemd to be in running state.
    state = 'initializing'
    timeout = config_system_timeout()

    while (not 'running' in state) and timeout > 0:
        time.sleep (1)
        state = get_systemd_state (sdp)

        print ("!", end="", flush=True)

        timeout -= 1

    print ("")

    if not 'running' in state:
        print (f"genie: systemd did not enter running state ({state}) after {config_system_timeout()} seconds")
        print ("genie: this may be due to a problem with your systemd configuration")
        print ("genie: information on problematic units is available at https://github.com/arkane-systems/genie/wiki/Systemd-units-known-to-be-problematic-under-WSL")
        print ("genie: a list of failed units follows:\n")

        with nsenter.Namespace (sdp, 'pid'):
            subprocess.run(["systemctl", "--failed"])

    # LAST: Now that systemd exists, write out its (external) pid.
    # We do not need to store the inside-bottle pid anywhere for obvious reasons.
    with open ('/run/genie.systemd.pid', 'w') as pidfile:
        print (sdp, file=pidfile)
        pidfile.close()

    # Unlock the init lock
    bottle_init_unlock()

## Run inside bottle.
def do_shell():
    """Start a shell inside the bottle, initializing it if necessary."""

    if verbose:
        print ("genie: starting shell")

    pre_systemd_action_checks(find_systemd())

    sdp = find_systemd()

    if sdp == 1:
        # we're already inside the bottle
        sys.exit("genie: already inside the bottle; cannot proceed")

    # At this point, we should be outside a bottle, one way or another.
    # Get the bottle namespace
    with nsenter.Namespace (sdp, 'pid'):
        subprocess.run( "machinectl shell -q " + login + "@.host", shell=True)

def do_login():
    """Start a login prompt inside the bottle, initializing it if necessary."""

    if verbose:
        print ("genie: starting login prompt")

    pre_systemd_action_checks(find_systemd())

    sdp = find_systemd()

    if sdp == 1:
        # we're already inside the bottle
        sys.exit("genie: already inside the bottle; cannot proceed")

    # At this point, we should be outside a bottle, one way or another.
    # Get the bottle namespace
    with nsenter.Namespace (sdp, 'pid'):
        subprocess.run ("machinectl login .host", shell=True)

def do_command(commandline):
    """Run a command in a user session inside the bottle, initializing it if necessary."""

    if verbose:
        print ("genie: running command " + ' '.join(commandline))

    sdp = find_systemd()

    if sdp == 1:
        # we're already inside the bottle
        ic = subprocess.run (commandline)
        return ic.returncode

    pre_systemd_action_checks(sdp)

    sdp = find_systemd()

    command = ["machinectl", "shell", "-q", login + "@.host", "/usr/lib/genie/runinwsl", os.getcwd()] + commandline

    with nsenter.Namespace (sdp, 'pid'):
        sp = subprocess.run (' '.join(command), shell=True)
        return sp

def do_shutdown():
    """Shutdown the genie bottle and clean up."""
    sdp = find_systemd()

    if sdp == 0:
        sys.exit ("genie: no bottle exists")

    if sdp == 1:
        sys.exit ("genie: cannot shut down bottle from inside bottle")

    state = get_systemd_state (sdp)

    if 'starting' in state or 'stopping' in state:
        sys.exit (f"genie: bottle is currently {state}; please wait until it is in a stable state")

    if verbose:
        print ("genie: running systemctl poweroff within bottle")

    with nsenter.Namespace (sdp, 'pid'):
        subprocess.run (["systemctl", "poweroff"])

    # Wait for systemd to exit.
    print ("Waiting for systemd to exit...", end="", flush=True)

    timeout = config_system_timeout()

    while find_systemd() != 0 and timeout > 0:
        time.sleep (1)
        print (".", end="", flush=True)

        timeout -= 1

    print ("")

    if (timeout <= 0):
        print ("genie: systemd did not exit after {config_system_timeout()} seconds")
        print ("genie: this may be due to a problem with your systemd configuration")
        print ("genie: attempting to continue")

    # Reverse the processes we performed to prepare the bottle as the post-shutdown
    # cleanup, only in reverse.
    if os.path.exists('/sys/module/apparmor'):
        apparmor_unconfigure()

    binfmts_mount()

    if config_update_hostname():
        hostname_restore()    

def do_is_running():
    """Display whether the bottle is running or not."""
    sdp = find_systemd()

    if sdp == 0:
        print ("stopped")
        return 1;

    state = get_systemd_state(sdp)

    if 'running' in state:
        print ("running")
        return 0

    if 'initializing' in state or 'starting' in state:
        print ("starting")
        return 2

    if 'stopping' in state:
        print ("stopping")
        return 3

    if 'degraded' in state:
        print ("running (systemd errors)")
        return 4

    print (f"unknown {state}")
    return 5

def do_is_in_bottle():
    """Display whether we are currently executing within or without the genie bottle."""
    sdp = find_systemd()

    if sdp == 1:
        print ("inside")
        return 0

    if sdp == 0:
        print ("no-bottle")
        return 2

    print ("outside")
    return 1

### Entrypoint
def entrypoint():
    """Entrypoint of the application."""
    global verbose
    global login

    prelaunch_checks()
    load_configuration()
    arguments = parse_command_line()

    ## Set globals
    verbose = arguments.verbose
    login = os.environ["LOGNAME"]

    ## Decide what to do.
    if arguments.initialize:
        do_initialize()
    elif arguments.shell:
        do_shell()
    elif arguments.login:
        do_login()
    elif not arguments.command is None:
        do_command(arguments.command)
    elif arguments.shutdown:
        do_shutdown()
    elif arguments.is_running:
        do_is_running()
    elif arguments.is_in_bottle:
        do_is_in_bottle()
    else:
        sys.exit ("genie: impossible argument - how did we get here?")

entrypoint()

### End of file
