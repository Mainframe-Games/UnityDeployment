﻿using Deployment.ChangeLogBuilders;
using Deployment.Configs;
using Deployment.Deployments;
using Deployment.PreBuild;
using Deployment.RemoteBuild;
using Deployment.Server;
using Deployment.Webhooks;
using SharedLib;

namespace Deployment;

public class BuildPipeline
{
	public static BuildPipeline? Current { get; private set; }
	
	private readonly Workspace _workspace;
	private readonly string[]? _args;
	private readonly BuildConfig? _config;
	private readonly PreBuildBase? _preBuild;
	private readonly LocalUnityBuild? _unity;

	public Workspace Workspace => _workspace;

	private string BuildVersionTitle => $"Build Version: {_preBuild?.BuildVersion}";

	public BuildPipeline(Workspace workspace, string[]? args)
	{
		_workspace = workspace;
		_args = args;
		_config = GetConfigJson(workspace.Directory);
		_unity = new LocalUnityBuild(workspace.UnityVersion);
		_preBuild = GetPreBuildClass(_config.PreBuildType);
		Environment.CurrentDirectory = workspace.Directory;
		Current = this;
	}

	private static PreBuildBase? GetPreBuildClass(PreBuildType preBuildType)
	{
		return preBuildType switch
		{
			PreBuildType.None => null,
			PreBuildType.Major_Minor => new PreBuild_Major_Minor(),
			PreBuildType.Major_ChangeSetId => new PreBuild_Major_ChangeSetId(),
			_ => throw new ArgumentOutOfRangeException(nameof(preBuildType), preBuildType, null)
		};
	}

	#region Build Steps
	
	public async Task RunAsync()
	{
		var startTime = DateTime.Now;
		await Prebuild();
		await Build();
		await PostBuild();
		Console.WriteLine($"Deployed. {DateTime.Now - startTime:hh\\:mm\\:ss}");
	}

	private async Task Prebuild()
	{
		if (IsFlag("-noprebuild"))
			return;

		if (_preBuild == null)
			throw new Exception("PreBuild class is null");
		
		Console.WriteLine("PreBuild process started...");
		_preBuild.Run();
		await Task.CompletedTask;
	}
	
	private async Task Build()
	{
		if (_config == null || _unity == null)
			throw new NullReferenceException();
		
		Console.WriteLine("Build process started...");

		var buildMap = new Dictionary<BuildContainer, List<string>>();
		
		// configs
		foreach (var build in _config.Builds)
		{
			// ensure correct appId is set
			await File.WriteAllTextAsync("steam_appid.txt", build.Steam.SteamId.ToString());

			// Build
			buildMap[build] = new List<string>();
			var targets = build.Targets;

			if (targets.Length == 0)
				throw new Exception("targets must be assigned in config");

			foreach (var target in targets)
			{
				if (!IsFlag("-nobuild"))
				{
					bool success;
					
					if (!string.IsNullOrEmpty(target.OffloadUrl))
						success = await _unity.SendRemoteBuildRequest(_workspace, target);
					else
						success = await _unity.Build(target);
	
					if (!success)
						continue;
				}

				if (!target.IgnoreSteamUpload)
					buildMap[build].Add(target.BuildPath);
			}

			await _unity.WaitBuildIds();

			if (_preBuild?.IsRun ?? false)
				_preBuild.CommitNewVersionNumber();
		}

		DeployAsync(buildMap);
	}

	private void DeployAsync( Dictionary<BuildContainer, List<string>> buildMap)
	{
		if (IsFlag("-nosteamdeploy"))
			return;

		foreach (var build in buildMap)
		{
			if (build.Value.Count == 0)
				continue;
			
			// Deploy - Steam
			var steam = new SteamDeploy(build.Key.Steam, ServerConfig.Instance.SteamPath);
			steam.ClearContentFolder();
			steam.Deploy(build.Value, BuildVersionTitle);
		}
	}
	
	private async Task PostBuild()
	{
		if (IsFlag("-nopostbuild"))
			return;
		
		if (_config?.Hooks == null)
			return;
		
		Console.WriteLine("PostBuild process started...");

		var hooks = _config.Hooks;
		var slackHook = hooks.Slack.Url;
		var slackTitle = hooks.Slack.Title;
		var discordHook = hooks.Discord.Url;
		var discordBotName = hooks.Discord.Title;
	
		var commits = _preBuild?.ChangeLog.Split(Environment.NewLine) ?? Array.Empty<string>();

		var discord = new ChangeLogBuilderDiscord();
		var isDiscordLog = discord.BuildLog(commits);
		if (isDiscordLog && !string.IsNullOrEmpty(discordHook))
			Discord.PostMessage(discordHook, discord.ToString(), discordBotName, BuildVersionTitle, Discord.Colour.GREEN);

		var steam = new ChangeLogBuilderSteam();
		var isSteamLog = steam.BuildLog(commits);

		if (isSteamLog && !string.IsNullOrEmpty(slackHook))
			Slack.PostMessage(slackHook, $"{slackTitle}.\n```{steam}```");

		await Task.CompletedTask;
	}
	
	#endregion

	#region Helper Methods

	private bool IsFlag(string flag)
	{
		return _args?.Contains(flag) ?? false;
	}

	private static BuildConfig GetConfigJson(string workingDirectory)
	{
		var path = Path.Combine(workingDirectory, "BuildScripts", "buildconfig.json");
		var configStr = File.ReadAllText(path);
		var configClass = Json.Deserialise<BuildConfig>(configStr);

		if (configClass == null)
			throw new NullReferenceException("Failed to parse buildconfig.json");

		configClass.Validate();
		return configClass;
	}

	public void RemoteBuildReceived(RemoteBuildResponse remoteBuildResponse)
	{
		_unity?.RemoteBuildReceived(remoteBuildResponse);
	}
	
	#endregion

}