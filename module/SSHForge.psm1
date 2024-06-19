# Copyright: (c) 2024, Jordan Borean (@jborean93) <jborean93@gmail.com>
# MIT License (see LICENSE or https://opensource.org/licenses/MIT)

$importModule = Get-Command -Name Import-Module -Module Microsoft.PowerShell.Core

$netVersion = if ($PSVersionTable.PSVersion.Minor -eq 3) {
    'net7.0'
}
elseif ($PSVersionTable.PSVersion.Minor -eq 4) {
    'net8.0'
}
else {
    'net9.0'
}

$moduleName = [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath)
$modPath = [System.IO.Path]::Combine($PSScriptRoot, 'bin', $netVersion, "$moduleName.dll")
&$importModule -Name $modPath -ErrorAction Stop -PassThru
