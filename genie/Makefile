#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the executable
#

all:
	dotnet publish -c Release -r linux-x64 --self-contained false

#
# install: build for /usr/local
#

#
# clean: delete the package interim files
#

clean:
	rm -rf bin
	rm -rf obj

