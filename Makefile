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
	# ? make build-binaries
	#
	# Install locally
	#
	# ? make install-local
	#
	# Package (native)
	#
	# ? make package
	# ? make package-debian
	# ? make package-tar
	#
	# Clean up
	#
	# ? make clean (does not clean altpacking by default)
	# ? make clean-debian
	# ? make clean-tar

#
# Targets: individual end-product build.
#

# clean: clean-local clean-tar clean-debian clean-arch clean-fedora
# 	make -C binsrc clean
# 	rm -rf out

# package: package-tar package-debian

# Local installation

# install-local:
# 	make -C package/local package

# clean-local:
# 	make -C package/local clean

# Tarball

# package-tar: make-output-directory
# 	make -C package/tar package

# clean-tar:
# 	make -C package/tar clean

#
# Debian packaging
#

# Debian installation locations

# DESTDIR=debian/systemd-genie

# INSTALLDIR = $(DESTDIR)/usr/lib/genie
# BINDIR = $(DESTDIR)/usr/bin
# ETCDIR = $(DESTDIR)/etc
# SVCDIR = $(DESTDIR)/usr/lib/systemd/system
# USRLIBDIR = $(DESTDIR)/usr/lib

# package-debian: make-output-directory
# 	mkdir -p out/debian
# 	debuild
# 	mv ../systemd-genie_* out/debian

# clean-debian:
# 	debuild -- clean

# Debian internal functions

# internal-debian-package:

# 	# Binaries.
# 	mkdir -p "$(BINDIR)"
# 	install -Dm 4755 -o root "binsrc/genie/bin/Release/net5.0/linux-x64/publish/genie" -t "$(INSTALLDIR)"
# 	install -Dm 0755 -o root "binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "$(INSTALLDIR)"

# 	# Environment generator.
# 	install -Dm 0755 -o root "othersrc/scripts/80-genie-envar.sh" -t "$(INSTALLDIR)"

# 	# Runtime dir mapping
# 	install -Dm 0755 -o root "othersrc/scripts/map-user-runtime-dir.sh" -t "$(INSTALLDIR)"
# 	install -Dm 0755 -o root "othersrc/scripts/unmap-user-runtime-dir.sh" -t "$(INSTALLDIR)"

# 	# Target check
# 	install -Dm 0755 -o root "othersrc/scripts/check-default-target.sh" -t "$(INSTALLDIR)"

# 	# Configuration file.
# 	install -Dm 0644 -o root "othersrc/etc/genie.ini" -t "$(ETCDIR)"

# 	# Unit files.
# 	install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.service" -t "$(SVCDIR)"
# 	install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.socket" -t "$(SVCDIR)"

# 	install -Dm 0644 -o root "othersrc/lib-systemd-system/user-runtime-dir@.service.d/override.conf" -t "$(SVCDIR)/user-runtime-dir@.service.d"

# 	# binfmt.d
# 	install -Dm 0644 -o root "othersrc/usr-lib/binfmt.d/WSLInterop.conf" -t "$(USRLIBDIR)/binfmt.d"

# internal-debian-clean:
# 	make -C binsrc clean

#
# Helpers: intermediate build stages.
#

# make-output-directory:
# 	mkdir -p out

# build-binaries:
# 	make -C binsrc

#
# Altpacking
#

# package-arch: make-output-directory
# 	make -C package/arch package

# clean-arch:
# 	make -C package/arch clean

# package-fedora: make-output-directory # build-binaries
# 	make -C package/fedora package

# clean-fedora:
# 	make -C package/fedora clean
