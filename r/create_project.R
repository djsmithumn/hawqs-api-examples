# Create a project with no scenarios.
# HRU settings match what is used for ICLUS
# Poll results of project creation every 10 seconds until progress is 100%
# Save project files to a directory - no scenario has been added or run yet, so these are watershed files such as subbasins, HRUs, and point source samples.
#
# r create_project.R
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
  dataset = 'HUC8',
  downstreamSubbasin = '07100009',
  setHrus = list(
    method = 'area',
    target = 1,
    units = 'km2'
  )
)

#Submit the request to the API and read the response
connection.request = request(apiUrl) |> 
  req_url_path("/builder/project/create-only") |>
  req_headers(!!!headers) |>
  req_body_json(input_data)

postResponse = connection.request |> req_perform(verbosity = 0)
submissionResult = postResponse |> resp_body_json()

#Check the status of the project creation until it's complete
print(paste("Project request ID", submissionResult[['id']], "submitted"))
spaces = 0

repeat {
  project <- getStatus(headers, submissionResult[['url']], spaces)[['project']]
  if (project[['status']][['progress']] >= 100) {
    cat("\n")
    break
  }
  Sys.sleep(pollInterval)
}

#Save project files to disk; will include HRUs CSV, subbasins CSV and watershed files (including point source samples)
requestPath = file.path(savePath, paste0("Project_",submissionResult[['id']]))
dir.create(requestPath, recursive=TRUE)

for (file in project[['output']]) {
  cat("Retrieving and saving ", file[['name']], " (", file[['format']], ")\n", sep = "")
  filename <- tail(strsplit(file[['url']], "/")[[1]], 1)
  download.file(file[['url']], destfile = file.path(requestPath, filename), mode = "wb")
}

print(paste("Project request ID", submissionResult[['id']], "creation complete"))
