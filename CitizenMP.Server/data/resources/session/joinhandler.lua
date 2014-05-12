local hasLocalPlayerBeenInitiated = false

AddEventHandler('gameModeStarted', function(gm)
	CreateThread(function()
		while true do
			Wait(0)

			if IsNetworkPlayerActive(GetPlayerId()) then
				-- run the 'local player started' event?
				if not hasLocalPlayerBeenInitiated then
					TriggerEvent('playerJoinReady', GetPlayerId())

					-- obvious defaults
					AllowGameToPauseForStreaming(true)
					
					SetMaxWantedLevel(6)
					SetWantedMultiplier(0.9999999)
					SetCreateRandomCops(true)
					SetDitchPoliceModels(false)

					DisplayPlayerNames(true)
					NetworkSetHealthReticuleOption(true)

					if IsThisMachineTheServer() then
						if not NetworkAdvertiseSession(true) then
							echo("noo!\n")
						end
					end

					hasLocalPlayerBeenInitiated = true
				end
			end
		end
	end)
end)