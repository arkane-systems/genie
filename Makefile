#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the release zip
#

all:
	make -C genie
	# Merge in place
	mkdir -p systemd-genie/usr/bin
	cp genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie systemd-genie/usr/bin/
	cp genie/bin/Release/netcoreapp3.1/linux-x64/publish/*.dll systemd-genie/usr/bin/
	cp genie/bin/Release/netcoreapp3.1/linux-x64/publish/genie.runtimeconfig.json systemd-genie/usr/bin/
	# add the man page
	cp genie.8 systemd-genie/usr/share/man/man8
	gzip -9 systemd-genie/usr/share/man/man8/genie.8
	# Set in-package permissions
	sudo chown root:root systemd-genie/etc/*
	sudo chown root:root systemd-genie/usr/bin/*
	sudo chmod u+s systemd-genie/usr/bin/genie
	sudo chmod a+rx systemd-genie/usr/bin/genie
	# Make the distro zip.
	sudo tar zcvf genie.tar.gz systemd-genie/*

#
# debian: build the deb installation package
#

pkg-deb:
	# Make debian subfolder
	mkdir -p systemd-genie/DEBIAN
	# Set in-package permissions
	sudo chmod -R 0755 systemd-genie/DEBIAN
	# Copy control files
	cp debian/* systemd-genie/DEBIAN
	# Compute md5 sums
	sudo md5sum systemd-genie/usr/bin/* | sed 's/systemd-genie//' > systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/usr/lib/genie/* | sed 's/systemd-genie//' >> systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/usr/lib/systemd/system-environment-generators/* | sed 's/systemd-genie//' >> systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/etc/* | sed 's/systemd-genie//' >> systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/usr/share/doc/genie/* | sed 's/systemd-genie//' >> systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/usr/share/man/man8/* | sed 's/systemd-genie//' >> systemd-genie/DEBIAN/md5sums
	# Make .deb package
	sudo dpkg-deb --build systemd-genie

debian: clean all pkg-deb

#
# arch: build the arch installation package (files only)
#

arch: clean all

#
# install: build for /usr/local and install locally
#

install:
	make -C genie local
	echo "If you have not yet, you must install the dependent packages - daemonize, hostess, and all systemd deps."
	sudo install -Dm 4755 -o root "genie/bin/ReleaseLocal/netcoreapp3.1/linux-x64/publish/genie" -t "/usr/local/bin"
	sudo install -Dm 644 -o root "genie/bin/ReleaseLocal/netcoreapp3.1/linux-x64/publish/genie.dll" -t "/usr/local/bin"
	sudo install -Dm 744 -o root "genie/bin/ReleaseLocal/netcoreapp3.1/linux-x64/publish/Linux.ProcessManager.dll" -t "/usr/local/bin"
	sudo install -Dm 744 -o root "genie/bin/ReleaseLocal/netcoreapp3.1/linux-x64/publish/System.CommandLine.dll" -t "/usr/local/bin"
	sudo install -Dm 744 -o root "genie/bin/ReleaseLocal/netcoreapp3.1/linux-x64/publish/Tmds.LibC.dll" -t "/usr/local/bin"
	sudo install -Dm 644 -o root "genie/bin/ReleaseLocal/netcoreapp3.1/linux-x64/publish/genie.runtimeconfig.json" -t "/usr/local/bin"
	sudo install -Dm 644 -o root "systemd-genie/etc/genie.ini" -t "/etc"
	sudo install -Dm 755 -o root "systemd-genie/usr/lib/genie/dumpwslenv.sh" -t "/usr/local/lib/genie/"
	sudo install -Dm 755 -o root "systemd-genie/usr/lib/genie/readwslenv.sh" -t "/usr/local/lib/genie/"
	sudo install -Dm 755 -o root "systemd-genie/usr/lib/genie/runinwsl.sh" -t "/usr/local/lib/genie/"
	sudo install -Dm 755 -o root "systemd-genie/usr/lib/systemd/system-environment-generators/10-genie-envar.sh" -t "/usr/local/lib/systemd/system-environment-generators"
	# !! UPDATE FOR MAN PAGE AND EXECUTABLE MOTION

#
# clean: delete the package interim files and products
#

clean:
	make -C genie clean
	rm -rf systemd-genie/DEBIAN
	rm -rf systemd-genie/usr/bin/*
	rm -f systemd-genie/usr/share/man/man8/genie.8.gz
	rm -f genie.tar.gz systemd-genie.deb
