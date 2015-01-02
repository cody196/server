--on the client
--[[

		local players = GetPlayers()
		for _, pid in ipairs(players) do
			if(pid == GetPlayerId()) then
				ShowText("Local player found. Name " .. GetPlayerName(pid))
			end
		end]]
--Blips: 
local playerBlips = {}

AddEventHandler('onClientMapStart', function()
    exports.spawnmanager:setAutoSpawn(true)
    exports.spawnmanager:forceRespawn()
end)

AddEventHandler('refreshPlayerBlips', function()
	local players = GetPlayers()
	for _, player in ipairs(players) do
		if(DoesBlipExist(playerBlips[player.serverId])) then
			RemoveBlip(playerBlips[player])
		end
		if(player.name ~= GetPlayerName(GetPlayerId(), _s) and player.isActive) then
			playerBlips[player.serverId] = AddBlipForChar(player.ped, _i)
			ChangeBlipSprite(playerBlips[player.serverId], 0)
			ChangeBlipScale(playerBlips[player.serverId], 0.90)
			ChangeBlipPriority(playerBlips[player.serverId], 3)
			ChangeBlipColour(playerBlips[player.serverId], GetPlayerColour(player))
			ChangeBlipNameFromAscii(playerBlips[player.serverId], player.name)
		end
	end
end)

AddEventHandler('playerSpawned', function()
	CreateThread(function()
		Wait(1200)
		TriggerServerEvent('playerSpawned')
	end)
end)

function ShowText(text, timeout)
  if(timeout == nil) then PrintStringWithLiteralStringNow("STRING", text, 2000, 1)
  else PrintStringWithLiteralStringNow("STRING", text, timeout, 1)
  end
end

--TriggerEvent for local events
CreateThread(function()
	SetMoneyCarriedByAllNewPeds(false)
	SetPlayersDropMoneyInNetworkGame(false)

	--SetRocketLauncherFreebieInHeli(true)
	SetSyncWeatherAndGameTime(true)
	SetTimeOfDay(13, 30)

	--poep

	--[[CreateHospital(1246.63806, 485.62134, 29.53984)
	CreateHospital(1199.60693, 192.86397, 33.55367)
	CreateHospital(980.39587, 1839.39734, 23.89775)
	CreateHospital(-391.05597, 1279.47949, 23.05956)
	CreateHospital(95.5218, 148.35896, 14.77959)
	CreateHospital(-1513.74866, 356.6188, 21.40543)
	CreateHospital(-391.05597, 1279.47949, 23.05956)
	CreateHospital(-1513.74866, 356.6188, 21.40543)--]]
end)