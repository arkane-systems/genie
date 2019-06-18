#!/usr/bin/python
# This program handles transitions to and from the "bottle" namespace for systemd under WSL.

import argparse
import os
import psutil
import pwd
import subprocess
import sys
import time

# Globals

args = None
realuid = 0

# Get systemd pid, if any.
def get_systemd_pid():
  systemdpid = 0

  # Since we iterate in pid order, the systemd picked up by this should be the oldest; i.e., the system systemd.
  # ...unless genie was first run well into the life of a WSL session and the pids overflow shortly thereafter,
  # but we'll worry about that case later. Much later.
  for proc in psutil.process_iter(attrs=['pid', 'name']):
    if (proc.info['name'] == 'systemd'):
      systemdpid = proc.info['pid']

  return systemdpid

# Initialize bottle.

def bottle_init():
  if args.verbose:
    print ('genie: initializing bottle')

  # Run systemd in a container.
  subprocess.check_call (["/usr/sbin/daemonize", "/usr/bin/unshare", "-fp", "--mount-proc", "/lib/systemd/systemd"])

  # Wait for systemd to be up
  systemdpid = 0

  # This is a lousy fix for a race condition, but it'll do for now.
  time.sleep(1)

  while 0 == systemdpid:
    systemdpid = get_systemd_pid()
    time.sleep(0.5)

  return systemdpid

# Rootify and unrootify.

def rootify():
  global realuid
  realuid = os.getuid()
  os.setuid(0)

def unrootify():
  global realuid
  os.setuid(realuid)

# Start shell.

def shell_start(systemdpid, username):

  if args.verbose:
    print ('genie: starting shell')

  subprocess.call (["/usr/bin/nsenter", "-t", str(systemdpid), "-m", "-p", "/bin/login", "-f", username])

# Run command.

def command_start(systemdpid, userid, command):

  if args.verbose:
    print ('genie: running command')

  return subprocess.call (["/usr/bin/nsenter", "-t", str(systemdpid), "-S", str(userid), "-m", "-p"] + command)

# Main body.

def main():

  # Check to make sure that we are root.
  if (os.geteuid() != 0):
    print ('genie: must execute as root - has the setuid bit gone astray?')
    sys.exit (os.EX_NOPERM)

  # Parse arguments.
  parser = argparse.ArgumentParser(description='Handles transitions to the "bottle" namespace for systemd under WSL.')
  parser.add_argument('-v', '--verbose', dest='verbose', action='store_true', help='display verbose progress messages')
  parser.add_argument('-V', '--version', action='version', version='%(prog)s 1.1')

  group = parser.add_mutually_exclusive_group(required=True)
  group.add_argument('-i', '--initialize', dest='initialize', action='store_true', help='initialize the bottle (if necessary) only')
  group.add_argument('-s', '--shell', dest='shell', action='store_true', help='initialize the bottle (if necessary) and run a shell in it')
  group.add_argument('-c', '--command', dest='command', help='initialize the bottle (if necessary), and run the specified command in it', nargs='+')

  global args
  args = parser.parse_args()

  ## Establish current status.
  # Store the name of the real user.
  userid = os.getuid()
  username = pwd.getpwuid(userid)[0]

  # Check if the bottle exists: which we do by checking if a systemd process exists.
  systemdpid = get_systemd_pid()
  bottle = True
  inside = False

  if (systemdpid == 0):
    bottle = False
    if (args.verbose):
      print ('genie: no bottle present')
  elif (systemdpid == 1):
    inside = True
    if (args.verbose):
      print ('genie: inside bottle')
  else:
    if (args.verbose):
      print ('genie: outside bottle')

  ## Do the needful.

  if args.initialize:
    # Initialize the bottle (if necessary) only

    # If a bottle exists, we have succeeded already. Exit and report success.
    if bottle:
      if args.verbose:
        print ('genie: bottle already exists (no need to initialize)')
      return

    rootify()
    bottle_init()
    unrootify()

  elif args.shell:
    # Initialize the bottle (if necessary), and then start a shell in it

    if inside:
      print ('genie: already inside the bottle; cannot start shell')
      sys.exit(os.EX_USAGE)

    rootify()

    if not bottle:
      systemdpid = bottle_init()

    # We must at this point be outside an existing bottle, one way or another.

    # It shouldn't matter whether we have setuid here, since we start the shell with login,
    # which expects root and reassigns uid appropriately.
    shell_start (systemdpid, username)

    unrootify()
    sys.exit(os.EX_OK)

  elif args.command:
    # Initialize the bottle (if necessary), and then execute a command in it

    if inside:
      # Inside, so just execute it.
      ret = subprocess.call (args.command)
      sys.exit(ret)

    rootify()

    if not bottle:
      systemdpid = bottle_init()

    # We must at this point be outside an existing bottle, one way or another.
    ret = command_start (systemdpid, userid, args.command)

    sys.exit(ret)

if __name__ == "__main__":
  main()
