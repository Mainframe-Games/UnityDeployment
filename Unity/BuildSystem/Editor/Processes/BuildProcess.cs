using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace BuildSystem.Processes
{
	[Serializable]
	public class BuildProcess : PipelineProcess
	{
		[Header("File")]
		public string Name;
		
		[Header("Build Config")]
		[Tooltip("File extension. Include '.'")]
		public string Extension;
		public string ProductName;
		public BuildTarget Target = BuildTarget.StandaloneWindows64;
		public BuildTargetGroup TargetGroup = BuildTargetGroup.Standalone;
		public StandaloneBuildSubtarget SubTarget = StandaloneBuildSubtarget.Player;
		[Tooltip("Location to build player. Can use -buildPath CLI param to override")]
		public string BuildPath = "Builds/";
		[Tooltip("Custom scenes overrides. Empty array will use EditorSettings.Scenes")]
		public SceneAsset[] Scenes;
		[FormerlySerializedAs("ScriptingDefines")] 
		[Tooltip("Custom define overrides. Empty array will use ProjectSettings defines")]
		public string[] ExtraScriptingDefines;
		public string AssetBundleManifestPath;
		public BuildOptions BuildOptions = BuildOptions.None;
	}
}