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
