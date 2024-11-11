using HawqsApiExamples.Models;
using Newtonsoft.Json;
using System.Text;

namespace HawqsApiExamples;

/// <summary>
/// Create a project with no scenarios.
/// HRU settings match what is used for ICLUS
/// Poll results of project creation every 10 seconds until progress is 100%
/// Save project files to a directory - no scenario has been added or run yet, so these are watershed files such as subbasins, HRUs, and point source samples.
/// </summary>
/// <param name="args">The command line arguments should include the command name.</param>
public class CreateProject : ICommandAction
{
	public const string CommandName = "create-project";

	public async Task<int> RunAsync(string[] args, AppSettings appSettings)
	{
		var pollInterval = TimeSpan.FromSeconds(10); //Define how frequently to check API status

		var projectRequestData = new
		{
			dataset = "HUC8",
			downstreamSubbasin = "07100009",
			setHrus = new
			{
				method = "area",
				target = 1,
				units = "km2"
			}
		};

		//Submit the request to the API
		using var client = new HttpClient();
		var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{appSettings.BaseUrl}/builder/project/create-only");
		postMessage.Headers.Add("X-API-Key", appSettings.ApiKey);
		postMessage.Content = new StringContent(JsonConvert.SerializeObject(projectRequestData), Encoding.UTF8, "application/json");
		var postResult = await client.SendAsync(postMessage);

		if (!postResult.IsSuccessStatusCode)
		{
			Console.WriteLine($"Error sending project creation API request: {postResult.StatusCode}, {postResult.ReasonPhrase}");
			return 1;
		}

		//Read the response and get the project request ID and URL
		var submissionStr = await postResult.Content.ReadAsStringAsync();
		var submissionResult = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(submissionStr);
		if (submissionResult == null || !submissionResult.ContainsKey("url"))
		{
			Console.WriteLine($"Unexpected API POST response: {submissionStr}");
			return 1;
		}

		int projectRequestId = (int)submissionResult["id"];
		Console.WriteLine($"Project request ID {projectRequestId} submitted");

		//Check the status of the project creation until it's complete
		var timer = new PeriodicTimer(pollInterval);
		ApiProjectResult project = await ApiHelpers.GetProjectStatus(client, submissionResult["url"], appSettings.ApiKey);

		while (await timer.WaitForNextTickAsync())
		{
			project = await ApiHelpers.GetProjectStatus(client, submissionResult["url"], appSettings.ApiKey);

			if (project.Status.Progress >= 100)
			{
				Console.WriteLine();
				if (!string.IsNullOrWhiteSpace(project.Status.ErrorStackTrace))
				{
					Console.WriteLine($"Error stack trace: {project.Status.ErrorStackTrace}");
				}
				timer.Dispose();
			}
		}

		//Save project files to disk; will include HRUs CSV, subbasins CSV and watershed files (including point source samples)
		var savePath = Path.Combine(appSettings.SavePath, $"Project_{projectRequestId}");
		if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

		foreach (var file in project.Output)
		{
			Console.WriteLine($"Retrieving and saving {file.Name} ({file.Format})");
			string fileName = file.Url.Split('/').ToList().Last();
			var message = new HttpRequestMessage(HttpMethod.Get, file.Url);
			var result = await client.SendAsync(message);

			using var stream = await result.Content.ReadAsStreamAsync();
			using var fileStream = new FileStream(Path.Combine(savePath, fileName), FileMode.Create);
			await stream.CopyToAsync(fileStream);
		}

		Console.WriteLine($"Project request ID {projectRequestId} creation complete");
		return 0;
	}
}
