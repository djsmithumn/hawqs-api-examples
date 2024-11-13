# Zip entire project with GIS data and save to disk.
# Must have set writeSwatEditorDb to "access" in at least one scenario in order to get GIS data.
#
# python zip_project.py <project_request_id>
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

#Submit the request to the API and read the response
connection.request('PATCH', '/builder/project/zip/{}'.format(projectRequestId), None, headers)
postResponse = connection.getresponse()
postResponse.read()

#Check the status of the project zipping until it's complete
print('Project zip request submitted for {}'.format(projectRequestId))
starttime = time()
spaces = 0
while True:
	project, spaces = api_helpers.getStatus(connection, headers, '/builder/project/{}'.format(projectRequestId), spaces)
	if project['status']['progress'] >= 100:
		print()
		break
	sleep(pollInterval - ((time() - starttime) % pollInterval))

error = project['status']['errorStackTrace']
if error is not None:
	print('Error stack trace: {}'.format(error))

#Save project files to disk
requestPath = os.path.join(savePath, 'ProjectZip_{}'.format(projectRequestId))
os.makedirs(requestPath, exist_ok=True)
for file in project['output']:
	print('Retrieving and saving {} ({})'.format(file['name'], file['format']))
	filename = file['url'].split('/')[-1]
	urllib.request.urlretrieve(file['url'], os.path.join(requestPath, filename))

print('Project request ID {} zip and save complete'.format(projectRequestId))
