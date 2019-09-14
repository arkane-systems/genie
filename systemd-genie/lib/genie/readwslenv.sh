#!/bin/sh
# Import the environment from the saved WSL environment to the shell.
if [ -e /run/genie.env ]
then
  export $(cat /run/genie.env | xargs)
fi
