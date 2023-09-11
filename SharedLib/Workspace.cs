﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharedLib;

public class Workspace
{
	private const string PROJ_SETTINGS_ASSET = "ProjectSettings.asset";
	private const string BUILD_VERSION_TXT = "build_version.txt";
	
	public string Name { get; }
	public string Directory { get; }
	public string? UnityVersion { get; private set; }
	public string? Branch { get; set; } = "main";
	public WorkspaceMeta? Meta { get; }
	public ProjectSettings ProjectSettings { get; private set; }
	[JsonIgnore] public string ProjectSettingsPath => Path.Combine(Directory, "ProjectSettings", PROJ_SETTINGS_ASSET);
	[JsonIgnore] public string BuildVersionPath => Path.Combine(Directory, "Assets", "StreamingAssets", BUILD_VERSION_TXT);

	private Workspace(string name, string directory)
	{
		Name = name;
		Directory = directory;
		ProjectSettings = new ProjectSettings(ProjectSettingsPath);
		UnityVersion = GetUnityVersion(directory);
		Meta = GetMetaData();
	}
	
	public override string ToString()
	{
		return $"{Name} @ {Directory} | UnityVersion: {UnityVersion}";
	}

	private WorkspaceMeta? GetMetaData()
	{
		var metaPath = Path.Combine(Directory, "BuildScripts", "WorkspaceMeta.json");
		var metaPathNew = Path.Combine(Directory, "BuildSystem", "WorkspaceMeta.json");

		string? fileContents = null;

		if (File.Exists(metaPathNew))
			fileContents = File.ReadAllText(metaPathNew);
		else if (File.Exists(metaPath))
			fileContents = File.ReadAllText(metaPath);

		if (string.IsNullOrEmpty(fileContents))
			return null;
		
		return Json.Deserialise<WorkspaceMeta>(fileContents);
	}
	
	public static List<Workspace> GetAvailableWorkspaces()
	{
		var (exitCode, output) = Cmd.Run("cm", "workspace", logOutput: false);

		if (exitCode != 0)
			throw new Exception(output);

		var workSpacesArray = output.Split(Environment.NewLine);
		var workspaces = new List<Workspace>();

		foreach (var workspace in workSpacesArray)
		{
			var split = workspace.Split('@');
			var name = split[0];
			var path = split[1].Replace(Environment.MachineName, string.Empty).Trim();
			var ws = new Workspace(name, path);
			workspaces.Add(ws);
		}

		return workspaces;
	}

	public static Workspace? AskWorkspace()
	{
		var (exitCode, output) = Cmd.Run("cm", "workspace", logOutput: false);

		if (exitCode != 0)
			throw new Exception(output);

		var workspaces = GetAvailableWorkspaces();
		var workspaceNames = workspaces.Select(x => x.Name).ToList();
		
		if (!Cmd.Choose("Choose workspace", workspaceNames, out var index))
			return null;
		
		var workspace = workspaces[index];
		Logger.Log($"Chosen workspace: {workspace}");
		return workspace;
	}

	public static Workspace? GetWorkspaceFromName(string? workspaceName)
	{
		var workspaces = GetAvailableWorkspaces();
		return workspaces.FirstOrDefault(x => string.Equals(x.Name, workspaceName, StringComparison.OrdinalIgnoreCase));
	}
	
	private static string GetUnityVersion(string? workingDirectory)
	{
		if (workingDirectory == null)
			return string.Empty;
		
		var path = Path.Combine(workingDirectory, "ProjectSettings", "ProjectVersion.txt");
		var txt = File.ReadAllText(path);
		var lines = txt.Split("\n");
		
		foreach (var line in lines)
		{
			if (line.Contains("m_EditorVersion:"))
				return line.Replace("m_EditorVersion:", string.Empty).Trim();
		}

		return string.Empty;
	}
	
	public int GetStandaloneBuildNumber()
	{
		var v = ProjectSettings.GetValue<string>("buildNumber.Standalone");
		return int.TryParse(v, out var num) ? num : 0;
	}
	
	public int GetAndroidBuildCode()
	{
		return ProjectSettings.GetValue<int>("AndroidBundleVersionCode");
	}
	
	public int GetIphoneBuildNumber()
	{
		var v = ProjectSettings.GetValue<string>("buildNumber.iPhone");
		return int.TryParse(v, out var num) ? num : 0;
	}
	
	public string? GetBundleVersion()
	{
		return ProjectSettings.GetValue<string?>("bundleVersion");;
	}

	// public int[] GetVersionArray()
	// {
	// 	var verStr = GetBundleVersion() ?? string.Empty;
	// 	var ver = verStr.Split(".");
	// 	var arr = new int[ver.Length];
	//
	// 	for (int i = 0; i < ver.Length; i++)
	// 		arr[i] = int.Parse(ver[i].Trim());
	//
	// 	return arr;
	// }

	public bool IsIL2CPP(UnityBuildTargetGroup group)
	{
		var val = ProjectSettings.GetValue<int?>($"scriptingBackend.{group}");
		return val == 1;
	}

	private bool TryGetBuildConfig(FileInfo[] assetFiles, string targetFileName, out BuildConfigAsset buildConfig)
	{
		foreach (var file in assetFiles)
		{
			var fileName = file.Name.Replace(file.Extension, string.Empty);
			
			if (targetFileName != fileName) 
				continue;
			
			buildConfig = new BuildConfigAsset(file.FullName);
			return true;
		}

		Logger.Log($"File '{targetFileName}' could not be found in workspace '{Name}'. Files: {string.Join(", ", assetFiles.Select(x => x.Name.Replace(x.Extension, string.Empty)))}");
		buildConfig = null;
		return false;
	}

	public IEnumerable<BuildSettingsAsset> GetBuildTargets()
	{
		var path = Path.Combine(Directory, "Assets", "Settings", "BuildSettings");
		var settingsFiles = new DirectoryInfo(path);
		var assetFiles = settingsFiles.GetFiles("*.asset");

		// TODO: support multiple build configs
		if (TryGetBuildConfig(assetFiles, "BuildConfig", out var buildConfigFile))
		{
			var targets = buildConfigFile.GetObject<JArray>("Build.BuildTargets");
			var guids = targets?.Select(x => x["guid"]?.ToString()).ToList();
			var metas = settingsFiles
				.GetFiles("*.asset.meta")
				.Select(x => new BuildSettingsMeta(x.FullName));
			
			foreach (var assetMetaFile in metas)
			{
				var guid = assetMetaFile.GetValue<string>("guid");
				if (guids?.Contains(guid) is true)
					yield return assetMetaFile.GetParentFile();
			}
		}
		else
		{
			foreach (var assetFile in assetFiles)
			{
				if (assetFile.Name.Contains("BuildSettings_"))
					yield return new BuildSettingsAsset(assetFile.FullName);
			}
		}
	}
	
	public BuildSettingsAsset GetBuildTarget(string name)
	{
		foreach (var asset in GetBuildTargets())
			if (asset.Name == name)
				return asset;

		throw new FileNotFoundException($"Unable to find {nameof(BuildSettingsAsset)} with name '{name}'");
	}
	
	public void Clear()
	{
		Cmd.Run("cm", $"unco -a \"{Directory}\"");
		ProjectSettings = new ProjectSettings(ProjectSettingsPath);
		UnityVersion = GetUnityVersion(Directory);
	}

	/// <summary>
	/// Updates the workspace. 
	/// </summary>
	/// <param name="changeSetId">ChangeSetId to update to. -1 is latest</param>
	public void Update(int changeSetId = -1)
	{
		// get all the latest updates
		Cmd.Run("cm", $"update \"{Directory}\"");
		
		// set to a specific change set
		if (changeSetId > 0)
		{
			var (exitCode, output) = Cmd.Run("cm", $"switch cs:{changeSetId} --workspace=\"{Directory}\"");
			
			if (exitCode != 0 || output.ToLower().Contains("does not exist"))
				throw new Exception($"Plastic update error: {output}");
		}
		
		UnityVersion = GetUnityVersion(Directory);
	}

	public void CleanBuild()
	{
		var rootDir = new DirectoryInfo(Directory);
		
		// delete folders
		var dirs = new[] { "Library", "Builds", "obj" };
		foreach (var directory in rootDir.GetDirectories())
			if (dirs.Contains(directory.Name))
				DeleteIfExist(directory);
		
		// delete files
		var files = new List<FileInfo>();
		files.AddRange(rootDir.GetFiles("*.csproj"));
		files.AddRange(rootDir.GetFiles("*.sln"));
		foreach (var file in files)
			DeleteIfExist(file);
	}

	public void GetCurrent(out int changesetId, out string guid)
	{
		var currentDir = Environment.CurrentDirectory;
		Environment.CurrentDirectory = Directory;
		
		var cmdRes = Cmd.Run(
			"cm", $"find changeset \"where branch='{Branch}'\" \"order by date desc\" \"limit 1\" --format=\"{{changesetid}} {{guid}}\" --nototal",
			logOutput: false);
		
		Environment.CurrentDirectory = currentDir;

		var split = cmdRes.output.Split(' ');
		changesetId = int.TryParse(split[0], out var id) ? id : 0;
		guid = split[1];
	}
	
	/// <summary>
	/// Gets previous changeSetId based on commit message
	/// </summary>
	/// <returns></returns>
	public int GetPreviousChangeSetId(string key)
	{
		var req = Cmd.Run("cm",
			$"find changeset \"where branch='{Branch}' and comment like '%{key}%'\" \"order by date desc\" \"limit 1\" --format=\"{{changesetid}}\" --nototal",
			logOutput: false);
		
		// if empty from branch, try again in 'main'
		if (string.IsNullOrEmpty(req.output))
			req = Cmd.Run("cm", 
				$"find changeset \"where branch='main' and comment like '%{key}%'\" \"order by date desc\" \"limit 1\" --format=\"{{changesetid}}\" --nototal",
				logOutput: false);
		
		return int.TryParse(req.output, out var cs) ? cs : 0;
	}
	
	/// <summary>
	/// Gets all change logs between two changeSetIds
	/// </summary>
	public string[] GetChangeLogInst(int curId, int prevId, bool print = true)
	{
		var dirBefore = Environment.CurrentDirectory;
		Environment.CurrentDirectory = Directory;
		var changeLog = GetChangeLog(curId, prevId, print);
		Environment.CurrentDirectory = dirBefore;
		return changeLog;
	}
	
	/// <summary>
	/// Gets all change logs between two changeSetIds
	/// </summary>
	public static string[] GetChangeLog(int curId, int prevId, bool print = true)
	{
		var raw = Cmd.Run("cm", $"log --from=cs:{prevId} cs:{curId} --csformat=\"{{comment}}\"").output;
		var changeLog = raw.Split(Environment.NewLine).Reverse().ToArray();
		
		if (print)
			Logger.Log($"___Change Logs___\n{string.Join("\n", changeLog)}");
		
		return changeLog;
	}
	
	public void CommitNewVersionNumber(string commitMessage)
	{
		// update in case there are new changes in coming otherwise it will fail
		// TODO: need to find a way to automatically resolve conflicts with cloud
		Update();
		
		var status = Cmd.Run("cm", "status --short").output;
		var files = status.Split(Environment.NewLine);
		var filesToCommit = files
			.Where(x => x.Contains(".vdf") 
			            || x.Contains(PROJ_SETTINGS_ASSET)
			            || x.Contains(BUILD_VERSION_TXT))
			.ToList();
		
		// commit changes
		var filesStr = $"\"{string.Join("\" \"", filesToCommit)}\"";
		Logger.Log($"Commiting new build version \"{commitMessage}\"");
		Cmd.Run("cm", $"ci {filesStr} -c=\"{commitMessage}\"");
	}

	private static void DeleteIfExist(FileSystemInfo fileSystemInfo)
	{
		if (!fileSystemInfo.Exists)
			return;
		
		if (fileSystemInfo is DirectoryInfo directoryInfo)
			directoryInfo.Delete(true);
		else
			fileSystemInfo.Delete();
	}
	
	public void SwitchBranch(string? branchPath)
	{
		var res = Cmd.Run("cm", $"switch {branchPath} --workspace=\"{Directory}\"");
		if (res.exitCode != 0)
			throw new Exception(res.output);
		Branch = branchPath;
	}

	public void SaveBuildVersion(string fullVersion)
	{
		File.WriteAllText(BuildVersionPath, fullVersion);
	}
}