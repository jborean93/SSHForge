#if NET8_0_OR_GREATER
using RemoteForge;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tmds.Ssh;

namespace SSHForge;

[Cmdlet(VerbsCommon.New, "TmdsSshInfo", DefaultParameterSetName = "UserName")]
[OutputType(typeof(TmdsSshInfo))]
public sealed class NewTmdsSshInfo : PSCmdlet
{
    private PSCredential? _credential;

    [Parameter(Position = 0, Mandatory = true)]
    public string ComputerName { get; set; } = string.Empty;

    [Parameter]
    public int Port { get; set; } = 22;

    [Parameter(ParameterSetName = "Credential")]
    [Credential]
    public PSCredential? Credential
    {
        get => _credential;
        set => _credential = value;
    }

    [Parameter(ParameterSetName = "UserName")]
    public string? UserName
    {
        get => _credential?.UserName;
        set => _credential = new(value, new());
    }

    [Parameter]
    public string Subsystem { get; set; } = TmdsSshTransport.DefaultSubsystem;

    [Parameter]
    public SwitchParameter SkipHostKeyCheck { get; set; }

    protected override void EndProcessing()
    {
        WriteObject(new TmdsSshInfo(
            ComputerName,
            Port,
            Subsystem,
            SkipHostKeyCheck,
            _credential));
    }
}

public sealed class TmdsSshInfo : IRemoteForge
{
    public static string ForgeName => "TmdsSsh";
    public static string ForgeDescription => ".NET managed SSH connection with Tmds.Ssh";

    public string ComputerName { get; }
    public int Port { get; }
    public string? Subsystem { get; }
    public PSCredential? Credential { get; }
    public bool SkipHostKeyCheck { get; }

    internal TmdsSshInfo(
        string hostname,
        int port = 22,
        string? subsystem = null,
        bool skipHostKeyCheck = false,
        PSCredential? credential = null)
    {
        ComputerName = hostname;
        Port = port;
        Subsystem = subsystem;
        SkipHostKeyCheck = skipHostKeyCheck;
        Credential = credential;
    }

    public string GetTransportString()
        => $"{ForgeName}:{ComputerName}:{Port}";

    public static IRemoteForge Create(string info)
    {
        (string hostname, int port, string? user) = SSHTransport.ParseSSHInfo(info);
        PSCredential? credential = null;
        if (user != null)
        {
            credential = new(user, new());
        }

        return new TmdsSshInfo(
            hostname,
            port: port,
            subsystem: null,
            skipHostKeyCheck: false,
            credential: credential);
    }

    public RemoteTransport CreateTransport()
    {
        List<Credential> sshCredentials = new();
        if (Credential != null && Credential.Password.Length > 0)
        {
            sshCredentials.Add(new KerberosCredential(Credential.GetNetworkCredential()));
        }
        else
        {
            sshCredentials.Add(new KerberosCredential());
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        string rsaKey = Path.Combine(home, ".ssh", "id_rsa");
        if (File.Exists(rsaKey))
        {
            sshCredentials.Add(new PrivateKeyCredential(rsaKey));
        }

        if (Credential != null && Credential.Password.Length > 0)
        {
            sshCredentials.Add(new PasswordCredential(Credential.GetNetworkCredential().Password));
        }

        SshClientSettings settings = new()
        {
            Host = ComputerName,
            Port = Port,
            UserName = Credential?.UserName ?? Environment.UserName,
            Credentials = sshCredentials,
        };

        if (SkipHostKeyCheck)
        {
            settings.HostAuthentication = (knownHostResult, connectionInfo, cancellationToken) => ValueTask.FromResult(true);
        }

        SshClient client = new(settings);
        return new TmdsSshTransport(client, Subsystem);
    }
}

public sealed class TmdsSshTransport : RemoteTransport
{
    public const string DefaultSubsystem = "powershell";

    private readonly SshClient _client;
    private readonly string _subsystem;
    private RemoteProcess? _proc;

    internal TmdsSshTransport(SshClient client, string? subsystem)
    {
        _client = client;
        _subsystem = subsystem ?? DefaultSubsystem;
    }

    protected override async Task Open(CancellationToken cancellationToken)
    {
        await _client.ConnectAsync(cancellationToken);
        _proc = await _client.ExecuteSubsystemAsync(_subsystem, cancellationToken);
    }

    protected override Task Close(CancellationToken cancellationToken)
    {
        _proc?.Dispose();
        _proc = null;
        return Task.CompletedTask;
    }

    protected override async Task<string?> ReadOutput(CancellationToken cancellationToken)
    {
        Debug.Assert(_proc != null);
        (bool isError, string? line) = await _proc.ReadLineAsync(cancellationToken: cancellationToken);
        if (isError)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                throw new Exception($"Received stderr from remote process: {line}");

            }
        }

        return line;
    }

    protected override async Task WriteInput(string input, CancellationToken cancellationToken)
    {
        Debug.Assert(_proc != null);
        await _proc.WriteLineAsync(input, cancellationToken);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            _proc?.Dispose();
            _client.Dispose();
        }
        base.Dispose(isDisposing);
    }
}
#endif
