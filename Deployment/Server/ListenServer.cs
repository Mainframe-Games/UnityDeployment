﻿using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using SharedLib;

namespace Deployment.Server;

public class ListenServer
{
	private readonly HttpListener _listener;
	public Func<List<string>>? GetAuth { get; set; }

	private readonly string _ip;
	private readonly ushort _port;

	public ListenServer(string ip, ushort port = 8080)
	{
		_ip = ip;
		_port = port;
		
		_listener = new HttpListener();
		_listener.Prefixes.Add($"http://{ip}:{port}/");
		_listener.Start();
		CheckIfServerStillListening();
	}

	public void CheckIfServerStillListening()
	{
		if (_listener.IsListening)
			Logger.Log($"... Server listening on '{_ip}:{_port}'");
		else
			throw new Exception("Server died");
	}

	public async Task RunAsync()
	{
		Receive();
		await Task.Delay(-1);
	}

	public void Stop()
	{
		_listener.Stop();
	}

	private void Receive()
	{
		_listener.BeginGetContext(ListenerCallback, _listener);
	}

	/// <summary>
	/// Reads from file each time so we can add/remove tokens without restarting server
	/// </summary>
	/// <param name="authToken"></param>
	/// <returns></returns>
	private bool IsAuthorised(string authToken)
	{
		// always return true if no auths have been given
		var authTokens = GetAuth?.Invoke();
		
		if (authTokens == null || authTokens.Count == 0)
			return true;

		foreach (var token in authTokens)
		{
			if (token == authToken)
				return true;
		}

		return false;
	}

	private async void ListenerCallback(IAsyncResult result)
	{
		if (!_listener.IsListening)
			return;

		var context = _listener.EndGetContext(result);
		var request = context.Request;

		// do something with the request
		Logger.Log($"{request.HttpMethod} {request.Url}");
		if (!string.IsNullOrEmpty(request.ContentType))
			Logger.Log($"Content-Type: {request.ContentType}");

		var response = request.HttpMethod switch
		{
			"GET" => await HandleGet(request),
			"POST" => await HandlePost(request),
			_ => throw new WebException($"HttpMethod not supported: {request.HttpMethod}")
		};

		Respond(context, response);
	}

	private static async Task<ServerResponse> HandleGet(HttpListenerRequest request)
	{
		await Task.CompletedTask;
		return new ServerResponse(HttpStatusCode.OK, "ok");
	}

	private async Task<ServerResponse> HandlePost(HttpListenerRequest request)
	{
		// check authorisation
		var authToken = request.Headers[HttpRequestHeader.Authorization.ToString()] ?? string.Empty;
		if (!IsAuthorised(authToken))
			return new ServerResponse(HttpStatusCode.Unauthorized, "You are not authorized to perform this action");

		if (!request.HasEntityBody)
			return new ServerResponse(HttpStatusCode.NoContent, "No body was given in request");
		
		using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
		var jsonStr = await reader.ReadToEndAsync();
		var packet = Json.Deserialise<RemoteBuildPacket>(jsonStr);
		if (packet == null)
			throw new NullReferenceException($"{nameof(RemoteBuildPacket)} is null from json: {jsonStr}");

		try
		{
			var responseMessage = await packet.ProcessAsync();
			return new ServerResponse(HttpStatusCode.OK, responseMessage);
		}
		catch (Exception e)
		{
			Console.Error.WriteLine(e);
			return new ServerResponse(HttpStatusCode.InternalServerError, e.Message);
		}
	}

	private void Respond(HttpListenerContext context, ServerResponse serverResponse)
	{
		try
		{
			Logger.Log(serverResponse.StatusCode);
			var response = context.Response;
			response.StatusCode = (int)serverResponse.StatusCode;
			response.ContentType = "application/json";
			var resJson = serverResponse.StatusCode == HttpStatusCode.OK
				? CreateSuccessResponse(serverResponse)
				: CreateErrorResponse(serverResponse);

			var bytes = Encoding.UTF8.GetBytes(resJson.ToString());
			response.OutputStream.Write(bytes);
			response.OutputStream.Close();

			// start listening again
			Receive();
		}
		catch (Exception e)
		{
			Logger.Log(e);
		}
	}

	private static JObject CreateSuccessResponse(ServerResponse serverResponse)
	{
		return new JObject
		{
			["data"] = serverResponse.Message
		};
	}

	private static JObject CreateErrorResponse(ServerResponse serverResponse)
	{
		return new JObject
		{
			["error"] = new JObject
			{
				["statusCode"] = serverResponse.StatusCode.ToString(),
				["message"] = serverResponse.Message
			}
		};
	}
}