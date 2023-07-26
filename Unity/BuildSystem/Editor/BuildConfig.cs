﻿using System;
using UnityEngine;

namespace BuildSystem
{
	[CreateAssetMenu(fileName = "BuildConfig", menuName = "Build System/New Config", order = 0)]
	public class BuildConfig : ScriptableObject
	{
		public PreBuild PreBuild;
		public Deploy Deploy;
		public WebHook[] Hooks;
	}

	[Serializable]
	public struct PreBuild
	{
		public int BumpIndex;
		public Versions Versions;
	}
	
	[Serializable]
	public struct Versions
	{
		public bool BundleVersion;
		public bool AndroidVersionCode;
		public BuildNumber[] BuildNumbers;
	}

	[Serializable]
	public struct Deploy
	{
		public string[] Steam;
		public bool	AppleStore;
		public bool	GoogleStore;	
		public bool	Clanforge;
		public bool	S3;
	}
	
	[Serializable]
	public struct WebHook
	{
		public string Title;
		public string url;
		public bool IsErrorChannel;
	}

	public enum BuildNumber
	{
		Standalone,
		iPhone,
		Bratwurst,
		tvOS,
		VisionOS,
	}
}