RegisterServerEvent('onPlayerDied')
AddEventHandler('onPlayerDied', function(player, reason)
    print('player ' .. GetPlayerName(player) .. ' died with reason ' .. tostring(reason))

    TriggerClientEvent('onPlayerDied', -1, player, reason)
end)

RegisterServerEvent('onPlayerKilled')
AddEventHandler('onPlayerKilled', function(player, attacker, reason)
    print('player ' .. GetPlayerName(player) .. ' got killed by ' .. GetPlayerName(attacker) .. ' with reason ' .. tostring(reason))

    TriggerClientEvent('onPlayerKilled', -1, player, attacker, reason)
end)

RegisterServerEvent('onPlayerWasted')
AddEventHandler('onPlayerWasted', function(player)
    TriggerClientEvent('onPlayerWasted', -1, source)
end)
