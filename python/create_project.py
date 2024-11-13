# Create a project with no scenarios.
# HRU settings match what is used for ICLUS
# Poll results of project creation every 10 seconds until progress is 100%
# Save project files to a directory - no scenario has been added or run yet, so these are watershed files such as subbasins, HRUs, and point source samples.
#
# python create_project.py
#

import json
import urllib.request
import os
from time import time, sleep
import api_helpers

#Get app settings
connection, headers, savePath, apiUrl = api_helpers.getAppsettings()

#Define how frequently to check the project's creation status (seconds)
pollInterval = 10 

inputData = {
	'dataset': 'HUC8',
	'downstreamSubbasin': '07100009',
	'setHrus': {
		'method': 'area',
		'target': 1,
		'units': 'km2'
	}
}

#Submit the request to the API and read the response
connection.request('POST', '/builder/project/create-only', json.dumps(inputData), headers)
postResponse = connection.getresponse()
submissionResult = json.loads(postResponse.read().decode())

#Check the status of the project creation until it's complete
print('Project request ID {} submitted'.format(submissionResult['id']))
starttime = time()
spaces = 0
while True:
	project, spaces = api_helpers.getStatus(connection, headers, '/builder/project/{}'.format(submissionResult['id']), spaces)
	if project['status']['progress'] >= 100:
		print()
		break
	sleep(pollInterval - ((time() - starttime) % pollInterval))

error = project['status']['errorStackTrace']
if error is not None:
	print('Error stack trace: {}'.format(error))

#Save project files to disk; will include HRUs CSV, subbasins CSV and watershed files (including point source samples)
requestPath = os.path.join(savePath, 'Project_{}'.format(submissionResult['id']))
os.makedirs(requestPath, exist_ok=True)
for file in project['output']:
	print('Retrieving and saving {} ({})'.format(file['name'], file['format']))
	filename = file['url'].split('/')[-1]
	urllib.request.urlretrieve(file['url'], os.path.join(requestPath, filename))

print('Project request ID {} creation complete'.format(submissionResult['id']))
