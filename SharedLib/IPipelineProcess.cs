namespace SharedLib;

public interface IPipelineProcess
{
	public string Name { get; }
	public Task<ProcessResult> ProcessesAsync();
}

public struct ProcessResult
{
	public ProcessStatus Status;
	public string Error;
}

public enum ProcessStatus
{
	/// <summary>
	/// Waiting to be processes
	/// </summary>
	Queued,
	/// <summary>
	/// Currently being processes
	/// </summary>
	Pending,
	/// <summary>
	/// Successfully completed
	/// </summary>
	Success,
	
	/// <summary>
	/// Failed to complete
	/// </summary>
	Failed
}