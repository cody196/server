local gResults = {}

AddEventHandler('svRpc:run', function(a, execQueue)
    local thisResults = {}

    for i, item in ipairs(execQueue) do
        local hash = item.h

        local arguments = {}
        local retvalMapping = {}

        for j, a in ipairs(item.a) do
            if type(a) == 'table' then
                if a._a ~= '_z' then
                    if a._a == '_i' then
                        table.insert(arguments, _i)
                    elseif a._a == '_f' then
                        table.insert(arguments, _f)
                    end

                    table.insert(retvalMapping, a._i)
                else
                    table.insert(arguments, gResults[a._i])
                end
            else
                table.insert(arguments, a)
            end
        end

        local retvals = table.pack(CallNative(hash, table.unpack(arguments)))

        for j, m in ipairs(retvalMapping) do
            thisResults[m] = retvals[j]
            gResults[m] = retvals[j]
        end
    end

    thisResults.idx = execQueue.idx

    TriggerServerEvent('svRpc:results', thisResults)
end)
