﻿using System.Net;
using Deployment.RemoteBuild;
using SharedLib;
using SharedLib.BuildToDiscord;
using SharedLib.Server;

namespace Server.RemoteBuild;

public class RemoteBuildWorkspaceRequest : IProcessable
{
	public string? WorkspaceName { get; set; }
	public string? Args { get; set; }
	public string? DiscordAddress { get; set; }
	public ulong CommandId { get; set; }
	
	public ServerResponse Process()
	{
		var args = new Args(Args);
		args.TryGetArg("-branch", out var branch, "main");

		var workspaceName =  new WorkspaceMapping().GetRemapping(WorkspaceName);
		var workspace = Workspace.GetWorkspaceFromName(workspaceName);
		
		if (workspace is null)
			return new ServerResponse(HttpStatusCode.BadRequest, $"Given namespace is not valid: {WorkspaceName}");
		
		Logger.Log($"Chosen workspace: {workspace}");
		
		workspace.Clear();
		workspace.Update();
		workspace.SwitchBranch(branch);

		var pipeline = App.CreateBuildPipeline(workspace, args);
		pipeline.Report.OnReportUpdated += OnReportUpdated;

		if (pipeline.ChangeLog.Length == 0)
			return new ServerResponse(HttpStatusCode.NotAcceptable, "No changes to build");
		
		App.RunBuildPipe(pipeline).FireAndForget();
		workspace.GetCurrent(out var changeSetId, out var guid);

		var data = new BuildPipelineResponse
		{
			ServerVersion = App.Version,
			PipelineId = pipeline.Id,
			Workspace = workspace.Name,
			WorkspaceMeta = workspace.Meta,
			Targets = string.Join(", ", workspace.GetBuildTargets().Select(x => x.Name)),
			Args = Args,
			UnityVersion = workspace.UnityVersion,
			ChangesetId = changeSetId,
			ChangesetGuid = guid,
			Branch = branch,
			ChangesetCount = pipeline.ChangeLog.Length,
		};
		return new ServerResponse(HttpStatusCode.OK, data);
	}

	private async void OnReportUpdated(PipelineReport report)
	{
		if (string.IsNullOrEmpty(DiscordAddress))
			return;

		var packet = new DiscordServerPacket
		{
			PipelineUpdate = new PipelineUpdateMessage
			{
				CommandId = CommandId,
				Report = report,
			}
		};
		await Web.SendAsync(HttpMethod.Post, DiscordAddress, body: packet);
	}
}