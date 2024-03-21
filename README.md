# PowerShell SSHForge

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/jborean93/SSHForge/blob/main/LICENSE)

PowerShell RemoteForge implementation for a SSH and Hyper-V VMs over SSH.

See [SSHForge index](docs/en-US/SSHForge.md) for more details.

## Requirements

These cmdlets have the following requirements

* PowerShell v7.3 or newer
+ [RemoteForge](https://github.com/jborean93/RemoteForge)

## Examples

TODO

## Installing

This module is not available in the gallery at this point in time as it is more a POC for `RemoteForge`.

## Contributing

Contributing is quite easy, fork this repo and submit a pull request with the changes.
To build this module run `.\build.ps1 -Task Build` in PowerShell.
To test a build run `.\build.ps1 -Task Test` in PowerShell.
This script will ensure all dependencies are installed before running the test suite.
