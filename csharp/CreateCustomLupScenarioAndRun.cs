using HawqsApiExamples.Models;
using Newtonsoft.Json;
using System.Text;

namespace HawqsApiExamples;

/// <summary>
/// Create a scenario, then demonstrate how to upload land use update data, and then run the scenario.
/// This example will not demonstrate how to programmatically create your own land use update data.
/// You may retrieve your HRUs CSV file from the project request. This file was saved to disk in the CreateProject example.
/// The API accepts a zip file with land use update data meeting the same exact requirements as the HAWQS website.
/// Please see the HAWQS website for more information on land use update data requirements.
/// For this example, you may use the land use update zip file from the source code repository, sample-files/huc8-07100009-lup-upload-example.zip.
/// </summary>
/// <param name="args">The command line arguments should include the command name followed by project request ID and the full path to land use update zip file.</param>
/// <remarks>
/// E.g. dotnet run -- --create-custom-lup-scenario-and-run 1234 C:\path\to\lup.zip
/// </remarks>
public class CreateCustomLupScenarioAndRun : ICommandAction
{
	public const string CommandName = "--create-custom-lup-scenario-and-run";

	public async Task<int> RunAsync(string[] args, AppSettings appSettings)
	{
		if (args.Length < 3)
		{
			Console.WriteLine("Please provide a project request ID and the full path to your land use update zip file.");
			return 1;
		}

		string lupFilePath = args[2].Trim();
		if (!File.Exists(lupFilePath))
		{
			Console.WriteLine("Land use update file not found.");
			return 1;
		}

		var pollInterval = TimeSpan.FromSeconds(10); //Define how frequently to check API status

		var scenarioRequestData = new
		{
			projectRequestId = int.Parse(args[1]),
			scenarioName = "custom-lup-scenario",
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

		//Submit the scenario create request to the API
		using var client = new HttpClient();
		var postMessage = new HttpRequestMessage(HttpMethod.Post, $"{appSettings.BaseUrl}/builder/scenario/create-only");
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
		string scenarioRequestUrl = (string)submissionResult["url"];
		Console.WriteLine($"Scenario request ID {scenarioRequestId} created");

		//Send the land use update zip file
		var addLupMessage = new HttpRequestMessage(HttpMethod.Put, $"{appSettings.BaseUrl}/builder/scenario/add-lup/{scenarioRequestId}");
		addLupMessage.Headers.Add("X-API-Key", appSettings.ApiKey);

		using var formContent = new MultipartFormDataContent();
		using FileStream fs = File.OpenRead(lupFilePath);
		var streamContent = new StreamContent(fs);
		streamContent.Headers.Add("Content-Type", "application/octet-stream");
		streamContent.Headers.Add("Content-Disposition", "form-data; name=\"file\"; filename=\"" + Path.GetFileName(lupFilePath) + "\"");
		formContent.Add(streamContent, "file", Path.GetFileName(lupFilePath));

		addLupMessage.Content = formContent;
		var addLupResult = await client.SendAsync(addLupMessage);

		if (!addLupResult.IsSuccessStatusCode)
		{
			Console.WriteLine($"Error sending upload land use update API request: {addLupResult.StatusCode}, {addLupResult.ReasonPhrase}");
			return 1;
		}

		//Run the scenario
		var runMessage = new HttpRequestMessage(HttpMethod.Patch, $"{appSettings.BaseUrl}/builder/scenario/run/{scenarioRequestId}");
		runMessage.Headers.Add("X-API-Key", appSettings.ApiKey);
		var runResult = await client.SendAsync(runMessage);

		if (!runResult.IsSuccessStatusCode)
		{
			Console.WriteLine($"Error sending scenario run API request: {runResult.StatusCode}, {runResult.ReasonPhrase}");
			return 1;
		}

		//Check the status of the scenario until it's complete
		var timer = new PeriodicTimer(pollInterval);
		ApiScenarioResult scenario = await ApiHelpers.GetScenarioStatus(client, scenarioRequestUrl, appSettings.ApiKey);

		while (await timer.WaitForNextTickAsync())
		{
			scenario = await ApiHelpers.GetScenarioStatus(client, scenarioRequestUrl, appSettings.ApiKey);

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
