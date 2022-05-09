# Helper functions module

import os
import shutil
import subprocess
import sys

import nsenter
import psutil


def find_systemd():
    """Find the running systemd process and return its pid."""
    for proc in psutil.process_iter():
        if "systemd" in proc.name():
            return proc.pid

    return 0


def get_login_session_user():
    """Get the user logged into the current session, pre-setuid."""
    # This environment variable is set by the setuid wrapper.
    return os.environ["GENIE_LOGNAME"]


def get_systemd_state(sdp):
    """Get the systemd state, whether we are within or without the bottle."""

    if sdp == 0:
        return "offline"

    with nsenter.Namespace(sdp, 'pid'):
        sc = subprocess.run(["systemctl", "is-system-running"],
                            capture_output=True, text=True)
        return sc.stdout.rstrip()


def get_systemd_target():
    """Get the default systemd target."""
    return os.path.basename(os.path.realpath('/etc/systemd/system/default.target'))


def get_unshare_path():
    """Find the path to the unshare utility."""
    return shutil.which('unshare')


def get_wsl_distro_name():
    """Get the WSL distribution name."""
    return os.environ['WSL_DISTRO_NAME']


def prelaunch_checks():
    """Check that we are on the correct platform, and as the correct user."""

    # Is this Linux?
    if not sys.platform.startswith('linux'):
        sys.exit("genie: not executing on the Linux platform - how did we get here?")

    # Is this WSL 1?
    root_type = list(filter(lambda x: x.mountpoint == '/',
                            psutil.disk_partitions(all=True)))[0].fstype
    if root_type == 'lxfs' or root_type == 'wslfs':
        sys.exit("genie: systemd is not supported under WSL 1.")

    # Is this WSL 2?
    if not os.path.exists('/run/WSL'):
        if 'microsoft' not in os.uname():
            sys.exit("genie: not executing under WSL 2 - how did we get here?")

    # Are we effectively root?
    if os.geteuid() != 0:
        sys.exit("genie: must execute as root - has the setuid bit gone astray?")
