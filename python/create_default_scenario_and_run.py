# Create a default scenario attached to the supplied project request ID and run it.
# Provide project request ID as a command line argument.
#
# python create_default_scenario_and_run.py <project_request_id>
#

import json
import urllib.request
import os, sys
from time import time, sleep
import api_helpers

#Get app settings
connection, headers, savePath = api_helpers.get_appsettings()

#Define how frequently to check the project's creation status (seconds)
pollInterval = 10 

inputData = {
	'projectRequestId': int(sys.argv[1]),
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

requestPath = os.path.join(savePath, 'Scenario_{}'.format(submissionResult['id']))
os.makedirs(requestPath, exist_ok=True)
for file in scenario['output']:
	print('Retrieving and saving {} ({})'.format(file['name'], file['format']))
	filename = file['url'].split('/')[-1]
	urllib.request.urlretrieve(file['url'], os.path.join(requestPath, filename))

print('Scenario request ID {} creation complete'.format(submissionResult['id']))
