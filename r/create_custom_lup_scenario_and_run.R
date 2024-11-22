# Create a scenario, then demonstrate how to upload land use update data, and then run the scenario.
# This example will not demonstrate how to programmatically create your own land use update data.
# You may retrieve your HRUs CSV file from the project request. This file was saved to disk in the CreateProject example.
# The API accepts a zip file with land use update data meeting the same exact requirements as the HAWQS website.
# Please see the HAWQS website for more information on land use update data requirements.
# For this example, you may use the land use update zip file from the source code repository, sample-files/huc8-07100009-lup-upload-example.zip.
#
#
# RScript create_custom_lup_scenario_and_run.R <project_request_id> <full_path_to_lup_zip_file>
# e.g., RScript create_custom_lup_scenario_and_run.R 1234 "C:\path\to\lup.zip"
#

library(httr2)
library(jsonlite)
library(curl)

source("api_helpers.R")

options(timeout=NA)

#Get app settings
Appsettings = getAppsettings('appsettings.json')

connection = Appsettings[['connection']]
headers = Appsettings[['headers']]
savePath = Appsettings[['savePath']]
apiUrl = Appsettings[['apiUrl']]

#Define how frequently to check the project's creation status (seconds)
pollInterval = 10 
projectRequestId = as.integer(commandArgs(trailingOnly = TRUE)[1])

lupZipPath = commandArgs(trailingOnly = TRUE)[2]
#lupZipPath = "C:/Users/dsmith02/github/djsmithumn/hawqs-api-examples/sample-files/huc8-07100009-lup-upload-example.zip"

if (!file.exists(lupZipPath)){
	print('Land use update file not found.')
	quit()
}

inputData <- list(
  projectRequestId = projectRequestId,
  scenarioName = "custom-lup-scenario",
  weatherDataset = "PRISM",
  startingSimulationDate = "1981-01-01",
  endingSimulationDate = "1989-12-31",
  warmupYears = 2,
  outputPrintSetting = "daily",
  writeSwatEditorDb = "access",
  reportData = list(
    formats = list("csv", "netcdf"),
    units = "metric",
    outputs = list(
      rch = list(
        statistics = list("daily_avg")
      )
    )
  )
)

#Submit the request to the API and read the response
connection.request = request(apiUrl) |> 
  req_url_path("/builder/scenario/create-and-run") |>
  req_headers(!!!headers) |>
  req_body_json(inputData)

postResponse = connection.request |> req_perform(verbosity = 0)
submissionResult = postResponse |> resp_body_json()

scenarioRequestId = submissionResult[['id']]
print(paste("Scenario request ID", scenarioRequestId, "submitted"))

#Send the land use update zip file
lupUrl = paste0(apiUrl,'/builder/scenario/add-lup/', scenarioRequestId)
lupHeaders = list("X-API-Key" = headers[['X-API-Key']])

files = form_file(lupZipPath, type = 'application/zip', name = basename(lupZipPath))

lupRequest = request(lupUrl) |>
  req_headers(!!!lupHeaders) |>
  req_body_multipart(file = files) |>
  req_method("PUT") |>
  req_perform()

if (lupRequest[["status_code"]] != 200){
	print(paste('Error uploading land use update data:',lupRequest[['text']]))
	print(lupRequest[['headers']])
	quit()
}

print('Land use update data uploaded')

#Run the scenario
connection.request = request(apiUrl) |> 
  req_url_path(paste0('/builder/scenario/run/', scenarioRequestId)) |>
  req_headers(!!!headers) |>
  req_method("PATCH")

runResponse = connection.request |> req_perform(verbosity = 0)

runResponse

spaces = 0

repeat {
  scenario <- getStatus(headers, paste0(apiUrl,"/builder/scenario/", scenarioRequestId), spaces)[['project']]
  if (scenario[['status']][['progress']] >= 100) {
    cat("\n")
    break
  }
  Sys.sleep(pollInterval)
}

error = scenario['status']['errorStackTrace'][[1]]
if (!is.null(error)){
  print(paste("Error stack trace:", error))
}

#Save scenario output files to disk
requestPath = file.path(savePath, paste0("Scenario_",scenarioRequestId))
dir.create(requestPath, recursive=TRUE)


for (file in scenario[['output']]) {
  cat("Retrieving and saving ", file[['name']], " (", file[['format']], ")\n", sep = "")
  filename <- tail(strsplit(file[['url']], "/")[[1]], 1)
  download.file(file[['url']], destfile = file.path(requestPath, filename), mode = "wb")
}

print(paste("Scenario request ID", scenarioRequestId, "run complete"))


