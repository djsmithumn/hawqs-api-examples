# Create a default scenario attached to the supplied project request ID and run it.
# Provide project request ID as a command line argument.
#
# python create_default_scenario_and_run.R <project_request_id>
#


library(httr2)
library(jsonlite)

source("api_helpers.R")

#Get app settings
Appsettings = getAppsettings('appsettings.json')

connection = Appsettings[['connection']]
headers = Appsettings[['headers']]
savePath = Appsettings[['savePath']]
apiUrl = Appsettings[['apiUrl']]

#Define how frequently to check the project's creation status (seconds)
pollInterval = 10 

input_data <- list(
    projectRequestId = as.integer(commandArgs(trailingOnly = TRUE)[1]),
    weatherDataset = "PRISM",
    startingSimulationDate = "1981-01-01",
    endingSimulationDate = "1985-12-31",
    warmupYears = 2,
    outputPrintSetting = "daily",
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
  req_body_json(input_data)

postResponse = connection.request |> req_perform(verbosity = 0)
submissionResult = postResponse |> resp_body_json()

#Check the status of the project creation until it's complete
print(paste("Project request ID", submissionResult[['id']], "submitted"))
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
  #print('Error stack trace: {}'.format(error))
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


