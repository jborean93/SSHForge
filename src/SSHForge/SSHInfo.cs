using RemoteForge;
using System.Management.Automation;
using System.Security;

namespace SSHForge;

[Cmdlet(VerbsCommon.New, "SSHv2ForgeInfo", DefaultParameterSetName = "Credential")]
[OutputType(typeof(SSHInfo))]
public sealed class NewSSHv2ForgeInfo : PSCmdlet
{
    private PSCredential? _credential;

    [Parameter(Mandatory = true, Position = 0)]
    [Alias("HostName")]
    public string ComputerName { get; set; } = string.Empty;

    [Parameter]
    public int? Port { get; set; }

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
        get => null;
        set => _credential = new(value, new SecureString());
    }

    [Parameter]
    public SwitchParameter SkipHostKeyCheck { get; set; }

    protected override void EndProcessing()
    {
        (string hostname, int port, string? user) = SSHTransport.ParseSSHInfo(ComputerName);
        if (_credential == null && user != null)
        {
            _credential = new(user, new SecureString());
        }

        WriteObject(new SSHInfo(
            hostname,
            Port != null ? (int)Port : port,
            _credential,
            SkipHostKeyCheck));
    }
}

public sealed class SSHInfo : IRemoteForge
{
    public static string ForgeName => "sshv2";
    public static string ForgeDescription => "Custom SSH remote transport with extra features";

    public string ComputerName { get; }
    public int Port { get; }
    public PSCredential? Credential { get; }
    public bool SkipHostKeyCheck { get; }

    internal SSHInfo(
        string hostname,
        int port = 22,
        PSCredential? credential = null,
        bool skipHostKeyCheck = false)
    {
        ComputerName = hostname;
        Port = port;
        Credential = credential;
        SkipHostKeyCheck = skipHostKeyCheck;
    }

    public static IRemoteForge Create(string info)
    {
        (string hostname, int port, string? user) = SSHTransport.ParseSSHInfo(info);
        PSCredential? credential = null;
        if (user != null)
        {
            credential = new(user, new());
        }

        return new SSHInfo(
            hostname,
            port: port,
            credential: credential);
    }

    public RemoteTransport CreateTransport()
        => SSHTransport.Create(
            ComputerName,
            Port,
            credential: Credential,
            disableHostKeyCheck: SkipHostKeyCheck);
}
