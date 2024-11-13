using HawqsApiExamples.Models;

namespace HawqsApiExamples;

/// <summary>
/// Zip entire project with GIS data and save to disk.
/// Must have set writeSwatEditorDb to "access" in at least one scenario in order to get GIS data.
/// </summary>
/// <param name="args">The command line arguments should include the command name followed by project request ID.</param>
public class ZipProject : ICommandAction
{
	public const string CommandName = "zip-project";

	public async Task<int> RunAsync(string[] args, AppSettings appSettings)
	{
		if (args.Length < 2)
		{
			Console.WriteLine("Please provide a project request ID.");
			return 1;
		}

		int projectRequestId = int.Parse(args[1]);
		var pollInterval = TimeSpan.FromSeconds(10); //Define how frequently to check API status

		//Submit the zip project request to the API
		using var client = new HttpClient();
		var zipMessage = new HttpRequestMessage(HttpMethod.Patch, $"{appSettings.BaseUrl}/builder/project/zip/{projectRequestId}");
		zipMessage.Headers.Add("X-API-Key", appSettings.ApiKey);
		var zipResult = await client.SendAsync(zipMessage);

		if (!zipResult.IsSuccessStatusCode)
		{
			Console.WriteLine($"Error sending zip project API request: {zipResult.StatusCode}, {zipResult.ReasonPhrase}");
			return 1;
		}

		//Check the status of the project zipping until it's complete
		var timer = new PeriodicTimer(pollInterval);
		ApiScenarioResult scenario = await ApiHelpers.GetScenarioStatus(client, $"{appSettings.BaseUrl}/builder/project/{projectRequestId}", appSettings.ApiKey);

		while (await timer.WaitForNextTickAsync())
		{
			scenario = await ApiHelpers.GetScenarioStatus(client, $"{appSettings.BaseUrl}/builder/project/{projectRequestId}", appSettings.ApiKey);

			if (scenario.Status.Progress >= 100)
			{
				Console.WriteLine();
				if (!string.IsNullOrWhiteSpace(scenario.Status.ErrorStackTrace))
				{
					Console.WriteLine($"Error stack trace: {scenario.Status.ErrorStackTrace}");
				}
				timer.Dispose();
			}
		}

		//Save project files to disk
		var savePath = Path.Combine(appSettings.SavePath, $"ProjectZip_{projectRequestId}");
		if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

		foreach (var file in scenario.Output)
		{
			Console.WriteLine($"Retrieving and saving {file.Name} ({file.Format})");
			string fileName = file.Url.Split('/').ToList().Last();
			var message = new HttpRequestMessage(HttpMethod.Get, file.Url);
			var result = await client.SendAsync(message);

			using var stream = await result.Content.ReadAsStreamAsync();
			using var fileStream = new FileStream(Path.Combine(savePath, fileName), FileMode.Create);
			await stream.CopyToAsync(fileStream);
		}

		Console.WriteLine($"Project request ID {projectRequestId} zip and save complete");

		return 0;
	}
}
