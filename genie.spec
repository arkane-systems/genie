%global project https://github.com/arkane-systems/genie/
%global version 2.5

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

Requires:      daemonize
Requires:      dbus
Requires:      gawk
Requires:      polkit
Requires:      python3 >= 3.7
Requires:      python3-pip
Requires:      python3-psutil
Requires:      systemd >= 232.25
Requires:      systemd-container >= 232.25
Conflicts:     bottle-imp
# BuildRequires: git
BuildRequires: make

%description
A quick way into systemd "bottle" for WSL

%prep
%setup -q -n %{name}-%{version}

%build
make build-binaries

%install
echo %{buildroot}%{_mandir}

install -d -p %{buildroot}%{_sysconfdir}
install -d -p %{buildroot}%{_exec_prefix}/lib/%{name}
install -d -p %{buildroot}%{_exec_prefix}/lib/systemd/system-environment-generators
install -d -p %{buildroot}%{_exec_prefix}/lib/systemd/user-environment-generators
install -d -p %{buildroot}%{_exec_prefix}/lib/tmpfiles.d
install -d -p %{buildroot}%{_bindir}
install -d -p %{buildroot}%{_unitdir}
install -d -p %{buildroot}%{_unitdir}/user-runtime-dir@.service.d
install -d -p %{buildroot}%{_mandir}/man8

make DESTDIR=%{buildroot} internal-package
make DESTDIR=%{buildroot} internal-supplement

%postun
if [ $1 -eq 0 ]; then
rm -f %{_bindir}/%{name}
rm -rf %{_exec_prefix}/lib/%{name}/*
rm -f %{_unitdir}/user-runtime-dir@.service.d/override.conf
rm -f %{_exec_prefix}/lib/tmpfiles.d/wslg.conf
rm -f %{_exec_prefix}/lib/systemd/system-environment-generators/80-genie-envar.sh
rm -f %{_exec_prefix}/lib/systemd/user-environment-generators/80-genie-envar.sh
rm -f ${_mandir}/man8/genie.8.gz
fi

%clean
rm -rf %{buildroot}

%files

%{_bindir}/%{name}
%{_exec_prefix}/lib/%{name}/*
%config %{_sysconfdir}/genie.ini
%{_unitdir}/user-runtime-dir@.service.d/override.conf
%{_exec_prefix}/lib/tmpfiles.d/wslg.conf
%{_exec_prefix}/lib/systemd/system-environment-generators/80-genie-envar.sh
%{_exec_prefix}/lib/systemd/user-environment-generators/80-genie-envar.sh
%doc %{_mandir}/man8/genie.8.gz

%changelog
* Fri Sep 23 2022 Alistair Young <avatar@arkane-systems.net> 2.5-1
- Fixed Debian multiarch packaging.
- Dynamically build WSLInterop.conf based on existing (fixes #287, #295).
- Fix for schrödinbug (fixes #298).
- Allow for faster timeout when system in degraded state.
- Added note and referrer about native systemd support.
- Added bottle-imp conflict to packages.

* Sat Jun 25 2022 Alistair Young <avatar@arkane-systems.net> 2.4-1
- Fixed missing dependency versions.
- Python refactoring.
- Fixed no-command-specified error for genie -c.
- Added cwd preservation note to help for genie -c.
- Added proper return values for status checks (fixes #269).
- Properly configure WSLInterop binary format (fixes #267, #264).
- Carries through real UID from wrapper (fixes #258).
- Use systemd-tmpfiles for WSLg support (fixes #214, #175).
- Warn user if unsupported Unicode hostname (warns on #268, no fix).
- arm64 package for Fedora.
- Miscellaneous fixes.

* Tue Mar 22 2022 Alistair Young <avatar@arkane-systems.net> 2.3-1
- Paths-containing-spaces fix (#240).
- Makefile updates for CI build.
- Fix WSL 1 detection.
- Added -a/--as-user option to allow shell/command as any user.
- Added support for Ubuntu 22.04 LTS (Jammy Jellyfish).
- Greater robustness against misconfigured hosts files (fixes #247).
- Miscellaneous fixes.

* Sun Mar 06 2022 Alistair Young <avatar@arkane-systems.net> 2.2-1
- Single-file package python scripts.
- Man page fixes.
- Fixed building on Python 3.10.
- Dropped the "local" install option (little used; use tarball instead).

* Mon Feb 28 2022 Alistair Young <avatar@arkane-systems.net> 2.1-1
- Documentation updates.
- Update /etc/hosts after hostname update.
- Minor fixes.

* Tue Feb 22 2022 Alistair Young <avatar@arkane-systems.net> 2.0b-1

- Major rewrite in Python, eliminating .NET dependency.
- Moved executables from /usr/libexec/genie to /usr/lib/genie.
- Allow configuration of hostname suffix.
- Support for AppArmor namespaces.
- Work to better handle simultaneity.
- Extra warnings for problematic states.
- Miscellaneous fixes.

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
- Better WSLg support, based on the code of Dani Llewellyn (@diddledani), here: https://github.com/diddledani/one-script-wsl2-systemd.

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
