using SharedLib.Processes;

namespace Deployment;

public class AppleDeployProcess : DeployProcess
{
	public AppleDeployProcess(params BuildProcess[] buildProcesses) : base(buildProcesses)
	{
	}
}