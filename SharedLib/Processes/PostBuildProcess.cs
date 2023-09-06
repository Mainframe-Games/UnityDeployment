namespace SharedLib.Processes;

public class PostBuildProcess : IProcessable2
{
	public string Name { get; }
	public Task<ProcessResult> ProcessesAsync()
	{
		throw new NotImplementedException();
	}
}