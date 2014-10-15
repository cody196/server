maps = {}
gametypes = {}

AddEventHandler('getResourceInitFuncs', function(isPreParse, add)
    if not isPreParse then
        add('map', function(file)
            addMap(file, GetInvokingResource())
        end)

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
    end
end)

mapFiles = {}

function addMap(file, owningResource)
    if not mapFiles[owningResource] then
        mapFiles[owningResource] = {}
    end

    table.insert(mapFiles[owningResource], file)
end

AddEventHandler('onClientResourceStart', function(res)
    if mapFiles[res] then
        for _, file in ipairs(mapFiles[res]) do
            parseMap(file, res)
        end
    end

    if maps[res] then
        TriggerEvent('onClientMapStart', res)
    elseif gametypes[res] then
        TriggerEvent('onClientGameTypeStart', res)
    end
end)

AddEventHandler('onClientResourceStop', function(res)
    if maps[res] then
        TriggerEvent('onClientMapStop', res)
    elseif gametypes[res] then
        TriggerEvent('onClientGameTypeStop', res)
    end

    if undoCallbacks[res] then
        for _, cb in ipairs(undoCallbacks[res]) do
            cb()
        end

        undoCallbacks[res] = nil
        mapFiles[res] = nil
    end
end)

undoCallbacks = {}

function parseMap(file, owningResource)
    local path = owningResource .. ':/' .. file

    local mapFunction = LoadScriptFile(path)

    if not mapFunction then
        echo("Couldn't load map " .. file .. "\n")
        return
    end

    if not undoCallbacks[owningResource] then
        undoCallbacks[owningResource] = {}
    end

    local env = {
        math = math, pairs = pairs, ipairs = ipairs, next = next, tonumber = tonumber, tostring = tostring,
        type = type, table = table, string = string, _G = env
    }

    TriggerEvent('getMapDirectives', function(key, cb, undocb)
        env[key] = function(...)
            local state = {}

            state.add = function(k, v)
                state[k] = v
            end

            local result = cb(state, ...)
            local args = table.pack(...)

            table.insert(undoCallbacks[owningResource], function()
                undocb(state)
            end)

            return result
        end
    end)

    local mt = {
        __index = function(t, k)
            if rawget(t, k) ~= nil then return rawget(t, k) end

            -- as we're not going to return nothing here (to allow unknown directives to be ignored)
            local f = function()
                return f
            end

            return function() return f end
        end
    }

    setmetatable(env, mt)
    setfenv(mapFunction, env)

    mapFunction()
end

AddEventHandler('getMapDirectives', function(add)
    add('car_generator', function(state, name)
        return function(opts)
            local x, y, z, heading
            local color1, color2

            if opts.x then
                x = opts.x
                y = opts.y
                z = opts.z
            else
                x = opts[1]
                y = opts[2]
                z = opts[3]
            end

            heading = opts.heading and (opts.heading + 0.01) or 0
            color1 = opts.color1 or -1
            color2 = opts.color2 or -1

            local hash = GetHashKey(name, _r)

            local carGen = CreateCarGenerator(x, y, z, heading, 2.01, 3.01, hash, color1, color2, -1, -1, 1, 0, 0, _i)
            SwitchCarGenerator(carGen, 101)

            echo("added car gen " .. tostring(carGen) .. "\n")

            state.add('cargen', carGen)
        end
    end, function(state, arg)
        echo("deleting car gen " .. tostring(state.cargen) .. "\n")

        DeleteCarGenerator(state.cargen)
    end)
end)
