using HawqsApiExamples;
using HawqsApiExamples.Models;
using Microsoft.Extensions.Configuration;

/// <summary>
/// The purpose of this example is to demonstrate how to use the HAWQS API project builder.
/// You may create a project, add scenarios, add point source or land use update to the scenarios, run the scenarios, and zip the project.
/// You may also use a single API endpoint to create a project with one or more scenarios and run them all in one go. With this option you cannot add point source or land use updates, and the zip project must be done separately.
/// 
/// This sample project is a console application that uses command line arguments to run the different API endpoints.
/// You will need an API key to run the examples.
/// 
/// The goal is to keep the examples simple and easy to understand. Therefore, project specific inputs are hard-coded in each class.
/// </summary>

//Store API key, API URL, and save directory in appsettings.json
var builder = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

IConfigurationRoot configuration = builder.Build();
var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>();

if (string.IsNullOrEmpty(appSettings.ApiKey) || string.IsNullOrEmpty(appSettings.BaseUrl) || string.IsNullOrEmpty(appSettings.SavePath))
{
	Console.WriteLine("Please provide an API key, API URL, and save directory in appsettings.json.");
	return 1;
}

if (args.Length < 1)
{
	Console.WriteLine("Please enter a command flag for the example you want to run. Options are: ");
	Console.WriteLine(CreateProject.CommandName);
	return 1;
}

string command = args[0].Trim();
switch (command)
{
	case CreateProject.CommandName:
		await new CreateProject().RunAsync(args, appSettings);
		break;
	case CreateDefaultScenarioAndRun.CommandName:
		await new CreateDefaultScenarioAndRun().RunAsync(args, appSettings);
		break;
	default:
		Console.WriteLine("Unrecognized command flag.");
		break;
}

return 0;
