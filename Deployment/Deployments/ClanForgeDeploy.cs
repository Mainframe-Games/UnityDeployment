using System.Net;
using System.Text;
using Deployment.Server.Config;
using Newtonsoft.Json.Linq;
using SharedLib;

namespace Deployment.Deployments;

/// <summary>
/// Docs: https://docs.unity.com/game-server-hosting/manual/legacy/update-your-game-via-the-clanforge-api#Managed_Game_Server_Hosting_(Clanforge)_API
/// </summary>
public class ClanForgeDeploy
{
	private const string BASE_URL = "https://api.multiplay.co.uk/cfp/v1";
	private const int POLL_TIME = 3000;
	
	/// <summary>
	/// A base64 encoded string of '{AccessKey}:{SecretKey}'
	/// </summary>
	private string AuthToken { get; }
	private uint ASID { get; }
	private uint MachineId { get; }
	private uint ImageId { get; }
	private string Desc { get; }
	private string Url { get; }

	public ClanForgeDeploy(ClanforgeConfig? clanforgeConfig, string desc)
	{
		if (clanforgeConfig == null)
			throw new NullReferenceException($"Param {nameof(clanforgeConfig)} can not be null");

		var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clanforgeConfig.AccessKey}:{clanforgeConfig.SecretKey}"));
		AuthToken = $"Basic {base64}";
		ASID = clanforgeConfig.Asid;
		MachineId = clanforgeConfig.MachineId;
		ImageId = clanforgeConfig.ImageId;
		Url = Uri.EscapeDataString(clanforgeConfig.Url ?? string.Empty);
		Desc = desc;
	}

	/// <summary>
	/// Entry build for deploying build to clanforge.
	/// Must be done after Steam Deploy
	/// </summary>
	public async Task Deploy()
	{
		// create image
		Console.WriteLine("...creating new image");
		var updateId = await CreateNewImage();

		// poll for when diff is ready
		Console.WriteLine("...polling image status");
		await PollStatus("imageupdate", $"updateid={updateId}");

		// generate diff
		Console.WriteLine("...generating diff");
		var diffId = await GenerateDiff();

		// poll for diff status
		Console.WriteLine("...polling diff status");
		await PollStatus("imagediff", $"diffid={diffId}");
		
		// accept diff and create new image version
		await CreateImageVersion(diffId);
	}

	#region Requests

	/// <summary>
	/// Docs: https://docs.unity.com/game-server-hosting/en/manual/api/endpoints/image-update-create
	/// </summary>
	/// <returns>updateid</returns>
	/// <exception cref="WebException"></exception>
	private async Task<int> CreateNewImage()
	{
		var url = $"{BASE_URL}/imageupdate/create?imageid={ImageId}&desc=\"{Desc}\"&machineid={MachineId}&accountserviceid={ASID}&url={Url}";
		var res = await SendRequest(url);
		var content = JObject.Parse(res.Content);
		return content["updateid"]?.Value<int>() ?? -1;
	}

	/// <summary>
	/// Docs: https://docs.unity.com/game-server-hosting/en/manual/api/endpoints/image-update-status
	/// </summary>
	/// <returns>success</returns>
	/// <exception cref="WebException"></exception>
	private async Task PollStatus(string path, string paramStr)
	{
		var url = $"{BASE_URL}/{path}/status?accountserviceid={ASID}&{paramStr}";
		var isCompleted = false;

		while (!isCompleted)
		{
			var res = await SendRequest(url);
			var content = JObject.Parse(res.Content);
			var stateName = content["jobstatename"]?.ToString();
			Console.WriteLine($"...{path} status: {stateName}");
			isCompleted = stateName == "Completed";

			if (isCompleted)
				ThrowIfNotSuccess(content);
			else
				await Task.Delay(POLL_TIME);
		}
	}

	private static void ThrowIfNotSuccess(JObject content)
	{
		var success = content["success"]?.Value<bool>() ?? false;
		if (!success)
			throw new WebException($"Status failed. Please check ClanForge dashboard for more information. {content}");
	}

	/// <summary>
	/// Docs: https://docs.unity.com/game-server-hosting/en/manual/api/endpoints/image-diff-create
	/// </summary>
	/// <returns>diffid</returns>
	private async Task<int> GenerateDiff()
	{
		var url = $"{BASE_URL}/imagediff/create?imageid={ImageId}&machineid={MachineId}&accountserviceid={ASID}";
		var res = await SendRequest(url);
		var content = JObject.Parse(res.Content);
		return content["diffid"]?.Value<int>() ?? -1;
	}

	/// <summary>
	/// Docs: docs.unity.com/game-server-hosting/en/manual/api/endpoints/image-create-version
	/// </summary>
	private async Task CreateImageVersion(int diffId)
	{
		var url = $"{BASE_URL}/imageversion/create?diffid={diffId}&accountserviceid={ASID}&restart=0&game_build=\"{Desc}\"";
		var res = await SendRequest(url);
		var content = JObject.Parse(res.Content);
		ThrowIfNotSuccess(content);
	}
	
	/// <summary>
	/// Helper wrapper for sending web requests with auth token and header with error throwing
	/// </summary>
	/// <param name="url"></param>
	/// <returns></returns>
	/// <exception cref="WebException"></exception>
	private async Task<Web.Response> SendRequest(string url)
	{
		var res = await Web.SendAsync(HttpMethod.Get, url, AuthToken, headers: (HttpRequestHeader.ContentType, "application/x-www-form-urlencoded"));
		
		if (res.StatusCode != HttpStatusCode.OK)
			throw new WebException(res.Reason);

		return res;
	}
	
	#endregion
}