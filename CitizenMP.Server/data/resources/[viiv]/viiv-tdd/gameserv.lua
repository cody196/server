-- on server

print("[GAME] Gameserv initialising...")
AddEventHandler('playerDropped', function()
	TriggerClientEvent('refreshPlayerBlips', -1)
end)

AddEventHandler('playerActivated', function()
	TriggerClientEvent('refreshPlayerBlips', -1)
end)

RegisterServerEvent('playerSpawned')
AddEventHandler('playerSpawned', function()
	TriggerClientEvent('refreshPlayerBlips', -1)
end)


print("[GAME] Gameserv initialized")