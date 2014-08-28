local chatBuffer = {}
local chatActive = false

AddUIHandler('getNew', function(data, cb)
    local localBuf = chatBuffer
    chatBuffer = {}

    cb(localBuf)
end)

AddEventHandler('chatMessage', function(name, color, message)
    table.insert(chatBuffer, {
        name = name,
        color = color,
        message = message
    })

    PollUI()
end)

AddUIHandler('chatResult', function(data, cb)
    chatInputActive = false

    SetUIFocus(false)

    if data.message then
        local id = GetPlayerId()

        local r, g, b = GetPlayerRgbColour(id, _i, _i, _i)

        TriggerServerEvent('chatMessageEntered', GetPlayerName(id, _s), { r, g, b }, data.message)
    end

    cb('ok')
end)

CreateThread(function()
    while true do
        Wait(0)

        if not chatInputActive then
            if IsGameKeyboardKeyJustPressed(21) --[[ y ]] then
                chatInputActive = true

                table.insert(chatBuffer, {
                    meta = 'openChatBox'
                })

                PollUI()

                SetUIFocus(true)

                NetworkSetLocalPlayerIsTyping(GetPlayerId())
            end
        end
    end
end)
