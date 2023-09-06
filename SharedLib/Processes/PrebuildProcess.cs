namespace SharedLib.Processes;

public class PrebuildProcess : IProcessable2
{
	public string Name { get; }
	public async Task<ProcessResult> ProcessesAsync()
	{
		await Task.CompletedTask;
		return ProcessResult.Success;
	}
}