#
# This Makefile builds and packages genie by invoking relevant sub-makefiles.
#

#
# Default target: list options
#

default:
	# Build options include:
	#
	# Build binaries only.
	# 
	# make build-binaries
	#
	# Package (native)
	#
	# make package
	# make package-debian
	# ? make package-tar
	#
	# Clean up
	#
	# make clean (does not clean altpacking by default)
	# make clean-debian
	# ? make clean-tar

#
# Targets: individual end-product build.
#

clean: clean-debian
	make -C binsrc clean
	rm -rf out

package: package-debian

#
# Debian packaging
#

# Debian installation locations

DESTDIR=debian/systemd-genie

INSTALLDIR = $(DESTDIR)/usr/lib/genie
BINDIR = $(DESTDIR)/usr/bin
ETCDIR = $(DESTDIR)/etc
SVCDIR = $(DESTDIR)/usr/lib/systemd/system
USRLIBDIR = $(DESTDIR)/usr/lib

package-debian: make-output-directory
	mkdir -p out/debian
	debuild
	mv ../systemd-genie_* out/debian

clean-debian:
	debuild -- clean

# Debian internal functions

internal-debian-package:

	# Binaries.
	mkdir -p "$(BINDIR)"
	install -Dm 6755 -o root "binsrc/genie-wrapper/genie" -t "$(BINDIR)"
	install -Dm 0755 -o root "binsrc/genie/genie" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "binsrc/genie/runinwsl" -t "$(INSTALLDIR)"

	# Requirements
	install -Dm 0644 -o root "binsrc/genie/requirements.txt" -t "$(INSTALLDIR)"

	# Environment generator.
	install -Dm 0755 -o root "othersrc/scripts/80-genie-envar.sh" -t "$(INSTALLDIR)"

	# Runtime dir mapping
	install -Dm 0755 -o root "othersrc/scripts/map-user-runtime-dir.sh" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "othersrc/scripts/unmap-user-runtime-dir.sh" -t "$(INSTALLDIR)"

	# Configuration file.
	install -Dm 0644 -o root "othersrc/etc/genie.ini" -t "$(ETCDIR)"

	# Unit files.
	install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.service" -t "$(SVCDIR)"
	install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.socket" -t "$(SVCDIR)"

	install -Dm 0644 -o root "othersrc/lib-systemd-system/user-runtime-dir@.service.d/override.conf" -t "$(SVCDIR)/user-runtime-dir@.service.d"

	# binfmt.d
	install -Dm 0644 -o root "othersrc/usr-lib/binfmt.d/WSLInterop.conf" -t "$(USRLIBDIR)/binfmt.d"

internal-debian-clean:
	make -C binsrc clean

#
# Helpers: intermediate build stages.
#

make-output-directory:
	mkdir -p out

build-binaries:
	make -C binsrc

