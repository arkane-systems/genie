#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the executable
#

all: build-amd64 build-arm64

build-amd64:
	gcc genie.c -o genie

build-arm64:
	aarch64-linux-gnu-gcc genie.c -o genie-arm64

#
# clean: delete the package interim files
#

clean:
	rm -f genie
	rm -f genie-arm64
