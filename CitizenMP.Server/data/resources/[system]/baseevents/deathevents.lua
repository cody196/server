CreateThread(function()
    local isDead = false

    while true do
        Wait(0)

        local player = GetPlayerId()

        if player.isActive then
            local ped = player.ped

            if ped.isFatallyInjured and not isDead then
                isDead = true

                local killer = player.lastKiller

                local networkId = ped.networkId
                local deathReason = GetDestroyerOfNetworkId(networkId)

                if killer == player then
                    triggerEvent('onPlayerDied', player.serverId, deathReason, ped.position)
                else
                    triggerEvent('onPlayerKilled', player.serverId, killer.serverId, deathReason, ped.position)
                end
            elseif not ped.isFatallyInjured then
                isDead = false
            end
        end
    end
end)
