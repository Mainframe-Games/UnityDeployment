﻿using System.Diagnostics;
using Deployment.Configs;
using SharedLib;

namespace Builder;

/// <summary>
/// Src: https://github.com/VeriorPies/ParrelSync
/// </summary>
public static class ClonesManager
{
	private static readonly HashSet<string> Links = new()
	{
		"Assets",
		"Packages",
		"ProjectSettings"
	};

	private static readonly HashSet<string> Copy = new()
	{
		"Library"
	};

	public static async Task<DirectoryInfo[]> CloneProject(DirectoryInfo srcDir, BuildConfig config)
	{
		if (config.Builds == null)
			throw new NullReferenceException("Builds array is null");

		var sw = Stopwatch.StartNew();
		
		// append links
		if (config.Links != null)
			AppendLinks(config.Links);

		var destDirs = new List<string>();

		foreach (var buildTarget in config.Builds)
		{
			// create dir
			var suffix = buildTarget.BuildPath?.Split("/")[^1];
			var targetDir = $"{srcDir.FullName}_{suffix}";
			Directory.CreateDirectory(targetDir);
			destDirs.Add(targetDir);
			
			// links
			foreach (var link in Links)
				LinkFolders(Path.Combine(srcDir.FullName, link), Path.Combine(targetDir, link));
		}

		// copies
		var sources = Copy.Select(x => new DirectoryInfo(Path.Combine(srcDir.FullName, x)));
		var copyByte = GetTotalBytesToCopy(sources);
		var totalBytes = copyByte * destDirs.Count;
		var copiedBytes = 0L;
		var printSize = PrintEx.ToGigaByteString(copyByte, "0.00");
		Console.Write($"Copying directories ({printSize}) to {destDirs.Count} locations ... ");
		
		using var progressBar = new ProgressBar();
		var tasks = new List<Task>();

		foreach (var destDir in destDirs)
		{
			foreach (var copy in Copy)
			{
				var task = Task.Run(() =>
				{
					var source = new DirectoryInfo(Path.Combine(srcDir.FullName, copy));
					var destination = new DirectoryInfo(Path.Combine(destDir, copy));
					CopyDirectoryWithProgressBarRecursive(source, destination, ref totalBytes, ref copiedBytes, progressBar);
				});
				tasks.Add(task);
			}
		}

		await Task.WhenAll(tasks);
		Console.WriteLine();
		Logger.LogTimeStamp("Copying complete", sw);
		sw.Stop();
		return destDirs.Select(x => new DirectoryInfo(x)).ToArray();
	}

	private static long GetTotalBytesToCopy(IEnumerable<DirectoryInfo> sources)
	{
		var totalSize = 0L;
		
		foreach (var source in sources)
			totalSize += GetDirectorySize(source, true);
		
		return totalSize;
	}

	/// <summary>
	/// Copies directory located at sourcePath to destinationPath. Displays a progress bar.
	/// Same as the previous method, but uses recursion to copy all nested folders as well.
	/// </summary>
	/// <param name="source">Directory to be copied.</param>
	/// <param name="destination">Destination directory (created automatically if needed).</param>
	/// <param name="totalBytes">Total bytes to be copied. Calculated automatically, initialize at 0.</param>
	/// <param name="copiedBytes">To track already copied bytes. Calculated automatically, initialize at 0.</param>
	/// <param name="progressBar"></param>
	private static void CopyDirectoryWithProgressBarRecursive(
		DirectoryInfo source,
		DirectoryInfo destination,
		ref long totalBytes,
		ref long copiedBytes,
		ProgressBar progressBar)
	{
		// Directory cannot be copied into itself.
		if (source.FullName.ToLower() == destination.FullName.ToLower())
			throw new Exception("Cannot copy directory into itself.");

		// Calculate total bytes, if required.
		// if (totalBytes == 0)
			// totalBytes = GetDirectorySize(source, true);

		// Create destination directory, if required.
		if (!Directory.Exists(destination.FullName))
			Directory.CreateDirectory(destination.FullName);

		// Copy all files from the source.
		foreach (var file in source.GetFiles())
		{
			try
			{
				file.CopyTo(Path.Combine(destination.ToString(), file.Name), true);
			}
			catch (IOException)
			{
				// Some files may throw IOException if they are currently open in Unity editor.
				// Just ignore them in such case.
			}

			// Account the copied file size.
			copiedBytes += file.Length;

			// Display the progress bar.
			progressBar.Report(copiedBytes / (double)totalBytes);
			progressBar.SetContext($"Copying {file.FullName}");
		}

		// Copy all nested directories from the source.
		foreach (var sourceNestedDir in source.GetDirectories())
		{
			var nextDestingationNestedDir = destination.CreateSubdirectory(sourceNestedDir.Name);
			CopyDirectoryWithProgressBarRecursive(sourceNestedDir, nextDestingationNestedDir, ref totalBytes, ref copiedBytes, progressBar);
		}
	}

	/// <summary>
	/// Calculates the size of the given directory. Displays a progress bar.
	/// </summary>
	/// <param name="directory">Directory, which size has to be calculated.</param>
	/// <param name="includeNested">If true, size will include all nested directories.</param>
	/// <returns>Size of the directory in bytes.</returns>
	private static long GetDirectorySize(DirectoryInfo directory, bool includeNested = false)
	{
		// Calculate size of all files in directory.
		var filesSize = directory.GetFiles().Sum(file => file.Length);

		// Calculate size of all nested directories.
		var directoriesSize = 0L;
		if (includeNested)
		{
			foreach (var nestedDir in directory.GetDirectories())
				directoriesSize += GetDirectorySize(nestedDir, true);
		}

		return filesSize + directoriesSize;
	}

	#region SymLinks

	/// <summary>
	/// Create a link / junction from the original project to it's clone.
	/// </summary>
	/// <param name="sourcePath"></param>
	/// <param name="destinationPath"></param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	private static void LinkFolders(string sourcePath, string destinationPath)
	{
		var platform = Environment.OSVersion.Platform;

		if (Directory.Exists(destinationPath) == false && Directory.Exists(sourcePath))
		{
			switch (platform)
			{
				case PlatformID.Win32S:
				case PlatformID.Win32Windows:
				case PlatformID.Win32NT:
				case PlatformID.WinCE:
					CreateLinkWin(sourcePath, destinationPath);
					break;

				case PlatformID.MacOSX:
					CreateLinkMac(sourcePath, destinationPath);
					break;

				case PlatformID.Unix:
					CreateLinkLinux(sourcePath, destinationPath);
					break;

				case PlatformID.Xbox:
				case PlatformID.Other:
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}
		else
		{
			Logger.Log($"Skipping Asset link, it already exists: {destinationPath}");
		}
	}

	/// <summary>
	/// Creates a symlink between destinationPath and sourcePath (Windows version).
	/// </summary>
	/// <param name="sourcePath"></param>
	/// <param name="destinationPath"></param>
	private static void CreateLinkWin(string sourcePath, string destinationPath)
	{
		var cmd = $"/C mklink /J \"{destinationPath}\" \"{sourcePath}\"";
		StartHiddenConsoleProcess("cmd.exe", cmd);
	}

	/// <summary>
	/// Creates a symlink between destinationPath and sourcePath (Mac version).
	/// </summary>
	/// <param name="sourcePath"></param>
	/// <param name="destinationPath"></param>
	private static void CreateLinkMac(string sourcePath, string destinationPath)
	{
		sourcePath = sourcePath.Replace(" ", "\\ ");
		destinationPath = destinationPath.Replace(" ", "\\ ");
		var command = $"ln -s {sourcePath} {destinationPath}";
		ExecuteBashCommand(command);
	}

	/// <summary>
	/// Creates a symlink between destinationPath and sourcePath (Linux version).
	/// </summary>
	/// <param name="sourcePath"></param>
	/// <param name="destinationPath"></param>
	private static void CreateLinkLinux(string sourcePath, string destinationPath)
	{
		sourcePath = sourcePath.Replace(" ", "\\ ");
		destinationPath = destinationPath.Replace(" ", "\\ ");
		var command = $"ln -s {sourcePath} {destinationPath}";
		ExecuteBashCommand(command);
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Starts process in the system console, taking the given fileName and args.
	/// </summary>
	/// <param name="fileName"></param>
	/// <param name="args"></param>
	private static void StartHiddenConsoleProcess(string fileName, string args)
	{
		var process = Process.Start(fileName, args);
		process.WaitForExit();
	}

	/// <summary>
	/// Thanks to https://github.com/karl-/unity-symlink-utility/blob/master/SymlinkUtility.cs
	/// </summary>
	/// <param name="command"></param>
	private static void ExecuteBashCommand(string command)
	{
		command = command.Replace("\"", "\"\"");

		using var proc = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "/bin/bash",
				Arguments = $"-c \"{command}\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true
			}
		};

		proc.Start();
		proc.WaitForExit();

		if (!proc.StandardError.EndOfStream)
			Logger.Log(proc.StandardError.ReadToEnd());
	}

	private static void AppendLinks(IEnumerable<string> links)
	{
		foreach (var link in links)
			Links.Add(link);
	}

	#endregion
}