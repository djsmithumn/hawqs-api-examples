using HawqsApiExamples.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HawqsApiExamples;
public class CreatePointSourceScenarioAndRun : ICommandAction
{
	public const string CommandName = "--create-point-source-scenario-and-run";

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

		var submissionStr = await postResult.Content.ReadAsStringAsync();
		var submissionResult = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(submissionStr);
		if (submissionResult == null || !submissionResult.ContainsKey("url"))
		{
			Console.WriteLine($"Unexpected API POST response: {submissionStr}");
			return 1;
		}

		return 0;
	}
}