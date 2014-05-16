-- triggers an event when the local network player becomes active
AddEventHandler('sessionInitialized', function()
    local playerId = GetPlayerId()

    -- create a looping thread
    CreateThread(function()
        -- wait until the player becomes active
        while not IsNetworkPlayerActive(playerId) do
            Wait(0)
        end

        -- trigger an event on both the local client and the server
        TriggerEvent('playerActivated')
        TriggerServerEvent('playerActivated')
    end)
end)
