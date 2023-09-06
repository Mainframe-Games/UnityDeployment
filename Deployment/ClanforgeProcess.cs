using SharedLib.Processes;

namespace Deployment;

public class ClanforgeProcess : DeployProcess
{
	public ClanforgeProcess(params BuildProcess[] buildProcesses) : base(buildProcesses)
	{
	}
}