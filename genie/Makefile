#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the executable
#

all:
	dotnet publish -c Release -r linux-x64 --self-contained false

#
# local: build for /usr/local
#

local:
	echo "Local build not yet implemented."

#
# clean: delete the package interim files
#

clean:
	rm -rf bin
	rm -rf obj

