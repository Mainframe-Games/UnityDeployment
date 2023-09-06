namespace SharedLib.Processes;

public abstract class BuildProcess : IProcessable2, IOffloadable2
{
	public string Name { get; }
	public bool IsOffloaded { get; }

	protected BuildProcess(string name)
	{
		Name = name;
	}
	
	public virtual async Task<ProcessResult> ProcessesAsync()
	{
		if (IsOffloaded)
		{
		}
				
		return ProcessResult.Success;
	}
}