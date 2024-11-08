using HawqsApiExamples.Models;
using Newtonsoft.Json;
using System.Text;

namespace HawqsApiExamples;

/// <summary>
/// Create a default scenario attached to the supplied project request ID and run it.
/// </summary>
/// <param name="args">The command line arguments should include the command name followed by project request ID.</param>
public class CreateDefaultScenarioAndRun : ICommandAction
{
	public const string CommandName = "--create-default-scenario-and-run";

	public async Task<int> RunAsync(string[] args, AppSettings appSettings)
	{
		if (args.Length < 2)
		{
			Console.WriteLine("Please provide a project request ID.");
			return 1;
		}

		var pollInterval = TimeSpan.FromSeconds(10); //Define how frequently to check API status

		var scenarioRequestData = new
		{
			projectRequestId = int.Parse(args[1]),
			weatherDataset = "PRISM",
			startingSimulationDate = "1981-01-01",
			endingSimulationDate = "1989-12-31",
			warmupYears = 2,
			outputPrintSetting = "daily",
			writeSwatEditorDb = true,
			reportData = new
			{
				formats = new List<string> { "csv", "netcdf" },
				units = "metric",
				outputs = new
				{
					rch = new
					{
						statistics = new List<string> { "daily_avg" }
					}
				}
			}
		};

		//Submit the scenario request to the API
		using var client = new HttpClient();
		var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{appSettings.BaseUrl}/builder/scenario/create-and-run");
		postMessage.Headers.Add("X-API-Key", appSettings.ApiKey);
		postMessage.Content = new StringContent(JsonConvert.SerializeObject(scenarioRequestData), Encoding.UTF8, "application/json");
		var postResult = await client.SendAsync(postMessage);

		if (!postResult.IsSuccessStatusCode)
		{
			Console.WriteLine($"Error sending scenario creation API request: {postResult.StatusCode}, {postResult.ReasonPhrase}");
			return 1;
		}

		//Read the response and get the scenario request ID and URL
		var submissionStr = await postResult.Content.ReadAsStringAsync();
		var submissionResult = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(submissionStr);
		if (submissionResult == null || !submissionResult.ContainsKey("url"))
		{
			Console.WriteLine($"Unexpected API POST response: {submissionStr}");
			return 1;
		}

		int scenarioRequestId = (int)submissionResult["id"];
		Console.WriteLine($"Scenario request ID {scenarioRequestId} submitted");

		//Check the status of the scenario until it's complete
		var timer = new PeriodicTimer(pollInterval);
		ApiScenarioResult scenario = await ApiHelpers.GetScenarioStatus(client, submissionResult["url"], appSettings.ApiKey);

		while (await timer.WaitForNextTickAsync())
		{
			scenario = await ApiHelpers.GetScenarioStatus(client, submissionResult["url"], appSettings.ApiKey);

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

		//Save scenario output files to disk
		var savePath = Path.Combine(appSettings.SavePath, $"Scenario_{scenarioRequestId}");
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

		Console.WriteLine($"Scenario request ID {scenarioRequestId} run complete");

		return 0;
	}
}
