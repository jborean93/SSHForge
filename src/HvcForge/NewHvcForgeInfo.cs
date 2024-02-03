using System.Management.Automation;

namespace HvcForge;

[Cmdlet(VerbsCommon.New, "HvcForgeInfo")]
[OutputType(typeof(HvcInfo))]
public sealed class NewHvcForgeInfo : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string VMName { get; set; } = string.Empty;

    [Parameter]
    public int Port { get; set; } = 22;

    [Parameter()]
    [Credential]
    public PSCredential? Credential { get; set; }

    protected override void EndProcessing()
    {
        WriteObject(new HvcInfo(VMName, Port, Credential));
    }
}
