# Create an ICLUS scenario attached to the supplied project request ID and run it.
# This is not much different than creating a default scenario, but the inputs must match the ICLUS requirements:
# First verify the project HRU settings match what is needed for ICLUS.
# Then set your scenario request to use CMIP weather data and set useIclus to true.
#
# python create_iclus_scenario_and_run.py <project_request_id>
#

import json
import urllib.request
import os, sys
from time import time, sleep
import api_helpers

#Get app settings
connection, headers, savePath, apiUrl = api_helpers.getAppsettings()

#Define how frequently to check the project's creation status (seconds)
pollInterval = 10 
projectRequestId = int(sys.argv[1])

#First check that the project HRU settings match what is needed for ICLUS
connection.request('GET', '/builder/project/{}'.format(projectRequestId), None, headers)
projectResponse = connection.getresponse()
projectData = json.loads(projectResponse.read().decode())

if projectData is None or projectData['status'] is None or not projectData['status']['isCreated']:
	print ('Project is not finished creating yet.')
	sys.exit()

if not projectData['status']['areHruSettingsCorrectForIclus']:
	print ('Project HRU settings do not match ICLUS requirements.')
	sys.exit()

inputData = {
	'projectRequestId': projectRequestId,
	'scenarioName': 'iclus-scenario',
	'weatherDataset': 'GISS-E2-R',
	'climateScenario': 'RCP45',
	'useIclus': True,
    'startingSimulationDate': '2030-01-01',
    'endingSimulationDate': '2040-12-31',
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
connection.request('POST', '/builder/scenario/create-and-run', json.dumps(inputData), headers)
postResponse = connection.getresponse()
submissionResult = json.loads(postResponse.read().decode())

#Check the status of the project creation until it's complete
print('Scenario request ID {} submitted'.format(submissionResult['id']))
starttime = time()
spaces = 0
while True:
	scenario, spaces = api_helpers.getStatus(connection, headers, '/builder/scenario/{}'.format(submissionResult['id']), spaces)
	if scenario['status']['progress'] >= 100:
		print()
		break
	sleep(pollInterval - ((time() - starttime) % pollInterval))

error = scenario['status']['errorStackTrace']
if error is not None:
	print('Error stack trace: {}'.format(error))

#Save scenario output files to disk
requestPath = os.path.join(savePath, 'Scenario_{}'.format(submissionResult['id']))
os.makedirs(requestPath, exist_ok=True)
for file in scenario['output']:
	print('Retrieving and saving {} ({})'.format(file['name'], file['format']))
	filename = file['url'].split('/')[-1]
	urllib.request.urlretrieve(file['url'], os.path.join(requestPath, filename))

print('Scenario request ID {} run complete'.format(submissionResult['id']))
