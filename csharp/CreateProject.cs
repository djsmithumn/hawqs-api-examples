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
public class CreateProject : ICommandAction
{
	public const string CommandName = "--create-project";

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
				units = "km2",
				exemptLanduse = new List<string> { "AGWF", "AGWR", "AGWT", "RIWF", "RIWN", "UPWF", "UPWN", "WATR", "WETF", "WETL", "WETN" },
				noAreaRedistribution = new List<string> { "AGWF", "AGWR", "AGWT", "RIWF", "RIWN", "UPWF", "UPWN", "WATR", "WETF", "WETL", "WETN" }
			}
		};

		using var client = new HttpClient();
		var createProjectPostMessage = new HttpRequestMessage(HttpMethod.Post, $"{appSettings.BaseUrl}/builder/project/create-only");
		createProjectPostMessage.Headers.Add("X-API-Key", appSettings.ApiKey);
		createProjectPostMessage.Content = new StringContent(JsonConvert.SerializeObject(projectRequestData), Encoding.UTF8, "application/json");
		var createProjectPostResult = await client.SendAsync(createProjectPostMessage);

		if (!createProjectPostResult.IsSuccessStatusCode)
		{
			Console.WriteLine($"Error sending project creation API request: {createProjectPostResult.StatusCode}, {createProjectPostResult.ReasonPhrase}");
			return 1;
		}

		var submissionStr = await createProjectPostResult.Content.ReadAsStringAsync();
		var submissionResult = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(submissionStr);
		if (submissionResult == null || !submissionResult.ContainsKey("url"))
		{
			Console.WriteLine($"Unexpected API POST response: {submissionStr}");
			return 1;
		}

		int projectRequestId = (int)submissionResult["id"];
		Console.WriteLine($"Project request ID {projectRequestId} submitted");

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
