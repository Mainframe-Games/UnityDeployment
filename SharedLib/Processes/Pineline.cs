namespace SharedLib.Processes;

public class Pineline : IProcessable2
{
	private readonly IProcessable2[] _processes;

	public string Name => nameof(Pineline);
	
	public Pineline(params IProcessable2[] processes)
	{
		_processes = processes;
	}
	
	public async Task<ProcessResult> ProcessesAsync()
	{
		try
		{
			foreach (var process in _processes)
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