-- loosely based on MTA's https://code.google.com/p/mtasa-resources/source/browse/trunk/%5Bmanagers%5D/mapmanager/mapmanager_main.lua

maps = {}
gametypes = {}

AddEventHandler('getResourceInitFuncs', function(isPreParse, add)
    add('resource_type', function(type)
        return function(params)
            local resourceName = GetInvokingResource()

            if type == 'map' then
                maps[resourceName] = params
            elseif type == 'gametype' then
                gametypes[resourceName] = params
            end
        end
    end)

    add('map', function(file)
        AddAuxFile(file)
    end)
end)

AddEventHandler('playerActivated', function()
    if getCurrentGameType() then
        TriggerClientEvent('onClientGameTypeStart', source, getCurrentGameType())
    end

    if getCurrentMap() then
        TriggerClientEvent('onClientMapStart', source, getCurrentMap())
    end
end)

AddEventHandler('onResourceStarting', function(resource)
    if maps[resource] then
        if getCurrentMap() and getCurrentMap() ~= resource then
            if doesMapSupportGameType(getCurrentGameType(), resource) then
                print("Changing map from " .. getCurrentMap() .. " to " .. resource)

                changeMap(resource)
            end

            CancelEvent()
        end
    elseif gametypes[resource] then
        if getCurrentGameType() and getCurrentGameType() ~= resource then
            print("Changing gametype from " .. getCurrentGameType() .. " to " .. resource)

            changeGameType(resource)

            CancelEvent()
        end
    end
end)

math.randomseed(os.time())

local currentGameType = nil
local currentMap = nil

AddEventHandler('onResourceStart', function(resource)
    if maps[resource] then
        if not getCurrentGameType() then
            for gt, _ in pairs(maps[resource].gameTypes) do
                changeGameType(gt)
                break
            end
        end

        if getCurrentGameType() and not getCurrentMap() then
            if doesMapSupportGameType(currentGameType, resource) then
                if TriggerEvent('onMapStart', resource, maps[resource]) then
                    if maps[resource].name then
                        SetMapName(maps[resource].name)
                    else
                        SetMapName(resource)
                    end

                    currentMap = resource
                else
                    currentMap = nil
                end
            end
        end
    elseif gametypes[resource] then
        if not getCurrentGameType() then
            if TriggerEvent('onGameTypeStart', resource, gametypes[resource]) then
                currentGameType = resource

                local gtName = gametypes[resource].name or resource

                SetGameType(gtName)

                print('Started gametype ' .. gtName)
                TriggerClientEvent('onClientGameTypeStart', -1, getCurrentGameType())

                SetTimeout(50, function()
                    if not currentMap then
                        local possibleMaps = {}

                        for map, data in pairs(maps) do
                            if data.gameTypes[currentGameType] then
                                table.insert(possibleMaps, map)
                            end
                        end

                        if #possibleMaps > 0 then
                            local rnd = math.random(#possibleMaps)
                            changeMap(possibleMaps[rnd])
                        end
                    end
                end)
            else
                currentGameType = nil
            end
        end
    end
end)

AddEventHandler('onResourceStop', function(resource)
    if resource == currentGameType then
        TriggerEvent('onGameTypeStop', resource)

        currentGameType = nil

        if currentMap then
            StopResource(currentMap)
        end
    elseif resource == currentMap then
        TriggerEvent('onMapStop', resource)

        currentMap = nil
    end
end)

AddEventHandler('rconCommand', function(commandName, args)
    if commandName == 'map' then
        if #args ~= 1 then
            RconPrint("usage: map [mapname]\n")
        end

        if not maps[args[1]] then
            RconPrint('no such map ' .. args[1] .. "\n")
            CancelEvent()

            return
        end

        if not doesMapSupportGameType(currentGameType, args[1]) then
            RconPrint('map ' .. args[1] .. ' does not support ' .. currentGameType .. "\n")
            CancelEvent()

            return
        end

        changeMap(args[1])

        RconPrint('map ' .. args[1] .. "\n")

        CancelEvent()
    elseif commandName == 'gametype' then
        if #args ~= 1 then
            RconPrint("usage: gametype [name]\n")
        end

        if not gametypes[args[1]] then
            RconPrint('no such gametype ' .. args[1] .. "\n")
            CancelEvent()

            return
        end

        changeGameType(args[1])

        RconPrint('gametype ' .. args[1] .. "\n")

        CancelEvent()
    end
end)

function getCurrentGameType()
    return currentGameType
end

function getCurrentMap()
    return currentMap
end

function changeGameType(gameType)
    if currentMap and not doesMapSupportGameType(gameType, map) then
        StopResource(currentMap)
    end

    if currentGameType then
        StopResource(currentGameType)
    end

    StartResource(gameType)
end

function changeMap(map)
    if currentMap then
        StopResource(currentMap)
    end

    StartResource(map)
end

function doesMapSupportGameType(gameType, map)
    if not gametypes[gameType] then
        return false
    end

    if not maps[map] then
        return false
    end

    if not maps[map].gameTypes then
        return true
    end

    return maps[map].gameTypes[gameType]
end
