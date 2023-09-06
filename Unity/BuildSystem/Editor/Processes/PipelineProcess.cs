using System;

namespace BuildSystem.Processes
{
	[Serializable]
	public abstract class PipelineProcess
	{
	}

	[Serializable]
	public sealed class PreBuildProcess : PipelineProcess
	{
	}

	[Serializable]
	public abstract class DeployProcess : PipelineProcess
	{
		public BuildProcess[] BuildProcesses;
	}

	[Serializable]
	public class SteamDeployProcess : DeployProcess
	{
	}

	[Serializable]
	public class ClanforgeDeployProcess : DeployProcess
	{
	}

	[Serializable]
	public class AppleStoreDeployProcess : DeployProcess
	{
	}

	[Serializable]
	public class PlayStoreDeployProcess : DeployProcess
	{
	}

	[Serializable]
	public sealed class PostBuildProcess : PipelineProcess
	{
	}
}