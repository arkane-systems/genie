#!/bin/sh
if [ -e /run/genie.env ]
then
  cat /run/genie.env
fi

if [ -e /run/genie.path ]
then
  PATH=$PATH:$(cat /run/genie.path)
  echo PATH="$(echo $PATH | awk -v RS=: '!($0 in a) {a[$0]; printf("%s%s", length(a) > 1 ? ":" : "", $0)}')"
fi
