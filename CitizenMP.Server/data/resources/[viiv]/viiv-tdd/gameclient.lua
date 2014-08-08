--on the client

--Blips:
local playerBlips = {}

AddEventHandler('refreshPlayerBlips', function()
	for i = 0,31 do
		if(DoesBlipExist(playerBlips[i])) then
		RemoveBlip(playerBlips[i])
		end
		if(IsNetworkPlayerActive(i) and i ~= GetPlayerId(_r)) then
			-- place the blip and shit
			playerBlips[i] = AddBlipForChar(GetPlayerChar(i, _i), _i)
			ChangeBlipSprite(playerBlips[i], 0)
			ChangeBlipScale(playerBlips[i], 0.90)
			ChangeBlipColour(playerBlips[i], GetPlayerColour(i))
			ChangeBlipNameFromAscii(playerBlips[i], GetPlayerName(i, _s))
		end
	end
end)

AddEventHandler('playerSpawned', function()
	CreateThread(function()
	Wait(1200)
	TriggerServerEvent('playerSpawning')
	end)
end)

--TriggerEvent for local events
CreateThread(function()
	NetworkSetHealthReticuleOption(true)
	ForceWeatherNow(1)
end)
