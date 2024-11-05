namespace HawqsApiExamples.Models;

/// <summary>
/// Class mapping to AppSettings in appsettings.json.
/// </summary>
public class AppSettings
{
	/// <summary>
	/// The API key to use for the HAWQS API.
	/// </summary>
	public string ApiKey { get; set; }

	/// <summary>
	/// The base URL for the HAWQS API, such as https://dev-api.hawqs.tamu.edu.
	/// </summary>
	public string BaseUrl { get; set; }

	/// <summary>
	/// The path to the directory where the project and scenario files will be saved.
	/// </summary>
	public string SavePath { get; set; }
}
