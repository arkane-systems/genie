%global project https://github.com/arkane-systems/genie/
%global version 1.38

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
install -d -p %{buildroot}%{_bindir}
install -m 4755 -vp binsrc/genie/bin/Release/net5.0/linux-x64/publish/genie %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp othersrc/scripts/bindxsocket.sh %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp othersrc/scripts/10-genie-envar.sh %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp othersrc/etc/genie.ini %{buildroot}%{_sysconfdir}/
ln -sf %{_libexecdir}/%{name}/%{name} %{buildroot}%{_bindir}/%{name}
ln -sf %{_libexecdir}/%{name}/10-genie-envar.sh %{buildroot}%{_exec_prefix}/lib/systemd/system-environment-generators/

%postun
rm -rf %{_libexecdir}/%{name}
rm -f %{_bindir}/%{name}
rm -f %{_exec_prefix}/lib/systemd/system-environment-generators/10-genie-envar.sh

%clean
rm -rf %{buildroot}

%files
%{_libexecdir}/%{name}/*
%config %{_sysconfdir}/genie.ini
%{_bindir}/%{name}
%{_exec_prefix}/lib/systemd/system-environment-generators/10-genie-envar.sh

%changelog
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
