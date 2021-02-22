#
# This Makefile produces all the binary images.
#

#
# default target: build both for installation
#
build:
	make -C genie
	make -C runinwsl

#
# build-local: build both for local use
#
build-local:
	make -C genie local
	make -C runinwsl

#
# clean: clean up after a build/package
#
clean:
	make -C genie clean
	make -C runinwsl clean
