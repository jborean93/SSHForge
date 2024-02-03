using RemoteForge;
using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace HvcForge;

public sealed class HvcInfo : IRemoteForge
{
    public static string ForgeName => "hvc";
    public static string ForgeDescription => "Hyper-V ssh wrapped PowerShell session";

    public string ComputerName { get; }
    public int Port { get; }
    public PSCredential? Credential { get; }

    public HvcInfo(
        string hostname,
        int port = 22,
        string? userName = null)
    {
        ComputerName = hostname;
        Port = port;

        if (userName != null)
        {
            Credential = new(userName, new SecureString());
        }
    }

    public HvcInfo(
        string hostname,
        int port = 22,
        PSCredential? credential = null)
    {
        ComputerName = hostname;
        Port = port;
        Credential = credential;
    }

    public static IRemoteForge Create(string info)
    {
        (string hostname, int port, string? user) = ParseSSHInfo(info);

        return new HvcInfo(hostname, port: port, userName: user);
    }

    public IRemoteForgeTransport CreateTransport()
        => new HvcInfoTransport(
            ComputerName,
            Port,
            Credential);

    // public async Task StartTransport(
    //     ChannelReader<string> reader,
    //     ChannelWriter<string> writer,
    //     CancellationToken cancellationToken)
    // {
    //     using Process proc = Process.Start("");

    //     await proc.WaitForExitAsync();
    // }

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
}

public sealed class HvcInfoTransport : IRemoteForgeTransport
{
    private readonly ChannelReader<(bool, string?)> _reader;
    private readonly ChannelWriter<(bool, string?)> _writer;
    private readonly Runspace? _runspace;
    private readonly string _hostname;
    private readonly int _port;
    private readonly string? _username;

    private Process? _proc;

    internal HvcInfoTransport(
        string hostname,
        int port,
        PSCredential? credential)
    {
        Channel<(bool, string?)> channel = Channel.CreateUnbounded<(bool, string?)>();
        _reader = channel.Reader;
        _writer = channel.Writer;

        _hostname = hostname;
        _port = port;
        _username = credential?.UserName;
        if (credential?.Password?.Length > 0)
        {
            _runspace = RunspaceFactory.CreateRunspace();
            _runspace.Open();
            _runspace.SessionStateProxy.SetVariable("ssh", credential.Password);
        }
    }

    public Task CreateConnection(CancellationToken cancellationToken)
    {
        string askPassScript = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(HvcInfo).Assembly.Location)!,
            "..",
            "..",
            "ask_pass.bat"));
        if (!File.Exists(askPassScript))
        {
            throw new FileNotFoundException($"Failed to find ask_pass.ps1 script at '{askPassScript}'");
        }

        _proc = new()
        {
            StartInfo = new()
            {
                FileName = "hvc.exe",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
            }
        };

        _proc.StartInfo.Environment.Add("SSH_ASKPASS", askPassScript);
        _proc.StartInfo.Environment.Add("SSH_ASKPASS_REQUIRE", "force");
        _proc.StartInfo.Environment.Add("HVCFORGE_PID", Environment.ProcessId.ToString());

        if (_runspace != null)
        {
            _proc.StartInfo.Environment.Add("HVCFORGE_RID", _runspace.Id.ToString());
        }

        _proc.StartInfo.ArgumentList.Add("ssh");

        if (_username != null)
        {
            _proc.StartInfo.ArgumentList.Add("-l");
            _proc.StartInfo.ArgumentList.Add(_username);
        }

        _proc.StartInfo.ArgumentList.Add("-p");
        _proc.StartInfo.ArgumentList.Add(_port.ToString());

        _proc.StartInfo.ArgumentList.Add("-s");

        _proc.StartInfo.ArgumentList.Add(_hostname);

        _proc.StartInfo.ArgumentList.Add("powershell");

        _proc.Start();

        Task stdoutTask = Task.Run(async () => await PumpStream(_proc.StandardOutput, false));
        Task stderrTask = Task.Run(async () => await PumpStream(_proc.StandardError, true));
        Task.Run(async () =>
        {
            try
            {
                await _proc.WaitForExitAsync();
                await stdoutTask;
                await stderrTask;
            }
            finally
            {
                _writer.Complete();
            }
        });

        return Task.CompletedTask;
    }

    private async Task PumpStream(StreamReader reader, bool isError)
    {
        while (true)
        {
            string? msg = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(msg))
            {
                break;
            }

            await _writer.WriteAsync((isError, msg));
        }
    }

    public Task CloseConnection(CancellationToken cancellationToken)
    {
        if (_proc != null)
        {
            _proc.Kill();
            // await _reader.Completion;
        }

        return Task.CompletedTask;
    }

    public async Task WriteMessage(string message, CancellationToken cancellationToken)
    {
        Debug.Assert(_proc != null);
        await _proc.StandardInput.WriteLineAsync(message.AsMemory(), cancellationToken);
    }

    public async Task<string?> WaitMessage(CancellationToken cancellationToken)
    {
        Debug.Assert(_proc != null);
        try
        {
            (bool isError, string? msg) = await _reader.ReadAsync(cancellationToken);
            if (isError)
            {
                throw new Exception(msg);
            }

            return msg;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _proc?.Dispose();
        _proc = null;
    }
}
