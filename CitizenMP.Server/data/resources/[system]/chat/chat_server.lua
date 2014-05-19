RegisterServerEvent('chatCommandEntered')
RegisterServerEvent('chatMessageEntered')

AddEventHandler('chatMessageEntered', function(name, color, message)
    if not name or not color or not message or #color ~= 3 then
        return
    end

    TriggerClientEvent('chatMessage', -1, name, color, message)
    TriggerEvent('chatMessage', source, name, message)

    print(name .. ': ' .. message)
end)

-- say command handler
AddEventHandler('rconCommand', function(commandName, args)
    if commandName ~= "say" then
        return
    end

    local msg = table.concat(args, ' ')

    TriggerClientEvent('chatMessage', -1, 'console', { 0, 0x99, 255 }, msg)
    RconPrint('console: ' .. msg .. "\n")
end)

-- tell command handler
AddEventHandler('rconCommand', function(commandName, args)
    if commandName ~= "tell" then
        return
    end

    local target = table.remove(args, 1)
    local msg = table.concat(args, ' ')

    TriggerClientEvent('chatMessage', tonumber(target), 'console', { 0, 0x99, 255 }, msg)
    RconPrint('console: ' .. msg .. "\n")
end)
