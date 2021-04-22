#!/bin/sh
if [ -e /mnt/wslg/.X11-unix ]
then
  mount --bind /mnt/wslg/.X11-unix /tmp/.X11-unix
else
  echo "genie: could not find WSLg X socket"
fi
