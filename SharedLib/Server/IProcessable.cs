﻿namespace SharedLib.Server;

public interface IProcessable
{
	Task<ServerResponse> ProcessAsync();
}

public interface IProcessable<in T>
{
	Task<ServerResponse> ProcessAsync(T context);
}

public interface IProcessable<in T0, in T1>
{
	Task<ServerResponse> ProcessAsync(T0 context0, T1 context1);
}

public interface IProcessable<in T0, in T1, in T2>
{
	Task<ServerResponse> ProcessAsync(T0 context0, T1 context1, T2 context2);
}