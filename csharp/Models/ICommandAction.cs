namespace HawqsApiExamples.Models;

/// <summary>
/// Interface to streamline definition of command line actions.
/// </summary>
public interface ICommandAction
{
	const string CommandName = "";
	Task<int> RunAsync(string[] args, AppSettings appSettings);
}
