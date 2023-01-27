﻿using Deployment;
using Deployment.Server;

try
{
	var config = ServerConfig.Load();

	if (config.RunServer)
	{
		var server = new ListenServer(config.IP, config.Port);

		if (config.AuthTokens is { Count: > 0 })
		{
			server.GetAuth = () =>
			{
				config.Refresh();
				return config.AuthTokens ?? Enumerable.Empty<string>();
			};
		}
		
		await server.RunAsync();
		Console.WriteLine("Server stopped");
	}
	else
	{
		var currentWorkspace = Workspace.GetWorkspace();
		Console.WriteLine($"Chosen workspace: {currentWorkspace}");
		var pipe = new BuildPipeline(currentWorkspace, args);
		await pipe.RunAsync();
	}
}
catch (Exception e)
{
	Console.WriteLine(e);
}

Console.Read();