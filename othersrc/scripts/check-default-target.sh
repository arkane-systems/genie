#! /bin/sh
if [ $(basename $(readlink -f /etc/systemd/system/default.target)) != "multi-user.target" ];
then
  echo WARNING: the systemd default target is not set to multi-user.target;
  echo          this is not supported and may cause issues.
  echo          If you are sure you wish to use another target,
  echo          this warning can be disabled in the config file.
fi
