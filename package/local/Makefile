#
# Local installation Makefile.
#

# Installation locations.
INSTALLDIR = /usr/local/libexec/genie
BINDIR = /usr/local/bin
ENVGENDIR = /usr/lib/systemd/system-environment-generators
USRENVGENDIR = /usr/lib/systemd/user-environment-generators
ETCDIR = /etc
MAN8DIR = /usr/local/share/man/man8
SVCDIR = /etc/systemd/system
USRLIBDIR = /usr/lib

#
# Default target: package.
#

package:
	# Installing locally.
	make -C binsrc build-local

	# Just put the bits in the right places.
	mkdir -p "$(BINDIR)"

	# Binaries.
	install -Dm 4755 -o root "binsrc/genie/bin/ReleaseLocal/net5.0/linux-x64/publish/genie" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "othersrc/scripts/bindxsocket.sh" -t "$(INSTALLDIR)"
	ln -s $(INSTALLDIR)/genie $(BINDIR)/genie

	# Environment generator.
	install -Dm 0755 -o root "othersrc/scripts/80-genie-envar.sh" -t "$(INSTALLDIR)"
	ln -s $(INSTALLDIR)/80-genie-envar.sh $(ENVGENDIR)/80-genie-envar.sh
	ln -s $(INSTALLDIR)/80-genie-envar.sh $(USRENVGENDIR)/80-genie-envar.sh

	# Runtime dir mapping
	install -Dm 0755 -o root "othersrc/scripts/map-user-runtime-dir.sh" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "othersrc/scripts/unmap-user-runtime-dir.sh" -t "$(INSTALLDIR)"

	# Configuration file.
	install -Dm 0644 -o root "othersrc/etc/genie.ini" -t "$(ETCDIR)"

	# Unit files.
	install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.service" -t "$(SVCDIR)"
	install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.socket" -t "$(SVCDIR)"

	install -Dm 0644 -o root "othersrc/lib-systemd-system/user-runtime-dir@.service.d/override.conf" -t "$(SVCDIR)/user-runtime-dir@.service.d"

	# tmpfiles.d
	install -Dm 0644 -o root "othersrc/usr-lib/tmpfiles.d/genie-stub-resolv.conf" -t "$(USRLIBDIR)/tmpfiles.d"

	# enable units
	ln -s $(SVCDIR)/wslg-xwayland.socket $(SVCDIR)/sockets.target.wants/wslg-xwayland.socket

	# Man page.
	# Make sure directory exists.
	mkdir -p "$(MAN8DIR)"

	# this bit would ordinarily be handed by debuild, etc.
	cp "othersrc/docs/genie.8" /tmp/genie.8
	gzip -9 "/tmp/genie.8"
	install -Dm 0644 -o root "/tmp/genie.8.gz" -t "$(MAN8DIR)"

clean:
	# Nothing to clean in a local installation.
