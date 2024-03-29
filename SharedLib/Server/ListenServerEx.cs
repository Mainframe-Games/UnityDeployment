﻿using System.Net;

namespace SharedLib.Server;

public static class ListenServerEx
{
	public static T GetPostContent<T>(this HttpListenerContext context)
	{
		if (!context.Request.HasEntityBody)
			return default;
		
		using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
		var jsonStr = reader.ReadToEnd();
		var packet = Json.Deserialise<T>(jsonStr);
		
		if (packet is null)
			throw new NullReferenceException($"{typeof(T).Name} is null from json: {jsonStr}");
		
		// Logger.Log($"Content: {packet}, {jsonStr}");

		return packet;
	}
}