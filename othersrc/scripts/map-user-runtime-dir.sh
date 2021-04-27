#!/bin/sh
if [! -d /mnt/wslg/runtime-dir]
then
  # WSLg is not present, so default to normal behavior
  /lib/systemd/systemd-user-runtime-dir start %1
  exit
fi

WUID=$(stat -c "%u" /mnt/wslg/runtime-dir)

if [%1 == $WUID]
then
  # We are the WSLg user, so map the runtime-dir
  mount --bind /mnt/wslg/runtime-dir /run/user/%1
  exit
fi

# If we get here, we are not the WSLg user
/lib/systemd/systemd-user-runtime-dir start %1
