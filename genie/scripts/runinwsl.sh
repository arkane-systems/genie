#!/bin/sh
# Import the environment from the saved WSL environment to the shell.
if [ -e /run/genie.env ]
then
  export $(cat /run/genie.env | xargs)
fi

if [ -e /run/genie.path ]
then
  PATH=$PATH:$(cat /run/genie.path)
  export PATH=$(echo $PATH | awk -v RS=: '!($0 in a) {a[$0]; printf("%s%s", length(a) > 1 ? ":" : "", $0)}')
fi

# Change to correct working directory.
cd $1
shift

# Run specified command.
exec $*
