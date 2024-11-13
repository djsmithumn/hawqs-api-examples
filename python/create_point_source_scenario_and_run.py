# Create a scenario, then demonstrate how to upload point source data, and then run the scenario.
# This example will not demonstrate how to create your own point source data.
# You may retrieve sample files to aid you from the project request.
# These files were saved to disk in the CreateProject example.
# You may construct your own using either the list of subbasins in the subbasin csv file,
# or unzip the watershed files and look in the HAWQS/Samples folder for point source samples for your watershed.
# The API accepts a zip file with point source data meeting the same exact requirements as the HAWQS website.
# Please see the HAWQS website for more information on point source data requirements.
# For this example, you may use the point source zip file from the source code repository, sample-files/huc8-07100009-point-source-upload-example.zip.
#
# REQUIRED LIBRARY: requests
# Install it with pip: pip install requests
#
# python create_point_source_scenario_and_run.py <project_request_id> <full_path_to_point_source_zip_file>
# e.g., python create_point_source_scenario_and_run.py 1234 "C:\path\to\point-source.zip"
#

import json
import urllib.request
import os, sys
from time import time, sleep
import api_helpers
import requests

#Get app settings
connection, headers, savePath, apiUrl = api_helpers.getAppsettings()

#Define how frequently to check the project's creation status (seconds)
pollInterval = 10 
projectRequestId = int(sys.argv[1])
pointSourceZipPath = sys.argv[2]

if not os.path.exists(pointSourceZipPath):
	print('Point source file not found.')
	sys.exit()

inputData = {
	'projectRequestId': projectRequestId,
	'scenarioName': 'point-source-scenario',
	'weatherDataset': 'PRISM',
    'startingSimulationDate': '1981-01-01',
    'endingSimulationDate': '1989-12-31',
    'warmupYears': 2,
    'outputPrintSetting': 'daily',
	'writeSwatEditorDb': 'access',
    'reportData': {
        'formats': [ 'csv', 'netcdf' ],
        'units': 'metric',
        'outputs': {
            'rch': {
                'statistics': [ 'daily_avg' ]
            }
        }
    }
}

#Submit the request to the API and read the response
connection.request('POST', '/builder/scenario/create-only', json.dumps(inputData), headers)
postResponse = connection.getresponse()
submissionResult = json.loads(postResponse.read().decode())

scenarioRequestId = submissionResult['id']
print('Scenario request ID {} created'.format(scenarioRequestId))

#Send the point source zip file
pointSourceUrl = '{}/builder/scenario/add-point-source/{}'.format(apiUrl, scenarioRequestId)
pointSourceHeaders = { 'X-API-Key': headers['X-API-Key'] }
files = {'file': (os.path.basename(pointSourceZipPath), open(pointSourceZipPath, 'rb'), 'application/zip')}
pointSourceRequest = requests.put(pointSourceUrl, headers=pointSourceHeaders, files=files)

if pointSourceRequest.status_code != 200:
	print('Error uploading point source data: {}'.format(pointSourceRequest.text))
	print(pointSourceRequest.headers)
	sys.exit()

print('Point source data uploaded')

#Run the scenario
connection.request('PATCH', '/builder/scenario/run/{}'.format(scenarioRequestId), None, headers)
runResponse = connection.getresponse()
runResponse.read()

starttime = time()
spaces = 0
while True:
	scenario, spaces = api_helpers.getStatus(connection, headers, '/builder/scenario/{}'.format(scenarioRequestId), spaces)
	if scenario['status']['progress'] >= 100:
		print()
		break
	sleep(pollInterval - ((time() - starttime) % pollInterval))

error = scenario['status']['errorStackTrace']
if error is not None:
	print('Error stack trace: {}'.format(error))

#Save scenario output files to disk
requestPath = os.path.join(savePath, 'Scenario_{}'.format(scenarioRequestId))
os.makedirs(requestPath, exist_ok=True)
for file in scenario['output']:
	print('Retrieving and saving {} ({})'.format(file['name'], file['format']))
	filename = file['url'].split('/')[-1]
	urllib.request.urlretrieve(file['url'], os.path.join(requestPath, filename))

print('Scenario request ID {} run complete'.format(scenarioRequestId))
