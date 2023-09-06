using SharedLib.Processes;

namespace Deployment;

public class SteamDeployProcess : DeployProcess
{
	public SteamDeployProcess(params BuildProcess[] buildProcesses) : base(buildProcesses)
	{
	}
}