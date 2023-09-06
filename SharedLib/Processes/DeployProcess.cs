namespace SharedLib.Processes;

public abstract class DeployProcess : IProcessable2, IOffloadable2
{
	private readonly BuildProcess[] _buildProcesses;
	
	public string Name { get; }
	public bool IsOffloaded { get; }

	protected DeployProcess(params BuildProcess[] buildProcesses)
	{
		_buildProcesses = buildProcesses;
	}

	public async Task<ProcessResult> ProcessesAsync()
	{
		try
		{
			foreach (var process in _buildProcesses)
				await process.ProcessesAsync();

			return ProcessResult.Success;
		}
		catch (Exception e)
		{
			Logger.Log(e);
			return new ProcessResult(ProcessStatus.Failed, e.Message);
		}
	}

}