﻿using Deployment;
using Deployment.RemoteBuild;
using SharedLib;

namespace Server.RemoteBuild;

public class RemoteBuildWorkspaceRequest : IRemoteControllable
{
	public string? WorkspaceName { get; set; }
	public string[]? Args { get; set; }
	
	public string Process()
	{
		var mapping = new WorkspaceMapping();
		var workspaceName = mapping.GetRemapping(WorkspaceName);
		var workspace = Workspace.GetWorkspaceFromName(workspaceName);
		Logger.Log($"Chosen workspace: {workspace}");
		workspace.Update();

		if (BuildPipeline.Current != null)
			throw new Exception($"A build process already active. {BuildPipeline.Current.Workspace}");
		
		App.RunBuildPipe(workspace, Args).FireAndForget();
		var changeSetId = workspace.GetCurrentChangeSetId();
		return $"{workspace.Name} | {workspace.UnityVersion} | changeSetId: {changeSetId}";
	}
}