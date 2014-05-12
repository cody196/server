CreateThread(function()
	while true do
		Wait(0)

		if DoesGameCodeWantToLeaveNetworkSession() then
			if NetworkIsSessionStarted() then
				NetworkEndSession()

				while NetworkEndSessionPending() do
					Wait(0)
				end
			end

			NetworkLeaveGame()

			while NetworkLeaveGamePending() do
				Wait(0)
			end

			NetworkStoreGameConfig(GetNetworkGameConfigPtr())

			ShutdownAndLaunchNetworkGame(0) -- episode id is arg
		end
	end
end)
