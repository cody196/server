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
		NetworkChangeExtendedGameConfigCit()

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
