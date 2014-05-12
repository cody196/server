local ffi = require('ffi')

ffi.cdef[[
typedef struct
{
	int blah[30];
} dotty;
]]

local function acquireHostLock()
	if IsThisMachineTheServer() then
		SetThisMachineRunningServerScript(true)
		return true
	end
	
	return false
end

local function releaseHostLock()
	SetThisMachineRunningServerScript(false)
end

function serviceHostStuff()
	if acquireHostLock() then
		-- check if players want to join
		for i = 0, 31 do
			if PlayerWantsToJoinNetworkGame(i) then
				echo("someone wants to join! (" .. i .. ")\n")
				TellNetPlayerToStartPlaying(i, 0)

				TriggerEvent('serverPlayerJoining', i)
			end
		end
	
		releaseHostLock()
	end
end

local didJoinBefore = false

AddEventHandler('onPlayerSpawning', function(id)
	if id == GetPlayerId() then
		if not didJoinBefore then
			CreateThread(function()
				Wait(100)

				TriggerRemoteEvent('playerJoined', -1, GetPlayerId())
			end)

			didJoinBefore = true
		end
	end
end)

AddEventHandler('gameModeStarted', function(gmName)
	-- this is needed for the player to become active
	RequestModel(GetPlayersettingsModelChoice(_r))
	LoadAllObjectsNow()
	
	ChangePlayerModel(GetPlayerIndex(), GetPlayersettingsModelChoice(_r))
	MarkModelAsNoLongerNeeded(GetPlayersettingsModelChoice(_r))

	if IsThisMachineTheServer() then
		-- unknown stuff, seems needed though
		local mm = ffi.new('dotty')
		ffi.fill(mm, ffi.sizeof(mm))

		mm.blah[1] = 16
		mm.blah[2] = 0
		mm.blah[3] = 32
		mm.blah[4] = 0
		mm.blah[5] = 0
		mm.blah[6] = 0
		mm.blah[7] = 0
		mm.blah[8] = 0
		
		mm.blah[9] = -1
		mm.blah[10] = -1
		mm.blah[11] = -1
		mm.blah[12] = 0
		mm.blah[13] = 1
		mm.blah[14] = 7
		mm.blah[15] = 0
		mm.blah[16] = -1
		
		mm.blah[17] = -1
		mm.blah[18] = -1
		mm.blah[19] = 1
		mm.blah[20] = 1
		mm.blah[21] = 0
		mm.blah[22] = 1
		mm.blah[23] = 2
		mm.blah[24] = 0
		
		mm.blah[25] = 1
		mm.blah[26] = 0
		mm.blah[27] = 0
		mm.blah[28] = 1
		mm.blah[29] = 0
		mm.blah[30] = 0
		
		NetworkChangeExtendedGameConfig(mm)

		CreateThread(function()
			Wait(1500)

			if not NetworkIsSessionStarted() then
				NetworkStartSession()

				while NetworkStartSessionPending() do
					Wait(0)
				end
				
				if not NetworkStartSessionSucceeded() then
					ForceLoadingScreen(0)
					SetMsgForLoadingScreen("MO_SNI")

					return
				end

				launchGame()
			end

			NetworkSetScriptLobbyState(false)
			SwitchArrowAboveBlippedPickups(true)
			UsePlayerColourInsteadOfTeamColour(true)
			LoadAllPathNodes(true)
			SetSyncWeatherAndGameTime(true)
		end)
	end

	-- some default settings
	NetworkSetScriptLobbyState(false)
	SwitchArrowAboveBlippedPickups(true)
	UsePlayerColourInsteadOfTeamColour(true)
	LoadAllPathNodes(true)
	SetSyncWeatherAndGameTime(true)

	-- host service loop
	CreateThread(function()
		while true do
			Wait(0)

			serviceHostStuff()
		
			if LocalPlayerIsReadyToStartPlaying() then
				echo("launching local player\n")
				LaunchLocalPlayerInNetworkGame()
			end
		end
	end)
end)