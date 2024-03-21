using System.Management.Automation;
using RemoteForge;

namespace SSHForge;

public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
        RemoteForgeRegistration.Register(typeof(HvcInfo).Assembly);
    }

    public void OnRemove(PSModuleInfo module)
    {
        RemoteForgeRegistration.Unregister(HvcInfo.ForgeName);
    }
}
