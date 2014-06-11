math.randomseed(os.time())

local randomBase = math.random()

RegisterServerEvent('playerActivated')

AddEventHandler('playerActivated', function()
    TriggerClientEvent('createGunPickups', source, randomBase)
end)
