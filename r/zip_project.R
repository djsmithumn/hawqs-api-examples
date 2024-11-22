# Zip entire project with GIS data and save to disk.
# Must have set writeSwatEditorDb to "access" in at least one scenario in order to get GIS data.
#
# Rscript zip_project.R <project_request_id>
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

#Submit the request to the API and read the response
connection.request = request(apiUrl) |> 
  req_url_path(paste0('/builder/project/zip/', projectRequestId)) |>
  req_headers(!!!headers) |>
  req_method("PATCH")

postResponse = connection.request |> req_perform()

postResponse

#Check the status of the project zipping until it's complete
print(paste("Project zip request submitted for", projectRequestId))

spaces = 0

repeat {
  project <- getStatus(headers, paste0(apiUrl,"/builder/project/", projectRequestId), spaces)[['project']]
  if (project[['status']][['progress']] >= 100) {
    cat("\n")
    break
  }
  Sys.sleep(pollInterval)
}

error = project['status']['errorStackTrace'][[1]]
if (!is.null(error)){
  print(paste("Error stack trace:", error))
}

#Save project files to disk
requestPath = file.path(savePath, paste0("ProjectZip_",projectRequestId))
dir.create(requestPath, recursive=TRUE)


for (file in project[['output']]) {
  cat("Retrieving and saving ", file[['name']], " (", file[['format']], ")\n", sep = "")
  filename <- tail(strsplit(file[['url']], "/")[[1]], 1)
  download.file(file[['url']], destfile = file.path(requestPath, filename), mode = "wb")
}

print(paste("Project request ID", projectRequestId, "zip and save complete"))

