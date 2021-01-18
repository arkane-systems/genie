#
# This Makefile produces and packages the genie-systemd runinwsl auxiliary program.
#

#
# default target: build the executable
#

all:
	dotnet publish -c Release -r linux-x64

#
# local: build for /usr/local
#

local:
	dotnet publish -c ReleaseLocal -r linux-x64

#
# clean: delete the package interim files
#

clean:
	rm -rf bin
	rm -rf obj
