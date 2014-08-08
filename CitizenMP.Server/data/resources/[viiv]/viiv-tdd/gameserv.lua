-- on server

AddEventHandler('onGameTypeStart', function(resource)
    print("[SCRIPT] Running.")
end)

AddEventHandler('onMapStart', function(resource)
    print("[MAP] Map has been started.")
end)

print("[GAME] Gameserv initialising...")
AddEventHandler('playerDropped', function()
	TriggerClientEvent('refreshPlayerBlips', -1)
	name = GetPlayerName(source)
	msg = "^3" .. name .. " ^7has left the game."
	print("[LEAVE] " .. name .. " has left the server.")
	TriggerClientEvent('refreshPlayerBlips', -1)
end)

AddEventHandler('playerActivated', function()
	TriggerClientEvent('refreshPlayerBlips', -1)
	name = GetPlayerName(source)
	msg = "^3" .. name .. " ^7has joined the game."
	print("[JOIN] " .. name .. " has joined the server.")
	TriggerClientEvent('refreshPlayerBlips', -1)
end)

RegisterServerEvent('playerSpawning')
AddEventHandler('playerSpawning', function()
	TriggerClientEvent('refreshPlayerBlips', -1)
end)

print("[GAME] Gameserv initialized")