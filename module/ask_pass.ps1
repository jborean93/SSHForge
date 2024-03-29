#!/usr/bin/env pwsh

using namespace System.Management.Automation.Runspaces
using namespace System.Net

param ([string]$Prompt)

$ErrorActionPreference = 'Stop'

if ($env:SSHFORGE_RID) {
    # If SSHFORGE_RID is set the password is stored in the runspace specified
    $script = {
        $ErrorActionPreference = 'Stop'

        $rs = Get-Runspace -Id $args[0]
        if (-not $rs) {
            throw "Failed to find runspace $($args[0]) to retrieve SSH password"
        }
        $rs.SessionStateProxy.GetVariable("ssh")
    }

    $argument = $env:SSHFORGE_RID
}
else {
    # If SSHFORGE_RID isn't set we need to prompt for the password
    $script = {
        $ErrorActionPreference = 'Stop'

        $rs = Get-Runspace -Id 1
        $remoteHost = $rs.GetType().GetProperty(
            'Host',
            [System.Reflection.BindingFlags]'Instance, NonPublic'
        ).GetValue($rs)
        $remoteHost.UI.Write($args[0])
        $remoteHost.UI.ReadLineAsSecureString()
    }

    $argument = $Prompt
}

$ci = [NamedPipeConnectionInfo]::new([int]$env:SSHFORGE_PID)
$rs = $ps = $null
try {
    $rs = [runspacefactory]::CreateRunspace($ci)
    $rs.Open()

    $ps = [powershell]::Create($rs)
    $null = $ps.AddScript($script.ToString()).AddArgument($argument)

    try {
        $output = $ps.Invoke()
    }
    catch {
        $host.UI.WriteErrorLine($_.Exception.InnerException.SerializedRemoteException.Message)
        exit 1
    }

    if ($output) {
        $host.UI.WriteLine([NetworkCredential]::new('', $output[0]).Password)
    }
}
finally {
    ${ps}?.Dispose()
    ${rs}?.Dispose()
}
