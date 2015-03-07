solution 'CitizenWorld/CitizenWorld.csproj'

resource_type 'gametype' { name = 'Duplicity' }

dependencies
{
	'spawnmanager'
}

SetResourceInfo('uiPage', 'html/ocw.html')

client_script 'client/ocw_ui.lua'

server_script 'server/dal_main.lua'
server_script 'server/ocw_main.lua'

files
{
	'html/ocw.html',
	'html/ocw.css',
	'html/ocw.js'
}