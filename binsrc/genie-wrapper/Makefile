#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the executable
#

all:
ifeq ($(DEB_TARGET_ARCH),amd64)
	gcc genie.c -o genie
endif

ifeq ($(DEB_TARGET_ARCH),arm64)
	aarch64-linux-gnu-gcc genie.c -o genie
endif

#
# clean: delete the package interim files
#

clean:
	rm -f genie
