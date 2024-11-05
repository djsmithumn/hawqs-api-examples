namespace HawqsApiExamples.Models;

public class ApiProjectResult
{
	public dynamic RequestData { get; set; }
	public ApiProjectOutputStatus Status { get; set; }
	public List<ApiOutputFile> Output { get; set; }
	public List<ApiScenarioOutput> Scenarios { get; set; }
}

public class ApiScenarioResult
{
	public dynamic RequestData { get; set; }
	public ApiScenarioOutputStatus Status { get; set; }
	public List<ApiOutputFile> Output { get; set; }
}

public class ApiScenarioOutput
{
	public int Id { get; set; }
	public string Url { get; set; }
	public string Name { get; set; }
	public bool HasRun { get; set; }
	public List<ApiOutputFile> Output { get; set; }
}

public class ApiProjectOutputStatus
{
	public bool IsCreated { get; set; }
	public bool AreHruSettingsCorrectForIclus { get; set; }
	public int Progress { get; set; }
	public string Message { get; set; }
	public string ErrorStackTrace { get; set; }
}

public class ApiScenarioOutputStatus
{
	public bool HasRun { get; set; }
	public bool CanUseIclus { get; set; }
	public int Progress { get; set; }
	public string Message { get; set; }
	public string ErrorStackTrace { get; set; }
}

public class ApiOutputFile
{
	public string Name { get; set; }
	public string Url { get; set; }
	public string Format { get; set; }
}
