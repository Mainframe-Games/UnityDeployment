namespace SharedLib.Processes;

public interface IOffloadable2
{
	/// <summary>
	/// True if build is offloaded to another build server
	/// </summary>
	public bool IsOffloaded { get; }
}