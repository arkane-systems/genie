#!/bin/sh
if [! -d /mnt/wslg/runtime-dir]
then
  # WSLg is not present, so default to normal behavior
  /lib/systemd/systemd-user-runtime-dir stop %1
  exit
fi

WUID=$(stat -c "%u" /mnt/wslg/runtime-dir)

if [%1 == $WUID]
then
  # We are the WSLg user, so unmap the runtime-dir
  umount /run/user/%1
  exit
fi

# If we get here, we are not the WSLg user
/lib/systemd/systemd-user-runtime-dir stop %1
