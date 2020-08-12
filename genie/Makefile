#
# This Makefile produces and packages the genie program.
#

# Installation locations for 'make install'
BIN = $(DESTDIR)/usr/bin
ETC = $(DESTDIR)/etc
LIBEXEC = $(DESTDIR)/usr/libexec/genie
LIBEXECMAIN = $(LIBEXEC)/main
ENVGEN = $(LIBEXEC)/usr/lib/systemd/system-environment-generators
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
	ln -s "$(LIBEXECMAIN)/genie" "$(BIN)/genie"
	install -Dm 0644 -o root "conf/genie.ini" -t "$(ETC)"

#
# clean: clean up after a build/package
#
clean:
	make -C genie clean

distclean: clean

# produce release zip
# install in /usr/local
# make deb package
# clean up build files
