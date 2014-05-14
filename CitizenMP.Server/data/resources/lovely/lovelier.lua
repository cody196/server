AddEventHandler('dick', function(a1, a2, a3)
    print("a1: " .. a1)
    print("a2: " .. a2)
    print("a3: " .. a3)

    TriggerEvent('dicks', 1, 95, { a = 'b' })
end)

AddEventHandler('dicks', function(a1, a2, a3)
    print("_a1: " .. a1)
    print("_a2: " .. a2)
    print("_a3.a: " .. a3.a)
end)

RegisterServerEvent('svPrint')

AddEventHandler('svPrint', function(m)
    print(tostring(source) .. ' says ' .. m)

    CreateRPCContext(source, function()
        return function()
            local ped = GetPlayerChar(0, _i)
            local x, y, z = GetCharCoordinates(ped, _f, _f, _f)
            PrintStringWithLiteralStringNow("STRING", "z: " .. z(), 5500, true)
        end
    end)
end)
