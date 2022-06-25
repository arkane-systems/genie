# Configuration data module

import configparser

# Global variables

_config = None


# functions
def clonable_envars():
    """Get the list of environment variables to clone."""
    return (_config.get('genie', 'clone-env',
                        fallback='WSL_DISTRO_NAME,WSL_INTEROP,WSLENV,DISPLAY,WAYLAND_DISPLAY,PULSE_SERVER')).split(',')


def clone_path():
    """Do we clone the outside-bottle path, or not?"""
    return _config.getboolean('genie', 'clone-path', fallback=False)


def resolved_stub():
    """Do we make the systemd-resolved stub, or not?"""
    return _config.getboolean('genie', 'resolved-stub', fallback=False)


def secure_path():
    """Get the configured secure path."""
    return _config.get('genie', 'secure-path', fallback='/lib/systemd:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin')


def system_timeout():
    """Return the configured timeout for systemd startup."""
    return _config.getint('genie', 'systemd-timeout', fallback=240)


def target_warning():
    """Warn when the systemd target is not set to 'multi-user.target'."""
    return _config.getboolean('genie', 'target-warning', fallback=True)


def update_hostname():
    """Update the hostname in the config file?"""
    return _config.getboolean('genie', 'update-hostname', fallback=True)


def update_hostname_suffix():
    """Update the hostname suffix in the config file with..."""
    return _config.get('genie', 'update-hostname-suffix', fallback='-wsl')


# Initialization

def load():
    """Load the configuration from the config file ('/etc/genie.ini')."""
    global _config

    _config = configparser.ConfigParser()
    _config.read('/etc/genie.ini')
