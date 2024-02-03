# PowerShell HvcForge

[![Test workflow](https://github.com/jborean93/HvcForge/workflows/Test%20HvcForge/badge.svg)](https://github.com/jborean93/HvcForge/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/jborean93/HvcForge/branch/main/graph/badge.svg?token=b51IOhpLfQ)](https://codecov.io/gh/jborean93/HvcForge)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/dt/HvcForge.svg)](https://www.powershellgallery.com/packages/HvcForge)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/jborean93/HvcForge/blob/main/LICENSE)

PowerShell RemoteForge implementation for a Hyper-V VM.

See [HvcForge index](docs/en-US/HvcForge.md) for more details.

## Requirements

These cmdlets have the following requirements

* PowerShell v7.3 or newer
+ [RemoteForge](https://github.com/jborean93/RemoteForge)

## Examples

TODO

## Installing

The easiest way to install this module is through [PowerShellGet](https://docs.microsoft.com/en-us/powershell/gallery/overview).

You can install this module by running either of the following `Install-PSResource` or `Install-Module` command.

```powershell
# Install for only the current user
Install-PSResource -Name HvcForge -Scope CurrentUser
Install-Module -Name HvcForge -Scope CurrentUser

# Install for all users
Install-PSResource -Name HvcForge -Scope AllUsers
Install-Module -Name HvcForge -Scope AllUsers
```

The `Install-PSResource` cmdlet is part of the new `PSResourceGet` module from Microsoft available in newer versions while `Install-Module` is present on older systems.

## Contributing

Contributing is quite easy, fork this repo and submit a pull request with the changes.
To build this module run `.\build.ps1 -Task Build` in PowerShell.
To test a build run `.\build.ps1 -Task Test` in PowerShell.
This script will ensure all dependencies are installed before running the test suite.
