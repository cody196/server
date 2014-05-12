local ffi = require('ffi')

ffi.cdef[[
typedef struct networkGameConfig_s
{
	int data[0x1E];
} networkGameConfig;
]]

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

			local mem = ffi.new("networkGameConfig")
			ffi.fill(mem, 0x1E * 4)

			mem.data[1] = 16 -- free mode
			mem.data[3] = 32 -- max players

			NetworkStoreGameConfig(mem)

			ShutdownAndLaunchNetworkGame(0) -- episode id is arg
		end
	end
end)