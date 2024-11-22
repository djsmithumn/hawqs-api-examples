# Create an ICLUS scenario attached to the supplied project request ID and run it.
# This is not much different than creating a default scenario, but the inputs must match the ICLUS requirements:
# First verify the project HRU settings match what is needed for ICLUS.
# Then set your scenario request to use CMIP weather data and set useIclus to true.
#
# Rscript create_iclus_scenario_and_run.R <project_request_id>
#

library(httr2)
library(jsonlite)

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

#First check that the project HRU settings match what is needed for ICLUS
connection.request = request(apiUrl) |> 
  req_url_path(paste0("/builder/project/",projectRequestId)) |>
  req_headers(!!!headers)

projectResponse =  connection.request |> req_perform(verbosity = 0)
projectData = projectResponse |> resp_body_json()

if (is.null(projectData) | is.null(projectData[['status']]) | !projectData[['status']][['isCreated']]){
	print ('Project is not finished creating yet.')
  quit()
}
	

if (!projectData[['status']][['areHruSettingsCorrectForIclus']]) {
	print ('Project HRU settings do not match ICLUS requirements.')
	quit()
}

inputData <- list(
  projectRequestId = projectRequestId,
  scenarioName = "iclus-scenario",
  weatherDataset = "GISS-E2-R",
  climateScenario = 'RCP45',
  useIclus = TRUE,
  startingSimulationDate = "2030-01-01",
  endingSimulationDate = "2040-12-31",
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

#Check the status of the project creation until it's complete
print(paste("Scenario request ID", submissionResult[['id']], "submitted"))
spaces = 0

repeat {
  scenario <- getStatus(headers, submissionResult[['url']], spaces)[['project']]
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
requestPath = file.path(savePath, paste0("Scenario_",submissionResult[['id']]))
dir.create(requestPath, recursive=TRUE)

for (file in scenario[['output']]) {
  cat("Retrieving and saving ", file[['name']], " (", file[['format']], ")\n", sep = "")
  filename <- tail(strsplit(file[['url']], "/")[[1]], 1)
  download.file(file[['url']], destfile = file.path(requestPath, filename), mode = "wb")
}

print(paste("Scenario request ID", submissionResult[['id']], "creation complete"))

