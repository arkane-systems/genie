#
# Tar archive Makefile.
#

# Installation locations

DESTDIR=tarball

INSTALLDIR = $(DESTDIR)/usr/lib/genie
BINDIR = $(DESTDIR)/usr/bin
ENVGENDIR = $(DESTDIR)/usr/lib/systemd/system-environment-generators
USRENVGENDIR = $(DESTDIR)/usr/lib/systemd/user-environment-generators
ETCDIR = $(DESTDIR)/etc
MAN8DIR = $(DESTDIR)/usr/share/man/man8
SVCDIR = $(DESTDIR)/usr/lib/systemd/system
USRLIBDIR = $(DESTDIR)/usr/lib

#
# Default target: package.
#

package: clean
	# Packaging TAR archive.
	mkdir -p $(DESTDIR)

	make -C binsrc

	fakeroot make package-inner

	mkdir -p ../../out/tar
	mv genie-systemd-*.tar.gz ../../out/tar

package-inner:
	# Inner packager, called from within fakeroot.

	# Binaries.
	mkdir -p "$(BINDIR)"
	install -Dm 4755 -o root "binsrc/genie/bin/Release/net5.0/linux-x64/publish/genie" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "$(INSTALLDIR)"
	ln -s $(INSTALLDIR)/genie $(BINDIR)/genie

	# Environment generator.
	install -Dm 0755 -o root "othersrc/scripts/80-genie-envar.sh" -t "$(INSTALLDIR)"
	mkdir -p "$(ENVGENDIR)"
	mkdir -p "$(USRENVGENDIR)"
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

	# binfmt.d
	install -Dm 0644 -o root "othersrc/usr-lib/binfmt.d/WSLInterop.conf" -t "$(USRLIBDIR)/binfmt.d"

    # Man page.
	# Make sure directory exists.
	mkdir -p "$(MAN8DIR)"

 	# this bit would ordinarily be handed by debuild, etc.
	cp "othersrc/docs/genie.8" /tmp/genie.8
	gzip -9 "/tmp/genie.8"
	install -Dm 0644 -o root "/tmp/genie.8.gz" -t "$(MAN8DIR)"

	# tar it up
	tar zcvf genie-systemd-`grep VersionPrefix binsrc/genie/genie.csproj | sed 's/.*<VersionPrefix>\(.*\)<\/VersionPrefix>/\1/' | tr -d "\r\n"`.tar.gz tarball/* --transform='s/^tarball//'

clean:
	rm -rf tarball
	touch /tmp/genie-fake
	rm /tmp/genie*
