# Import the environment from the saved WSL environment to the shell.
if [ -e /run/genie.env ]
then
  export INSIDE_GENIE=true
  export $(cat /run/genie.env | xargs)
fi
