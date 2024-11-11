# HAWQS API C\# Examples #

The purpose of this example is to demonstrate how to use the HAWQS API project builder. You may create a project, add scenarios, add point source or land use update to the scenarios, run the scenarios, and zip the project.

This sample project is a console application that uses command line arguments to run the different API endpoints. You will need an API key to run the examples.

The goal is to keep the examples simple and easy to understand. Therefore, project specific inputs are hard-coded in each class.

## How to run the examples ##

Edit appsettings.json and add your API key, set the base URL (e.g. https://dev-api.hawqs.tamu.edu), and set directory to save your project and scenario files.

Next, decide how you will run the console application. You can run the console application from a dotnet command or from a compiled executable. If you run from a compiled executable, be sure to copy your appsettings.json file to the same folder as your executable.

If you run from a dotnet command, your commands will start with `dotnet run --`, and if you run from a compiled executable, your commands will start with `HawqsApiExamples.exe`.

### Create a project ###

```bash
dotnet run -- create-project
```

See `CreateProject.cs`. This command will create a project with no scenarios to start. HRU settings match what is used for ICLUS so that we may add an ICLUS scenario later. After the project is create, we save to disk the subbasins CSV, HRUs CSV, and a zip of other watershed files including point source example templates.

### Create a default scenario ###

```bash
dotnet run -- create-default-scenario-and-run [PROJECT_REQUEST_ID]
```

See `CreateDefaultScenarioAndRun.cs`. `[PROJECT_REQUEST_ID]` should be replaced with the project request ID integer received when you created the project. This command will create a default scenario with no point sources or land use updates. The scenario will be run and the results will be saved to disk.

### Create an ICLUS scenario ###

```bash
dotnet run -- create-iclus-scenario-and-run [PROJECT_REQUEST_ID]
```

See `CreateIclusScenarioAndRun.cs`. This is not much different than creating a default scenario, but the inputs must match the ICLUS requirements. First we verify the project HRU settings match what is needed for ICLUS. Then set the scenario request to use CMIP weather data and set `useIclus` to true.

### Create a scenario with custom land use update data ###

```bash
dotnet run -- create-custom-lup-scenario-and-run [PROJECT_REQUEST_ID] "C:\path\to\lup.zip"
```

See `CreateCustomLupScenarioAndRun.cs`. This will create a scenario, then demonstrate how to upload land use update data, and then run the scenario. This example will not demonstrate how to programmatically create your own land use update data. You may retrieve your HRUs CSV file from the project request. This file was saved to disk in the `create-project` example. 

The API accepts a zip file with land use update data meeting the same exact requirements as the HAWQS website. Please see the HAWQS website for more information on land use update data requirements.

For this example, you may use the land use update zip file from the source code repository, `sample-files/huc8-07100009-lup-upload-example.zip`.

### Create a scenario with point source data ###

```bash
dotnet run -- create-point-source-scenario-and-run [PROJECT_REQUEST_ID] "C:\path\to\point-source.zip"
```

See `CreatePointSourceScenarioAndRun.cs`. This will create a scenario, demonstrate how to upload point source data, and then run the scenario. This example will not demonstrate how to create your own point source data. You may retrieve sample files to aid you from the project request. These files were saved to disk in the `create-project` example.

You may construct your own using either the list of subbasins in the subbasin csv file, or unzip the watershed files and look in the HAWQS/Samples folder for point source samples for your watershed.

The API accepts a zip file with point source data meeting the same exact requirements as the HAWQS website. Please see the HAWQS website for more information on point source data requirements.

For this example, you may use the point source zip file from the source code repository, `sample-files/huc8-07100009-point-source-upload-example.zip`.

### Zip a project ###

```bash
dotnet run -- zip-project [PROJECT_REQUEST_ID]
```

See `ZipProject.cs`. This will zip the entire project with GIS data and save to disk. You must have set `writeSwatEditorDb` to "access" in at least one scenario (default preferred for clarity) in order to get GIS data. This command functions the same as the zip project feature available in the Project Downloads section of the main HAWQS interface with GIS data checked.