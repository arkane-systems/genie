%global project https://github.com/arkane-systems/genie/
%global version 1.44

# debuginfo is 'not supported' for .NET binaries
%global debug_package %{nil}
%global _enable_debug_package 0
%global __os_install_post /usr/lib/rpm/brp-compress %{nil}

Name:          genie
Version:       %{version}
Release:       1%{?dist}
Summary:       A quick way into systemd "bottle" for WSL

License:       Unlicense
URL:           %{project}
Source0:       %{project}archive/genie-%{version}.tar.gz
Requires:      systemd-container
Requires:      daemonize
Requires:      dotnet-runtime-5.0
BuildRequires: dotnet-sdk-5.0
BuildRequires: make

ExclusiveArch: x86_64

%description
A quick way into systemd "bottle" for WSL

%prep
%setup -q -n %{name}-%{version}

%build
make -C binsrc

%install
pwd
install -d -p %{buildroot}%{_libexecdir}/%{name}
install -d -p %{buildroot}%{_sysconfdir}
install -d -p %{buildroot}%{_exec_prefix}/lib/systemd/system-environment-generators
install -d -p %{buildroot}%{_exec_prefix}/lib/systemd/user-environment-generators
install -d -p %{buildroot}%{_exec_prefix}/lib/tmpfiles.d
install -d -p %{buildroot}%{_exec_prefix}/lib/binfmt.d
install -d -p %{buildroot}%{_bindir}
install -d -p %{buildroot}%{_unitdir}
install -d -p %{buildroot}%{_unitdir}/user-runtime-dir@.service.d
install -d -p %{buildroot}%{_unitdir}/sockets.target.wants
install -m 4755 -vp binsrc/genie/bin/Release/net5.0/linux-x64/publish/genie %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp othersrc/scripts/80-genie-envar.sh %{buildroot}%{_libexecdir}/%{name}
install -m 0755 "othersrc/scripts/map-user-runtime-dir.sh" %{buildroot}%{_libexecdir}/%{name}
install -m 0755 "othersrc/scripts/unmap-user-runtime-dir.sh" %{buildroot}%{_libexecdir}/%{name}
install -m 0644 -vp othersrc/etc/genie.ini %{buildroot}%{_sysconfdir}/
install -m 0644 -vp othersrc/lib-systemd-system/wslg-xwayland.service %{buildroot}%{_unitdir}
install -m 0644 -vp othersrc/lib-systemd-system/wslg-xwayland.socket %{buildroot}%{_unitdir}
install -m 0644 -vp othersrc/lib-systemd-system/user-runtime-dir@.service.d/override.conf %{buildroot}%{_unitdir}/user-runtime-dir@.service.d
install -m 0644 -vp othersrc/usr-lib/binfmt.d/WSLInterop.conf %{buildroot}%{_exec_prefix}/lib/binfmt.d/
ln -sf %{_libexecdir}/%{name}/%{name} %{buildroot}%{_bindir}/%{name}
ln -sf %{_libexecdir}/%{name}/80-genie-envar.sh %{buildroot}%{_exec_prefix}/lib/systemd/system-environment-generators/
ln -sf %{_libexecdir}/%{name}/80-genie-envar.sh %{buildroot}%{_exec_prefix}/lib/systemd/user-environment-generators/
ln -sf %{_unitdir}/wslg-xwayland.socket %{buildroot}%{_unitdir}/sockets.target.wants/wslg-xwayland.socket

%postun
if [ $1 -eq 0 ]; then
rm -rf %{_libexecdir}/%{name}
rm -f %{_bindir}/%{name}
rm -f %{_exec_prefix}/lib/systemd/system-environment-generators/80-genie-envar.sh
rm -f %{_exec_prefix}/lib/systemd/user-environment-generators/80-genie-envar.sh
rm -f %{_unitdir}/sockets.target.wants/wslg-xwayland.socket
rm -f %{_unitdir}/wslg-xwayland.service
rm -f %{_unitdir}/wslg-xwayland.socket
rm -f %{_unitdir}/user-runtime-dir@.service.d/override.conf
rm -f %{_exec_prefix}/lib/binfmt.d/WSLInterop.conf
fi

%clean
rm -rf %{buildroot}

%files
%{_libexecdir}/%{name}/*
%config %{_sysconfdir}/genie.ini
%{_bindir}/%{name}
%{_exec_prefix}/lib/systemd/system-environment-generators/80-genie-envar.sh
%{_exec_prefix}/lib/systemd/user-environment-generators/80-genie-envar.sh
%{_unitdir}/wslg-xwayland.service
%{_unitdir}/wslg-xwayland.socket
%{_unitdir}/sockets.target.wants/wslg-xwayland.socket
%{_unitdir}/user-runtime-dir@.service.d/override.conf
%{_exec_prefix}/lib/binfmt.d/WSLInterop.conf

%changelog
* Sat Aug 07 2021 Alistair Young <avatar@arkane-systems.net> 1.44-1
- Standardized use of /usr/lib rather than /lib.
- Updated to ArkaneSystems.WSL 0.2.13.
- Made stub resolv.conf file option-controlled.
- Misc fixes.

* Thu Jul 29 2021 Alistair Young <avatar@arkane-systems.net> 1.43-1
- Based on collated systemd-analyze results, re-upped systemd startup timeout to 240.
- Added automated creation of resolv.conf symlink (per #130).
- Added fix for binfmt mount (per #142).

* Thu May 06 2021 Alistair Young <avatar@arkane-systems.net> 1.42-1
- Regression fixes.

* Thu May 06 2021 Alistair Young <avatar@arkane-systems.net> 1.41-1
- Moved user-runtime-dir@.service override to ExecStartPost.
- Fix virtualization detection for non-custom kernels.
- Detect slow-start as distinct from failed-start.

* Tue Apr 27 2021 Alistair Young <avatar@arkane-systems.net> 1.40-1
- Improved Fedora packaging to eliminate manual unit enable.
- Moved generic Linux/WSL functionality into shared assembly.
- Fixed missing user-environment-generator in Fedora package.
- Upnumbered genie-envar to fix missing path cloning on some systems.
- Fixed typo in wslg-xwayland.socket.
- Map XWayland socket only where WSLg is present and active.
- Mount user runtime directory only where WSLg is present and user matches.

* Thu Apr 22 2021 Alistair Young <avatar@arkane-systems.net> 1.39-1
- Better WSLg support, based on the code of Daniel Llewellyn (@diddledan), here: https://github.com/diddledan/one-script-wsl2-systemd.

* Thu Apr 22 2021 Alistair Young <avatar@arkane-systems.net> 1.38-1
- Restored original default systemd startup timeout.
- Changes to support WSLg.

* Fri Apr 16 2021 Alistair Young <avatar@arkane-systems.net> 1.37-1
- Merged fixes to Fedora packaging (PR #138).
- Reduced default systemd startup timeout to 60s.
- Added display of failed systemd units on timeout.

* Sun Mar 14 2021 Alistair Young <avatar@arkane-systems.net> 1.36-1
- Added dependency on hostname(1).
- Added --is-running and --is-in-bottle informational options.
- Added storage of systemd external PID in pidfile.
- Removed dependencies on mount(1) and hostname(1).
- Modified genie -u to wait for systemd exit.

* Mon Feb 22 2021 Alistair Young <avatar@arkane-systems.net> 1.35-1
- Packager modified for new build system.

* Wed Feb 17 2021 Gerard Braad <me@gbraad.nl> 1.34-1
- Initial version for Fedora
