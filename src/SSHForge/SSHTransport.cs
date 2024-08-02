using RemoteForge;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SSHForge;

public sealed class SSHTransport : ProcessTransport
{
    private Runspace? _runspace;

    internal SSHTransport(
        string executable,
        IEnumerable<string> arguments,
        Dictionary<string, string> environment,
        string? password) : base(executable, arguments, environment)

    {
        if (password != null)
        {
            _runspace = RunspaceFactory.CreateRunspace();
            _runspace.Open();
            _runspace.SessionStateProxy.SetVariable("ssh", password);
            Proc.StartInfo.Environment.Add("SSHFORGE_RID", _runspace.Id.ToString());
        }
    }

    internal static SSHTransport Create(
        string hostName,
        int port,
        string? executable = null,
        PSCredential? credential = null,
        bool disableHostKeyCheck = false)
    {
        string askPassFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "ask_pass.bat" : "ask_pass.sh";
        string askPassScript = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(HvcInfo).Assembly.Location)!,
            "..",
            "..",
            askPassFile));
        if (!File.Exists(askPassScript))
        {
            throw new FileNotFoundException($"Failed to find {askPassFile} script at '{askPassScript}'");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                Process.Start("chmod", $"+x {askPassScript}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set execution policy to {askPassScript}: {ex.Message}");
            }
        }

        List<string> sshArgs = new();
        if (string.IsNullOrWhiteSpace(executable))
        {
            executable = "ssh";
        }
        else
        {
            sshArgs.Add("ssh");
        }

        if (disableHostKeyCheck == true)
        {
            sshArgs.AddRange(new[]
            {
                "-o", "StrictHostKeyChecking=no",
                "-o", "UserKnownHostsFile=/dev/null",
            });
        }

        if (credential?.UserName != null)
        {
            sshArgs.AddRange(new[] { "-l", credential.UserName });
        }
        sshArgs.AddRange(new[]
        {
            "-p", port.ToString(),
            "-s", hostName,
            "powershell",
        });

        Dictionary<string, string> envVars = new()
        {
            { "SSH_ASKPASS", askPassScript },
            { "SSH_ASKPASS_REQUIRE", "force" },
            { "SSHFORGE_PID", Environment.ProcessId.ToString() },
        };

        return new SSHTransport(executable, sshArgs, envVars, credential?.GetNetworkCredential()?.Password);
    }

    protected override async Task<string?> ReadOutput(CancellationToken cancellationToken)
    {
        string? msg = await base.ReadOutput(cancellationToken);

        // Once we receive a message we don't need to keep the password in
        // the Runspace state anymore.
        if (_runspace != null)
        {
            _runspace.Dispose();
            _runspace = null;
        }

        return msg;
    }

    internal static (string, int, string?) ParseSSHInfo(string info)
    {
        // Split out the username portion first to allow UPNs that contain
        // @ as well before the last @ that separates the user from the
        // hostname. This is done because the Uri class will not work if the
        // user contains two '@' chars.
        string? userName = null;
        string hostname;
        int userSplitIdx = info.LastIndexOf('@');
        int hostNameOffset = 0;
        if (userSplitIdx == -1)
        {
            hostname = info;
        }
        else
        {
            hostNameOffset = userSplitIdx + 1;
            userName = info.Substring(0, userSplitIdx);
            hostname = info.Substring(userSplitIdx + 1);
        }

        // While we use the Uri class to validate and inspect the provided host
        // string, it does canonicalise the value so we need to extract the
        // original value used.
        Uri sshUri = new($"ssh://{hostname}");

        int port = sshUri.Port == -1 ? 22 : sshUri.Port;
        if (sshUri.HostNameType == UriHostNameType.IPv6)
        {
            // IPv6 is enclosed with [] and is canonicalised so we need to just
            // extract the value enclosed by [] from the original string for
            // the hostname.
            hostname = info[(1 + hostNameOffset)..info.IndexOf(']')];
        }
        else
        {
            // As the hostname is lower cased we need to extract the original
            // string value.
            int originalHostIndex = sshUri.OriginalString.IndexOf(
                sshUri.Host,
                StringComparison.OrdinalIgnoreCase);
            hostname = originalHostIndex == -1
                ? sshUri.Host
                : sshUri.OriginalString.Substring(originalHostIndex, sshUri.Host.Length);
        }

        return (hostname, port, userName);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing && _runspace != null)
        {
            _runspace.Dispose();
        }
        base.Dispose(isDisposing);
    }
}
