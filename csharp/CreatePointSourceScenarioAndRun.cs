using HawqsApiExamples.Models;
using Newtonsoft.Json;
using System.Text;

namespace HawqsApiExamples;

/// <summary>
/// Create a scenario, then demonstrate how to upload point source data, and then run the scenario.
/// This example will not demonstrate how to create your own point source data.
/// You may retrieve sample files to aid you from the project request.
/// These files were saved to disk in the CreateProject example.
/// You may construct your own using either the list of subbasins in the subbasin csv file,
/// or unzip the watershed files and look in the HAWQS/Samples folder for point source samples for your watershed.
/// The API accepts a zip file with point source data meeting the same exact requirements as the HAWQS website.
/// Please see the HAWQS website for more information on point source data requirements.
/// For this example, you may use the point source zip file from the source code repository, sample-files/huc8-07100009-point-source-upload-example.zip.
/// </summary>
/// <param name="args">The command line arguments should include the command name followed by project request ID and the full path to point source zip file.</param>
/// <remarks>
/// E.g. dotnet run -- --create-point-source-scenario-and-run 1234 C:\path\to\point-source.zip
/// </remarks>
public class CreatePointSourceScenarioAndRun : ICommandAction
{
	public const string CommandName = "--create-point-source-scenario-and-run";

	public async Task<int> RunAsync(string[] args, AppSettings appSettings)
	{
		if (args.Length < 3)
		{
			Console.WriteLine("Please provide a project request ID and the full path to your point source zip file.");
			return 1;
		}

		string pointSourceFilePath = args[2].Trim();
		if (!File.Exists(pointSourceFilePath))
		{
			Console.WriteLine("Point source file not found.");
			return 1;
		}

		var pollInterval = TimeSpan.FromSeconds(10); //Define how frequently to check API status

		var scenarioRequestData = new
		{
			projectRequestId = int.Parse(args[1]),
			scenarioName = "point-source-scenario",
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

		//Send the point source zip file
		var addPsMessage = new HttpRequestMessage(HttpMethod.Put, $"{appSettings.BaseUrl}/builder/scenario/add-point-source/{scenarioRequestId}");
		addPsMessage.Headers.Add("X-API-Key", appSettings.ApiKey);

		using var formContent = new MultipartFormDataContent();
		using FileStream fs = File.OpenRead(pointSourceFilePath);
		var streamContent = new StreamContent(fs);
		streamContent.Headers.Add("Content-Type", "application/octet-stream");
		streamContent.Headers.Add("Content-Disposition", "form-data; name=\"file\"; filename=\"" + Path.GetFileName(pointSourceFilePath) + "\"");
		formContent.Add(streamContent, "file", Path.GetFileName(pointSourceFilePath));

		addPsMessage.Content = formContent;
		var addPsResult = await client.SendAsync(addPsMessage);

		if (!addPsResult.IsSuccessStatusCode)
		{
			Console.WriteLine($"Error sending upload point source API request: {addPsResult.StatusCode}, {addPsResult.ReasonPhrase}");
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