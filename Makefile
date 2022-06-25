#
# This Makefile builds and packages genie by invoking relevant sub-makefiles.
#

# Genie version
GENIEVERSION = 2.4

# Determine this makefile's path.
# Be sure to place this BEFORE `include` directives, if any.
THIS_FILE := $(lastword $(MAKEFILE_LIST))

# The values of these variables depend upon DESTDIR, set in the recursive call to
# the internal-package target.
INSTALLDIR = $(DESTDIR)/usr/lib/genie
BINDIR = $(DESTDIR)/usr/bin
ETCDIR = $(DESTDIR)/etc
SVCDIR = $(DESTDIR)/usr/lib/systemd/system
USRLIBDIR = $(DESTDIR)/usr/lib

# used only by TAR installer
ENVGENDIR = $(DESTDIR)/usr/lib/systemd/system-environment-generators
USRENVGENDIR = $(DESTDIR)/usr/lib/systemd/user-environment-generators
MAN8DIR = $(DESTDIR)/usr/share/man/man8

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
	# Package
	#
	# make package
	# make package-debian
	# make package-debian-arm64
	# make package-tar
	# make package-arch (requires Arch packaging environment)
	# make package-fedora (requires Fedora packaging environment)
	#
	# Clean up
	#
	# make clean
	# make clean-debian
	# make clean-tar
	# make clean-arch
	# make clean-fedora (requires Fedora packaging environment)

#
# Targets: individual end-product build.
#

clean: clean-debian clean-tar clean-arch
	make -C binsrc clean
	rm -rf out

package: package-debian

#
# Debian packaging
#

package-debian: package-debian-amd64 package-debian-arm64

package-debian-amd64: make-output-directory
	mkdir -p out/debian
	debuild -us -uc
	mv ../systemd-genie_* out/debian

package-debian-arm64: make-output-directory
	mkdir -p out/debian
	debuild -aarm64 -us -uc
	mv ../systemd-genie_* out/debian

clean-debian:
	debuild -- clean

package-tar: make-output-directory build-binaries
	mkdir -p out/tar
	mkdir -p tarball

	fakeroot $(MAKE) -f $(THIS_FILE) DESTDIR=tarball internal-package

	# Do the things that TAR needs that debuild would otherwise do
	fakeroot $(MAKE) -f $(THIS_FILE) DESTDIR=tarball internal-supplement
	fakeroot $(MAKE) -f $(THIS_FILE) DESTDIR=tarball internal-tar

	mv genie-systemd-*-amd64.tar.gz out/tar

clean-tar:
	rm -rf tarball

package-arch:
	mkdir -p out/arch
	updpkgsums
	BUILDDIR=/tmp PKDEST=$(PWD)/out/arch fakeroot makepkg
	rm -rf $(PWD)/genie
	mv *.zst out/arch

clean-arch:
	rm -rf $(PWD)/genie
	rm -rf out/arch

package-fedora: genie_version := $(shell rpmspec -q --qf %{Version} --srpm genie.spec)

RPMBUILD_TARGET = $(shell uname --processor)
package-fedora:
	rpmdev-setuptree
	tar zcvf $(shell rpm --eval '%{_sourcedir}')/genie-${genie_version}.tar.gz * --dereference --transform='s/^/genie-${genie_version}\//'
	fakeroot rpmbuild --target $(RPMBUILD_TARGET) -ba -v genie.spec
	mkdir -p out/fedora
	mv $(shell rpm --eval '%{_rpmdir}')/*/genie* out/fedora

clean-fedora:
	rpmdev-wipetree
	rm -rf out/fedora

# Internal packaging functions

internal-debian-package:
	mkdir -p debian/systemd-genie
	@$(MAKE) -f $(THIS_FILE) DESTDIR=debian/systemd-genie internal-package

# We can assume DESTDIR is set, due to how the following are called.

internal-package:

	# Binaries.
	mkdir -p "$(BINDIR)"
	install -Dm 6755 -o root "binsrc/genie-wrapper/genie" -t "$(BINDIR)"
	install -Dm 0755 -o root "binsrc/out/genie" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "binsrc/out/runinwsl" -t "$(INSTALLDIR)"

	# Environment generator.
	install -Dm 0755 -o root "othersrc/scripts/80-genie-envar.sh" -t "$(INSTALLDIR)"

	# Runtime dir mapping
	install -Dm 0755 -o root "othersrc/scripts/map-user-runtime-dir.sh" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "othersrc/scripts/unmap-user-runtime-dir.sh" -t "$(INSTALLDIR)"

	# Configuration file.
	install -Dm 0644 -o root "othersrc/etc/genie.ini" -t "$(ETCDIR)"

	# Unit files.
	install -Dm 0644 -o root "othersrc/lib-systemd-system/user-runtime-dir@.service.d/override.conf" -t "$(SVCDIR)/user-runtime-dir@.service.d"

	# binfmt.d
	install -Dm 0644 -o root "othersrc/usr-lib/binfmt.d/WSLInterop.conf" -t "$(USRLIBDIR)/binfmt.d"

	# tmpfiles.d
	install -Dm 0644 -o root "othersrc/usr-lib/tmpfiles.d/wslg.conf" -t "$(USRLIBDIR)/tmpfiles.d"

internal-clean:
	make -C binsrc clean

internal-supplement:
	# Fixup symbolic links
	mkdir -p $(ENVGENDIR)
	mkdir -p $(USRENVGENDIR)
	ln -sr $(INSTALLDIR)/80-genie-envar.sh $(ENVGENDIR)/80-genie-envar.sh
	ln -sr $(INSTALLDIR)/80-genie-envar.sh $(USRENVGENDIR)/80-genie-envar.sh

	# Man page.
	# Make sure directory exists.
	mkdir -p "$(MAN8DIR)"

 	# this bit would ordinarily be handed by debuild, etc.
	cp "othersrc/docs/genie.8" /tmp/genie.8
	gzip -f9 "/tmp/genie.8"
	install -Dm 0644 -o root "/tmp/genie.8.gz" -t "$(MAN8DIR)"

internal-tar:
	# tar it up
	tar zcvf genie-systemd-$(GENIEVERSION)-amd64.tar.gz tarball/* --transform='s/^tarball//'

#
# Helpers: intermediate build stages.
#

make-output-directory:
	mkdir -p out

build-binaries:
	make -C binsrc
