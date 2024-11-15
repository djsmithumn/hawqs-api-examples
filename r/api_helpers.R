library(httr2)
library(jsonlite)

#Read in app settings from a file
#This is a simple way to store your API key, base URL, and path to save output files on your computer
getAppsettings = function(appsettings.json) {
  appSettings = read_json(appsettings.json)

  apiUrl = appSettings[['AppSettings']][['BaseUrl']]
  connection = request(sub('https://', '',apiUrl))
  apiKey = appSettings[['AppSettings']][['ApiKey']]
  savePath = appSettings[['AppSettings']][['SavePath']]
  
  headers <- list("Content-Type" = "application/json", "X-API-Key" = apiKey)

  return(list(connection=connection, headers=headers, savePath=savePath, apiUrl=apiUrl)) 
}

#Common function to repeatedly check the status of an API request
# def getStatus(connection, headers, url, spaces):
#   connection.request('GET', url, None, headers)
#   response = connection.getresponse()
#   data = json.loads(response.read().decode())
#   
#   msg = '{}% - {}'.format(data['status']['progress'], data['status']['message'])
#   print(msg + ' ' * abs(spaces - len(msg)), end='\r', flush=True)
# 
# return data, len(msg)

getStatus = function(headers, url, spaces) {
  connection.request = request(url) |> 
    req_headers(!!!headers)
  response = connection.request |>
    req_perform(verbosity = 0)
  data = response |>
    resp_body_json()
  msg <- paste0(data[['status']][['progress']], "% - ", data[['status']][['message']])
  message(msg,replicate(abs(spaces - nchar(msg))," "),"\r",appendLF = FALSE)
  
  return(list(project=data, spaces = nchar(msg))) 
}
