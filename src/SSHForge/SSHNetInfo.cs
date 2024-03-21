using RemoteForge;
using Renci.SshNet;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace SSHForge;

public sealed class SSHNetInfo : IRemoteForge
{
    public static string ForgeName => "sshnet";
    public static string ForgeDescription => "SSH.NET wrapped PowerShell session";

    public string ComputerName { get; }
    public int Port { get; }
    public string UserName { get; }

    private SSHNetInfo(string computerName, int port, string username)
    {
        ComputerName = computerName;
        Port = port;
        UserName = username;
    }

    public static IRemoteForge Create(string info)
    {
        (string hostname, int port, string? user) = SSHTransport.ParseSSHInfo(info);

        if (string.IsNullOrWhiteSpace(user))
        {
            throw new ArgumentException("User must be supplied in sshnet connection string");
        }

        return new SSHNetInfo(hostname, port, user);
    }

    public RemoteTransport CreateTransport()
    {
        ConnectionInfo connInfo = new ConnectionInfo(
            ComputerName,
            Port,
            UserName,
            new AuthenticationMethod[]{
                new PasswordAuthenticationMethod(UserName, Environment.GetEnvironmentVariable("TEST_PASS")),
            });
        return new SSHNetTransport(connInfo);
    }
}

public sealed class SSHNetTransport : RemoteTransport
{
    private readonly SshClient _client;
    private SshCommand? _cmd;
    private StreamReader? _stdoutReader;
    private StreamReader? _stderrReader;
    private StreamWriter? _stdinWriter;
    private IAsyncResult _cmdTask;

    internal SSHNetTransport(ConnectionInfo connInfo)
    {
        _client = new(connInfo);
    }

    protected override async Task Open(CancellationToken cancellationToken)
    {
        await _client.ConnectAsync(cancellationToken);
        _cmd = _client.CreateCommand("pwsh -NoProfile -SSHServerMode");
        _cmdTask = _cmd.BeginExecute();
        _stdoutReader = new(_cmd.OutputStream);
        _stderrReader = new(_cmd.ExtendedOutputStream);
        _stdinWriter = new(_cmd.CreateInputStream());
    }

    protected override Task Close(CancellationToken cancellationToken)
    {
        if (_cmd != null)
        {
            _cmd.CancelAsync();
        }
        _client.Disconnect();
        return Task.CompletedTask;
    }

    protected override async Task<string?> ReadOutput(CancellationToken cancellationToken)
    {
        Debug.Assert(_stdoutReader != null);
        string? msg = await _stdoutReader.ReadLineAsync(cancellationToken);
        Console.WriteLine($"STDOUT: {msg}");
        Console.WriteLine("STDOUT END");
        if (msg == null)
        {
            string res = _cmd.EndExecute(_cmdTask);
            Console.WriteLine(res);
        }

        return msg;
    }

    protected override async Task<string?> ReadError(CancellationToken cancellationToken)
    {
        Debug.Assert(_stderrReader != null);
        string? msg = await _stderrReader.ReadToEndAsync(cancellationToken);
        Console.WriteLine($"STDERR: {msg}");

        return msg;
    }

    protected override async Task WriteInput(string input, CancellationToken cancellationToken)
    {
        Debug.Assert(_stdinWriter != null);
        Console.WriteLine($"STDIN: {input}");
        await _stdinWriter.WriteLineAsync(input.AsMemory(), cancellationToken);
        await _stdinWriter.FlushAsync();
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            _cmd?.Dispose();
        }
        base.Dispose(isDisposing);
    }
}
