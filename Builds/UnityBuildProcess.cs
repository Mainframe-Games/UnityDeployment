using Deployment;
using SharedLib;
using SharedLib.Processes;

namespace Builds;

public class UnityBuildProcess : BuildProcess
{
	private readonly Workspace _workspace;
	private readonly BuildSettingsAsset _buildSettings;

	public UnityBuildProcess(Workspace workspace, BuildSettingsAsset buildSettings) : base(buildSettings.Name)
	{
		_workspace = workspace;
		_buildSettings = buildSettings;
	}

	public override async Task<ProcessResult> ProcessesAsync()
	{
		await Task.CompletedTask;
		
		var unity = new LocalUnityBuild(_workspace);
		var res = unity.Build(_buildSettings);

		if (res.IsErrors)
			return new ProcessResult(ProcessStatus.Failed, res.Errors);

		return ProcessResult.Success;
	}
}