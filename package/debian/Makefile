#
# Debian Makefile.
#

# Installation locations.

DESTDIR=debian/systemd-genie

INSTALLDIR = $(DESTDIR)/usr/libexec/genie
BINDIR = $(DESTDIR)/usr/bin
ETCDIR = $(DESTDIR)/etc
SVCDIR = $(DESTDIR)/lib/systemd/system

#
# Default target: package.
#

package:
	# Packaging for Debian.
	debuild
	mkdir -p ../../out/debian
	mv ../systemd-genie* ../../out/debian

build-inner:
	# Innermost iteration; invoked by debuild.
	make -C binsrc

package-inner:
	# Innermost iteration; invoked by debuild.

	# Binaries.
	mkdir -p "$(BINDIR)"
	install -Dm 4755 -o root "binsrc/genie/bin/Release/net5.0/linux-x64/publish/genie" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "othersrc/scripts/drop_cache_if_idle" -t "$(INSTALLDIR)"

	# Environment generator.
	install -Dm 0755 -o root "othersrc/scripts/10-genie-envar.sh" -t "$(INSTALLDIR)"

	# Configuration file.
	install -Dm 0644 -o root "othersrc/etc/genie.ini" -t "$(ETCDIR)"

	# Unit files.
    install -Dm 0644 -o root "othersrc/lib-systemd-system/wsl-drop-caches.service" -t "$(SVCDIR)"
    install -Dm 0644 -o root "othersrc/lib-systemd-system/wsl-drop-caches.timer" -t "$(SVCDIR)"

	install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.service" -t "$(SVCDIR)"
    install -Dm 0644 -o root "othersrc/lib-systemd-system/wslg-xwayland.socket" -t "$(SVCDIR)"

    install -Dm 0644 -o root "othersrc/lib-systemd-system/user-runtime-dir@.service.d/override.conf" -t "$(SVCDIR)/user-runtime-dir@.service.d/override.conf"

clean:
	debuild -- clean

clean-inner:
	# Innermost iteration; invoked by debuild.
	make -C binsrc clean
