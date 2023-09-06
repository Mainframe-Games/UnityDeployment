namespace SharedLib.Processes;

public interface IProcessable2
{
	public string Name { get; }
	public Task<ProcessResult> ProcessesAsync();
}

public readonly struct ProcessResult
{
	public readonly ProcessStatus Status;
	public readonly string? Error;

	public ProcessResult(ProcessStatus status, string? error = null)
	{
		Status = status;
		Error = error;
	}

	public static readonly ProcessResult Success = new(ProcessStatus.Success);

	public override string ToString()
	{
		return $"{Status} {Error}";
	}
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