local ignoreFirstKey = false
local chatInputActive = false
local chatInputBuffer = ''
local lastBackspaceTime = 0
local lastCharTime = 0
local lastWasRepeat = false
local lastChar = ''

local chatBuffer = {}
local chatHeight = 6
local chatMaxItem = 1
local chatMinItem = 1

local keyboardDelay, keyboardSpeed = GetKeyboardDelays()

local colors = {
	['0'] = 'r',
	['1'] = 'r',
	['2'] = 'g',
	['3'] = 'b',
	['4'] = 'y',
	['5'] = 'p',
	['6'] = 'c',
	['7'] = 'w',
	['8'] = 'm',
	['9'] = 'l',
}

function printChatLine(name, message, color)
	if not color then
		color = { 255, 255, 255 }
	end

	chatMaxItem = chatMaxItem + 1

	-- process message
	message = message:gsub('~', '')

	for i, letter in pairs(colors) do
		message = message:gsub('%^' .. i, '~' .. letter .. '~')
	end

	-- add to array
	chatBuffer[(chatMaxItem % chatHeight) + 1] = {
		time = GetNetworkTimer(_i),
		name = name .. ':',
		color = color,
		msg = message
	}

	if (chatMaxItem - chatMinItem) > chatHeight then
		chatMinItem = chatMinItem + 1
	end
end

local function addMessageToChatBuffer(playerid, message)
	local r, g, b = GetPlayerRgbColour(player, _i, _i, _i)
	--[[local player = ConvertIntToPlayerindex(playerid)
	local r, g, b = GetPlayerRgbColour(player, _i, _i, _i)

	chatMaxItem = chatMaxItem + 1

	chatBuffer[(chatMaxItem % chatHeight) + 1] = {
		time = GetNetworkTimer(_i),
		name = GetPlayerName(player, _s) .. ':',
		color = { r, g, b },
		msg = message
	}

	if (chatMaxItem - chatMinItem) > chatHeight then
		chatMinItem = chatMinItem + 1
	end]]

	printChatLine(GetPlayerName(playerid, _s), message, { r, g, b })
end

local function setupFontStyle(font, wrapX, wrapY, style, styleArg, styleR, styleG, styleB, styleA)
	SetTextFont(font)
	SetTextBackground(false)
	SetTextDropshadow(false, 0, 0, 0, 255)
	SetTextEdge(false, 0, 0, 0, 255)

	if style == 1 then
		SetTextBackground(true)
	elseif style == 2 then
		SetTextDropshadow(styleArg, styleR, styleG, styleB, styleA)
	elseif style == 3 then
		SetTextEdge(styleArg, styleR, styleG, styleB, styleA)
	end

	SetTextProportional(true)
	SetTextWrap(wrapX, wrapY)
end

local function drawChatText()
	if chatMaxItem == chatMinItem then
		return
	end

	local i = chatMaxItem
	local time = GetNetworkTimer(_i)

	SetWidescreenFormat(2)

	local right = 1.00

	if GetIsWidescreen() then
		right = 1.33
	end

	if i >= chatMinItem then
		local line = chatHeight - 1

		repeat
			local item = chatBuffer[(i % chatHeight) + 1]

			if not item then
				echo("prevented death, values:\n")
				echo(chatMinItem .. ' ' .. chatMaxItem .. ' ' .. i .. "\n")

				item = { time = -50000 }
			end

			local msec = (15000 - (time - item.time))

			local alpha = 255
			local skipLine = false

			if msec <= 400 then
				alpha = (msec / 400) * 255

				if alpha < 0 then
					skipLine = true
				end
			end

			alpha = math.floor(alpha)

			if not skipLine then
				-- fVar91 = GET_STRING_WIDTH_WITH_STRING( "STRING", GET_PLAYER_NAME( sub_7304( l_U7111[I]._fU20 ) ) );
				SetTextRightJustify(false)
				SetTextScale(0.315, 0.43)
				SetTextColour(item.color[1], item.color[2], item.color[3], alpha)
				setupFontStyle(0, 0.00001, 2.0001, 3, true, 0, 0, 0, alpha)

				local width = GetStringWidthWithString('STRING', item.msg, _rf)
				local nameWidth = GetStringWidthWithString('STRING', item.name, _rf)
				--PrintStringWithLiteralStringNow("STRING", 'width ' .. width, 5000, true)

				-- some attempt at aligning nicely
				local fakeWidth = width

				local y = 0.010--((1 - (0.04)) / 2) - (chatHeight * 0.04)

				SetTextUseUnderscore(true)
				--DisplayTextWithLiteralString(right - 0.015 - fakeWidth - 0.006 - nameWidth, y + (0.04 * line), 'STRING', item.name)

				if name ~= ':' then
					SetTextDrawBeforeFade(true)
					DisplayTextWithLiteralString(0.010, y + (0.04 * line), 'STRING', item.name)

					SetTextRightJustify(false)
					SetTextScale(0.315, 0.43)
					setupFontStyle(0, 0.00001, 2.0001, 3, true, 0, 0, 0, alpha)
					SetTextUseUnderscore(true)
				else
					nameWidth = -0.006
				end

				SetTextColour(255, 255, 255, alpha)

				SetTextDrawBeforeFade(true)
				DisplayTextWithLiteralString(0.010 + 0.006 + nameWidth, y + (0.04 * line), 'STRING', item.msg)

				SetTextUseUnderscore(false)
				SetTextDrawBeforeFade(false)

				line = line - 1
			end

			i = i - 1
		until i <= chatMinItem or line < 0
	end
end

local function processAsciiInput(char)
	-- gta passes something invalid as ~ sometimes, we pcall to catch that
	pcall(function()
		local c = string.char(char)

		-- GTA doesn't like ~
		if c == '~' then
			return
		end

		-- don't allow leading spaces
		if c == ' ' and chatInputBuffer:len() == 0 then
			return
		end

		NetworkSetLocalPlayerIsTyping(GetPlayerId())

		-- don't do the first character, that's typically a y
		if ignoreFirstKey then
			ignoreFirstKey = false
			return
		end

		chatInputBuffer = chatInputBuffer .. string.char(char)
		lastCharTime = GetNetworkTimer(_i)
	end)
end

local function processBackspace()
	if chatInputBuffer:len() > 0 then
		chatInputBuffer = chatInputBuffer:sub(1, chatInputBuffer:len() - 1)

		lastCharTime = GetNetworkTimer(_i)
	end
end

local lastPressed = {}
local toBeOrNotToBe = 0

local function processInputForChat()
	local hasAscii

	if IsGameKeyboardKeyPressed(14) then -- holding backspace
		local timeDiff = GetNetworkTimer(_i) - lastCharTime

		if (lastWasRepeat and timeDiff > keyboardSpeed) or (not lastWasRepeat and timeDiff > keyboardDelay) or lastChar ~= 'bksp' then
			processBackspace()

			if lastChar == 'bksp' then
				lastWasRepeat = true
			else
				lastWasRepeat = false
			end

			lastChar = 'bksp'
		end
	elseif chatInputBuffer:len() > 0 and IsGameKeyboardKeyJustPressed(28) --[[ enter ]] then
		if chatInputBuffer:sub(1, 1) == '/' then
			if #chatInputBuffer == 1 then
				chatInputActive = false
				return
			end

			local commandString = chatInputBuffer:sub(2)

			TriggerEvent('chatCommandEntered', commandString)
			TriggerServerEvent('chatCommandEntered', commandString)

			chatInputActive = false
			return
		end

		--NetworkSendTextChat(GetPlayerId(), chatInputBuffer)
		local id = GetPlayerId()
		TriggerServerEvent('chatMessageEntered', GetPlayerName(id, _s), table.pack(GetPlayerRgbColour(id, _i, _i, _i)), chatInputBuffer)

		--addMessageToChatBuffer(GetPlayerId(), chatInputBuffer)

		chatInputActive = false
	elseif IsGameKeyboardKeyJustPressed(1) then
		chatInputActive = false
	else
		local i = 0
		local hasChar = false

		for i = 0, 255 do
			local ascii
			hasAscii, ascii = GetAsciiPressed(i, _i, _b)

			if hasAscii then
				if not lastPressed[i] then
					toBeOrNotToBe = i
				end

				if toBeOrNotToBe == i then
					local timeDiff = (GetNetworkTimer(_i) - lastCharTime)

					if (lastWasRepeat and timeDiff > keyboardSpeed) or (not lastWasRepeat and timeDiff > keyboardDelay) or lastChar ~= ascii then
						processAsciiInput(ascii)

						if lastChar == ascii then
							lastWasRepeat = true
						else
							lastWasRepeat = false
						end

						lastChar = ascii
					end
				end

				hasChar = true
			end

			lastPressed[i] = hasAscii
		end

		if not hasChar then
			lastChar = ''
			lastWasRepeat = false

			toBeOrNotToBe = 0
		end
	end
end

local function drawChatInput()
	-- 0.975y, 0.05h

	SetWidescreenFormat(2)
	DrawRect(0.5, 0.975, 2.000001, 0.05, 0, 0, 0, 155)

	local width = 0

	if chatInputBuffer:len() > 0 then
		SetTextScale(0.315, 0.43)
		SetTextColour(255, 255, 255, 255)
		setupFontStyle(0, 0.00001, 1.00001, 0, 0, 255, 255, 255, 255)

		local wrapValue = 1.10

		if GetIsWidescreen() then
			wrapValue = 1.33
		end

		SetTextRightJustify(false)
		SetTextUseUnderscore(true)
		SetTextWrap(0.00001, wrapValue)

		width = GetStringWidthWithString('STRING', chatInputBuffer, _rf)

		DisplayTextWithLiteralString(0.005, 0.962, 'STRING', chatInputBuffer)
		SetTextUseUnderscore(false)
	end

	SetWidescreenFormat(2)
	DrawRect(0.005 + width + 0.002 + 0.0075, 0.975, 0.015, 0.035, 255, 255, 255, 255)
end

local function processChatInput()
	-- check if the chat key was pressed
	if not chatInputActive then
		if IsGameKeyboardKeyJustPressed(21) --[[ y ]] then
			chatInputActive = true
			ignoreFirstKey = true

			--NetworkSetTextChatRecipients(-2)
			SetTextInputActive(true)

			lastCharTime = GetNetworkTimer(_i)

			chatInputBuffer = ''
		end
	else
		processInputForChat()
		drawChatInput()
	end

	SetPlayerControlForTextChat(GetPlayerIndex(), chatInputActive)

	if not chatInputActive then
		SetTextInputActive(false)
	end
end

local function getTextChat()
	local id = NetworkGetPlayerIdOfNextTextChat()

	if id ~= -1 and id ~= 0xFFFFFFFF then
		local str = NetworkGetNextTextChat(_s)

		addMessageToChatBuffer(id, str)
	end
end

AddEventHandler('chatMessage', function(name, color, message)
	local start, remaining = 1, #message

	while remaining > 0 do
		local thisSize = 100

		if remaining < thisSize then
			thisSize = remaining
		end

		local msg = message:sub(start, thisSize)

		printChatLine(name, msg, color)

		remaining = remaining - thisSize
		start = start + thisSize
	end
end)

CreateThread(function()
	while true do
		Wait(0)

		processChatInput()

		getTextChat()
		drawChatText()
	end
end)
