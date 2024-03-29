﻿using System.Reflection;
using Deployment;
using Deployment.Deployments;
using Deployment.Server.Unity;
using Server.Configs;
using Server.RemoteDeploy;
using SharedLib;
using SharedLib.Server;

namespace Server;

public static class App
{
	public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
	private static string? RootDirectory { get; set; }
	private static ListenServer? Server { get; set; }
	public static ServerConfig? Config { get; private set; }
	private static bool IsLocal { get; set; }

	private static ulong NextPipelineId { get; set; }
	public static readonly Dictionary<ulong, BuildPipeline> Pipelines = new();

	public static async Task RunAsync(Args args)
	{
		Config = ServerConfig.Load();
		RootDirectory = Environment.CurrentDirectory;
		IsLocal = args.IsFlag("-local");

		// for locally running the build process without a listen server
		if (IsLocal)
		{
			Logger.Log($"App Version: {Version}");


			var isGit = args.IsFlag("-git");
			var isPlastic = args.IsFlag("-plastic");
			
			// if no flags are set, ask user
			if (!isGit && !isPlastic)
			{
				Cmd.Choose("Git or Plastic SCM?", ["Git", "Plastic SCM"], out var i);
				switch (i)
				{
					case 0:
						isGit = true;
						break;
					case 1:
						isPlastic = true;
						break;
				}
			}

			BuildPipeline? buildPipeline = null;
			
			if (isGit && !InitializeGit(args, out buildPipeline))
				return;

			if (isPlastic && !InitializePlastic(args, out buildPipeline))
				return;
			
			if (buildPipeline is null)
				throw new NullReferenceException();
				
			await RunBuildPipe(buildPipeline);
		}
		else
		{
			args.TryGetArg("-ip", out var ip, Config.IP);
			args.TryGetArg("-port", out var port, Config.Port.ToString());
			Server = new ListenServer(ip, ushort.Parse(port), Assembly.GetExecutingAssembly());
			CheckIfServerStillListening();
			await Task.Delay(-1);
			Logger.Log("Server stopped");
		}
	}

	private static bool InitializePlastic(Args args, out BuildPipeline? buildPipeline)
	{
		buildPipeline = null;
			
		if (!PlasticWorkspace.TryAskWorkspace(out var workspace))
		{
			Logger.Log("No Workspace chosen");
			return false;
		}
		
		buildPipeline = CreateBuildPipeline(workspace, args);
		return true;
	}

	private static bool InitializeGit(Args args, out BuildPipeline? buildPipeline)
	{
		if (!args.TryGetArg("-git", out var gitDirPath))
			gitDirPath = Cmd.Ask("Git directory: ", string.Empty);

		buildPipeline = null;
		
		var gitDir = new DirectoryInfo(gitDirPath);
		if (!gitDir.Exists)
		{
			Logger.Log($"Directory does not exist: {gitDirPath}");
			return false;
		}
						
		var childDirs = gitDir.GetDirectories();
		if (childDirs.All(x => x.Name != ".git"))
		{
			Logger.Log("No .git directory found");
			return false;
		}
						
		var gitWorkspace = new GitWorkspace(gitDir.Name, gitDir.FullName);
		buildPipeline = CreateBuildPipeline(gitWorkspace, args);
		return true;
	}

	public static void DumpLogs(bool clearConsole = true)
	{
		Logger.WriteToFile(RootDirectory, clearConsole);
		CheckIfServerStillListening();
		Environment.CurrentDirectory = RootDirectory; // reset cur dir back to root of exe
	}
	
	private static void CheckIfServerStillListening()
	{
		if (Server is null)
			return;
		
		if (Server.IsListening is not true)
			throw new Exception("Server died");

		Logger.Log($"Server Listening on '{Server.Prefixes}'");
		Logger.Log($"App Version: {Version}");
	}

	#region Build Pipeline

	public static BuildPipeline CreateBuildPipeline(Workspace workspace, Args args)
	{
		var pipelineId = NextPipelineId++;
		Offloader? offloader = null;
		
		if (Config?.Offload is not null)
		{
			offloader = new Offloader
			{
				Url = Config.Offload.Url,
				Targets = Config.Offload.Targets,
				SendBackUrl = $"http://{Config.IP}:{Config.Port}",
				WorkspaceName = workspace.Name,
				WorkspaceBranch = workspace.Branch,
				PipelineId = pipelineId,
				XcodeConfig = Config.AppleStore
			};
		}
		
		var pipeline = new BuildPipeline(pipelineId, workspace, args, offloader);
		
		// TODO: this should maybe be set in the BuildPipeline ctor but BuildConfig is not in Builds namespace
		if (offloader is not null)
			offloader.BuildConfig = pipeline.Config;
		
		return pipeline;
	}
	
	public static async Task RunBuildPipe(BuildPipeline pipeline)
	{
		Pipelines.Add(pipeline.Id, pipeline);
		
		pipeline.GetExtraHookLogs += BuildPipelineOnGetExtraHookLog;
		pipeline.DeployEvent += BuildPipelineOnDeployEvent;
		
		var isSuccessful = await pipeline.RunAsync();
		
		if (isSuccessful)
			DumpLogs();
		
		Pipelines.Remove(pipeline.Id);
	}

	private static async Task<bool> BuildPipelineOnDeployEvent(BuildPipeline pipeline)
	{
		try
		{
			var fullVersion = pipeline.BuildVersions?.FullVersion ?? string.Empty;
			
			// client deploys
			await DeployApple(pipeline); // apple first as apple takes longer to process on appstore connect
			await DeployGoogle(pipeline, fullVersion);
			DeploySteam(pipeline, fullVersion);

			/*
			 * Note: clanforge relies on steam URL, so steam MUST be updated first
			 */
			
			// server deploys
			await DeployToS3Bucket(pipeline);
			await DeployClanforge(pipeline, fullVersion);
			
			return true;
		}
		catch (Exception e)
		{
			Logger.Log(e);
			pipeline?.SendErrorHook(e);
			return false;
		}
	}

	private static async Task DeployToS3Bucket(BuildPipeline pipeline)
	{
		if (Config?.S3 == null || pipeline.Config.Deploy?.S3 is not true)
			return;
		
		// upload to s3
		var pathToBuild = pipeline.Workspace.GetBuildTarget($"{BuildTargetFlag.Linux64}_Server").BuildPath;
		var s3 = new AmazonS3Deploy(Config.S3.AccessKey, Config.S3.SecretKey, Config.S3.BucketName);
		await s3.DeployAsync(pathToBuild);
		
		if (Config.Ugs?.ServerHosting == null)
			return;

		if (Config.Ugs.ServerHosting.BuildId == 0)
			throw new Exception("Invalid build Id");

		var project = Config.Ugs.GetProjectFromName(pipeline.Workspace.Name);
		var gameServer = new UnityGameServerRequest(Config.Ugs.KeyId, Config.Ugs.SecretKey);
		await gameServer.CreateNewBuildVersion(
			project.ProjectId,
			project.EnvironmentId,
			Config.Ugs.ServerHosting.BuildId,
			Config.S3.Url,
			Config.S3.AccessKey,
			Config.S3.SecretKey);

		Logger.Log("Unity server updated");
	}

	private static async Task DeployClanforge(BuildPipeline pipeline, string buildVersionTitle)
	{
		if (pipeline.Config.Deploy?.Clanforge is null or false)
			return;

		pipeline.Args.TryGetArg("-setlive", out var beta, Config.Steam.DefaultSetLive);
		pipeline.Args.TryGetArg("-clanforge", out var profile, Config.Clanforge.DefaultProfile);
		var isFull = pipeline.Args.IsFlag("-full");
		var clanforge = new ClanForgeDeploy(Config.Clanforge, profile, buildVersionTitle, beta, isFull);
		await clanforge.Deploy();
	}

	private static async Task DeployApple(BuildPipeline pipeline)
	{
		if (pipeline.Config.Deploy?.AppleStore is not true || !OperatingSystem.IsMacOS())
			return;

		var apple = new RemoteAppleDeploy
		{
			WorkspaceName = pipeline.Workspace.Name,
			Config = Config.AppleStore,
		};
		
		await apple.ProcessAsync();
	}

	private static async Task DeployGoogle(BuildPipeline pipeline, string buildVersionTitle)
	{
		if (pipeline.Config.Deploy?.GoogleStore is not true)
			return;
		
		var packageName = pipeline.Workspace.ProjectSettings.GetValue<string>("applicationIdentifier.Android");
		var changeLogArr = pipeline.ChangeLog;
		var changeLog = string.Join("\n", changeLogArr);
		var buildSettingsAsset = pipeline.Workspace.GetBuildTarget(BuildTargetFlag.Android.ToString());
		var productName = buildSettingsAsset.GetValue<string>("ProductName");
		var buildPath = buildSettingsAsset.GetValue<string>("BuildPath");
		var path = Path.Combine(buildPath, $"{productName}.aab");
		var aabFile = new FileInfo(path);

		if (!aabFile.Exists)
			throw new FileNotFoundException($"aab file not found: {path}");

		await GooglePlayDeploy.Deploy(
			packageName,
			aabFile.FullName,
			Config.GoogleStore.CredentialsPath,
			Config.GoogleStore.ServiceUsername,
			buildVersionTitle,
			changeLog);
	}

	private static void DeploySteam(BuildPipeline pipeline, string buildVersionTitle)
	{
		var vdfPaths = pipeline.Config.Deploy?.Steam;
		
		if (vdfPaths == null)
			return;
		
		if (Config?.Steam is null)
			throw new NullReferenceException("Steam config is null");
		
		pipeline.Args.TryGetArg("-setlive", out var setLive, Config.Steam.DefaultSetLive);
		
		foreach (var vdfPath in vdfPaths)
		{
			var path = Config.Steam.Path;
			var password = Config.Steam.Password;
			var username = Config.Steam.Username;
			var steam = new SteamDeploy(vdfPath, password, username, path);
			steam.Deploy(buildVersionTitle, setLive);
		}
	}

	private static string? BuildPipelineOnGetExtraHookLog(BuildPipeline pipeline)
	{
		try
		{
			if (Config?.Clanforge != null)
			{
				pipeline.Args.TryGetArg("-clanforge", out var profile, Config.Clanforge.DefaultProfile);
				return Config.Clanforge.BuildHookMessage(profile, "Updated");
			}
		}
		catch (Exception e)
		{
			Logger.Log(e);
		}
		
		return null;
	}
	
	#endregion
}