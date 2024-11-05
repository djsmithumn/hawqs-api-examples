using HawqsApiExamples.Models;
using Newtonsoft.Json;

namespace HawqsApiExamples;

public static class ApiHelpers
{
	public static async Task<ApiProjectResult> GetProjectStatus(HttpClient client, string url, string apiKey)
	{
		var message = new HttpRequestMessage(HttpMethod.Get, url);
		message.Headers.Add("X-API-Key", apiKey);
		var result = await client.SendAsync(message);

		var str = await result.Content.ReadAsStringAsync();
		var data = JsonConvert.DeserializeObject<ApiProjectResult>(str);

		var statusText = $"{data.Status.Progress}% - {data.Status.Message}";
		var consoleText = statusText + new string(' ', Console.WindowWidth - statusText.Length);
		int currentLineCursor = Console.CursorTop;
		Console.Write($"\r{consoleText}");
		if (data.Status.Progress < 100) Console.SetCursorPosition(0, currentLineCursor);

		return data;
	}

	public static async Task<ApiScenarioResult> GetScenarioStatus(HttpClient client, string url, string apiKey)
	{
		var message = new HttpRequestMessage(HttpMethod.Get, url);
		message.Headers.Add("X-API-Key", apiKey);
		var result = await client.SendAsync(message);

		var str = await result.Content.ReadAsStringAsync();
		var data = JsonConvert.DeserializeObject<ApiScenarioResult>(str);

		var statusText = $"{data.Status.Progress}% - {data.Status.Message}";
		var consoleText = statusText + new string(' ', Console.WindowWidth - statusText.Length);
		int currentLineCursor = Console.CursorTop;
		Console.Write($"\r{consoleText}");
		if (data.Status.Progress < 100) Console.SetCursorPosition(0, currentLineCursor);

		return data;
	}
}
