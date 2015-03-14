local chatSharp = clr.ChatSharp

local client = chatSharp.IrcClient('irc.rizon.net', chatSharp.IrcUser('mateyate', 'mateyate'), false)

-- temporary workaround for connections that never triggered playerActivated but triggered playerDropped
local activatedPlayers = {}

client.ConnectionComplete:add(function(s : object, e : System.EventArgs) : void
	client:JoinChannel('#fourdeltaone')
end)

-- why is 'received' even misspelled here?
client.ChannelMessageRecieved:add(function(s : object, e : ChatSharp.Events.PrivateMessageEventArgs) : void
	local msg = e.PrivateMessage

	TriggerClientEvent('chatMessage', -1, msg.User.Nick, { 0, 0x99, 255 }, msg.Message)
end)

AddEventHandler('playerActivated', function()
	client:SendMessage('* ' .. GetPlayerName(source) .. '(' .. GetPlayerGuid(source) .. '@' .. GetPlayerEP(source) .. ') joined the server', '#fourdeltaone')
	table.insert(activatedPlayers, GetPlayerGuid(source))
end)

AddEventHandler('playerDropped', function()
	-- find out if this connection ever triggered playerActivated
	for index,guid in pairs(activatedPlayers) do
		if guid == playerGuid then
			-- show player dropping connection in chat
			client:SendMessage('* ' .. GetPlayerName(source) .. '(' .. GetPlayerGuid(source) .. '@' .. GetPlayerEP(source) .. ') left the server', '#fourdeltaone')
			table.remove(activatedPlayers, index)
			return
		end
	end
end)

AddEventHandler('chatMessage', function(source, name, message)
	print('hey there ' .. name)

	local displayMessage = gsub(message, '^%d', '')

	-- ignore zero-length messages
	if string.len(displayMessage) == 0 then
		return
	end

	-- ignore chat messages that are actually commands
	if string.sub(displayMessage, 1, 1) == "/" then
		return
	end

	client:SendMessage('[' .. tostring(GetPlayerName(source)) .. ']: ' .. displayMessage, '#fourdeltaone')
end)

client:ConnectAsync()

AddEventHandler('onResourceStop', function(name)
	if name == GetInvokingResource() then
		client:Quit('Resource stopping.')
	end
end)