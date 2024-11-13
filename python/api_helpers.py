import http.client
import json

#Read in app settings from a file
#This is a simple way to store your API key, base URL, and path to save output files on your computer
def getAppsettings():
	appSettings = json.load(open('appsettings.json'))

	apiUrl = appSettings['AppSettings']['BaseUrl']
	connection = http.client.HTTPConnection(apiUrl.replace('http://', ''))
	apiKey = appSettings['AppSettings']['ApiKey']
	savePath = appSettings['AppSettings']['SavePath']

	headers = { 'X-API-Key': apiKey, 'Content-Type': 'application/json' }

	return connection, headers, savePath, apiUrl

#Common function to repeatedly check the status of an API request
def getStatus(connection, headers, url, spaces):
	connection.request('GET', url, None, headers)
	response = connection.getresponse()
	data = json.loads(response.read().decode())

	msg = '{}% - {}'.format(data['status']['progress'], data['status']['message'])
	print(msg + ' ' * abs(spaces - len(msg)), end='\r', flush=True)

	return data, len(msg)
