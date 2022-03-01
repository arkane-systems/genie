#
# This Makefile produces all the binary images.
#

#
# default target: build both for installation
#
build: build-wrapper build-runinwsl build-genie

build-wrapper:
	make -C genie-wrapper

build-runinwsl:
	python -m zipapp -o out/runinwsl -p "/usr/bin/env python3" -c runinwsl

build-genie:
	python -m pip install -r genie/requirements.txt --target genie
	python -m zipapp -o out/genie -p "/usr/bin/env python3" -c genie

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
