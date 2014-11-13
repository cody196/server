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

function round2(num, idp)
  return string.format("%." .. (idp or 0) .. "f", num)
end

local drawingCoords = {}

AddEventHandler('drawCoord', function(x, y, z)
	table.insert(drawingCoords, { tonumber(x) + 0.001, tonumber(y) + 0.001, tonumber(z) + 0.001 })

	local blip = AddBlipForCoord(tonumber(x) + 0.001, tonumber(y) + 0.001, tonumber(z) + 0.001, _i)
	ChangeBlipNameFromAscii(blip, 'this shit right here')
end)

AddEventHandler('clearDraw', function()
	drawingCoords = {}
end)

--TriggerEvent for local events
CreateThread(function()
	NetworkSetHealthReticuleOption(true)
	ForceWeatherNow(1)

	while true do
		Wait(0)

		for _, c in ipairs(drawingCoords) do
			DrawCorona(c[1], c[2], c[3], 2.5, 0, 0, 255, 0, 0)
		end

		local pos = table.pack(GetCharCoordinates(GetPlayerChar(target, _i), _f, _f, _f))

		local coors = "coords: (" .. round(pos[1], 2) .. ', ' .. round(pos[2], 2) .. ', ' .. round(pos[3], 2) .. ')'

		--PrintStringWithLiteralStringNow("STRING", coors, 70, 1)
	end
end)
