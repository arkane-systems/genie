#
# This Makefile produces and packages the genie program.
#

# Installation locations for 'make install'
BIN = $(DESTDIR)/usr/bin
ETC = $(DESTDIR)/etc
LIBEXEC = $(DESTDIR)/usr/libexec/genie
LIBEXECMAIN = $(LIBEXEC)/main
ENVGEN = $(DESTDIR)/usr/lib/systemd/system-environment-generators
MAN8 = $(DESTDIR)/usr/share/man/man8
DOCS = $(DESTDIR)/usr/share/doc/genie

#
# default target: build the whole thing, but do not package
#
build:
	make -C genie

#
# install: install the package
#
install:
	install -Dm 4755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie" -t "$(LIBEXECMAIN)"
	install -Dm 0644 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie.runtimeconfig.json" -t "${LIBEXECMAIN}"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Linux.ProcessManager.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.Abstractions.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.FileExtensions.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.Ini.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.FileProviders.Abstractions.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.FileProviders.Physical.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Primitives.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/System.CommandLine.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/System.Runtime.CompilerServices.Unsafe.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Tmds.LibC.dll" -t "$(LIBEXECMAIN)"
	install -Dm 0755 -o root "scripts/dumpwslenv.sh" -t "$(LIBEXEC)"
	install -Dm 0755 -o root "scripts/runinwsl.sh" -t "$(LIBEXEC)"
	install -Dm 0755 -o root "scripts/10-genie-envar.sh" -t "$(ENVGEN)"
	mkdir -p "$(BIN)"
	install -Dm 0644 -o root "conf/genie.ini" -t "$(ETC)"

#
# install-local: install a build genie package locally
#
install-local:
	# Local install of the above
	install -Dm 4755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie" -t "/usr/local/libexec/genie/main"
	install -Dm 0644 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie.runtimeconfig.json" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Linux.ProcessManager.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.Abstractions.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.FileExtensions.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Configuration.Ini.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.FileProviders.Abstractions.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.FileProviders.Physical.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Microsoft.Extensions.Primitives.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/System.CommandLine.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/System.Runtime.CompilerServices.Unsafe.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "genie/bin/Release/netcoreapp3.1/linux-x64/publish/Tmds.LibC.dll" -t "/usr/local/libexec/genie/main"
	install -Dm 0755 -o root "scripts/dumpwslenv.sh" -t "/usr/local/libexec/genie"
	install -Dm 0755 -o root "scripts/runinwsl.sh" -t "/usr/local/libexec/genie"
	# Environment generator has to go in the same place
	install -Dm 0755 -o root "scripts/10-genie-envar.sh" -t "$(ENVGEN)"
	mkdir -p "$(BIN)"
	# Create symlink manually
	ln -s /usr/local/libexec/genie/main/genie /usr/local/bin/genie
	# Config file also to the same place
	install -Dm 0644 -o root "conf/genie.ini" -t "$(ETC)"
	# These are handled elsewhere by debuild, so we need to do them manually.
	cp "docs/genie.8" /tmp/genie.8
	gzip -9 "/tmp/genie.8"
	install -Dm 0644 -o root "/tmp/genie.8.gz" -t "$(MAN8)"

#
# local: build and install locally
#
local: clean build install-local

#
# clean: clean up after a build/package
#
clean:
	make -C genie clean

distclean: clean
