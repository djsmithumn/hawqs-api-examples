using HawqsApiExamples.Models;
using Newtonsoft.Json;
using System.Text;

namespace HawqsApiExamples;

/// <summary>
/// Create an ICLUS scenario attached to the supplied project request ID and run it.
/// This is not much different than creating a default scenario, but the inputs must match the ICLUS requirements:
/// First verify the project HRU settings match what is needed for ICLUS.
/// Then set your scenario request to use CMIP weather data and set useIclus to true.
/// </summary>
/// <param name="args">The command line arguments should include the command name followed by project request ID.</param>
public class CreateIclusScenarioAndRun : ICommandAction
{
	public const string CommandName = "create-iclus-scenario-and-run";

	public async Task<int> RunAsync(string[] args, AppSettings appSettings)
	{
		if (args.Length < 2)
		{
			Console.WriteLine("Please provide a project request ID.");
			return 1;
		}

		//First check that the project HRU settings match what is needed for ICLUS
		int projectRequestId = int.Parse(args[1]);
		using var client = new HttpClient();
		var projectMessage = new HttpRequestMessage(HttpMethod.Get, $"{appSettings.BaseUrl}/builder/project/{projectRequestId}");
		projectMessage.Headers.Add("X-API-Key", appSettings.ApiKey);
		var projectResult = await client.SendAsync(projectMessage);

		var str = await projectResult.Content.ReadAsStringAsync();
		var data = JsonConvert.DeserializeObject<ApiProjectResult>(str);

		if (data == null || data.Status == null || !data.Status.IsCreated)
		{
			Console.WriteLine("Project is not finished creating yet.");
			return 1;
		}

		if (!data.Status.AreHruSettingsCorrectForIclus)
		{
			Console.WriteLine("Project HRU settings do not match ICLUS requirements.");
			return 1;
		}

		var pollInterval = TimeSpan.FromSeconds(10); //Define how frequently to check API status

		var scenarioRequestData = new
		{
			projectRequestId,
			scenarioName = "iclus-scenario",
			weatherDataset = "GISS-E2-R",
			climateScenario = "RCP45",
			useIclus = true,
			startingSimulationDate = "2030-01-01",
			endingSimulationDate = "2040-12-31",
			warmupYears = 2,
			outputPrintSetting = "daily",
			writeSwatEditorDb = "access",
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
