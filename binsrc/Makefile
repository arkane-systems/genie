#
# This Makefile produces all the binary images.
#

#
# default target: build both for installation
#
build: build-wrapper build-runinwsl build-genie

build-wrapper:
	mkdir -p out
	make -C genie-wrapper

build-runinwsl:
	mkdir -p out
	python3 -m zipapp -o out/runinwsl -p "/usr/bin/env python3" -c runinwsl

build-genie:
	mkdir -p out
	python3 -m pip install -r genie/requirements.txt --target genie --upgrade
	python3 -m zipapp -o out/genie -p "/usr/bin/env python3" -c genie

#
# clean: clean up after a build/package
#
clean: clean-wrapper clean-runinwsl clean-genie

clean-wrapper:
	make -C genie-wrapper clean

clean-runinwsl:
	rm -f out/runinwsl

clean-genie:
	rm -f out/genie
