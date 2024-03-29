using SharedLib;

namespace Deployment.Configs;

/// <summary>
/// Build config local to each Unity project
/// </summary>
public sealed class BuildConfig
{
	public MonoBehaviour MonoBehaviour { get; set; }
	public PreBuildConfig? PreBuild => MonoBehaviour.PreBuild;
	public DeployContainerConfig? Deploy => MonoBehaviour.Deploy;
	public HooksConfig[]? Hooks => MonoBehaviour.Hooks;

	public static BuildConfig GetConfig(string workingDirectory)
	{
		var path = Path.Combine(workingDirectory, "Assets", "Settings", "BuildSettings", "BuildConfig.asset");
		var config = Yaml.Deserialise<BuildConfig>(path);
		return config;
	}
}

public class MonoBehaviour
{
	public PreBuildConfig? PreBuild { get; set; }
	public DeployContainerConfig? Deploy { get; set; }
	public HooksConfig[]? Hooks { get; set; }
}

public class PreBuildConfig
{
	public bool? BuildNumberStandalone { get; set; }
	public bool? BuildNumberIphone { get; set; }
	public bool? AndroidVersionCode { get; set; }
}

public class DeployContainerConfig
{
	public string[]? Steam { get; set; }
	public bool? Clanforge { get; set; }
	public bool? AppleStore { get; set; }
	public bool? GoogleStore { get; set; }
	public bool? S3 { get; set; }
}

public class HooksConfig
{
	public string? Url { get; set; }
	public string? Title { get; set; }
	public bool? IsErrorChannel { get; set; }

	public bool IsDiscord() => Url?.StartsWith("https://discord.com/") ?? false;
	public bool IsSlack() => Url?.StartsWith("https://hooks.slack.com/") ?? false;
}