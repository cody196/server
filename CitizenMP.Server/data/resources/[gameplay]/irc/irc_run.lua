local chatSharp = clr.ChatSharp

local client = chatSharp.IrcClient('irc.rizon.net', chatSharp.IrcUser('mateyate', 'mateyate'), false)

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
end)

AddEventHandler('playerDropped', function()
	client:SendMessage('* ' .. GetPlayerName(source) .. '(' .. GetPlayerGuid(source) .. '@' .. GetPlayerEP(source) .. ') left the server', '#fourdeltaone')
end)

AddEventHandler('chatMessage', function(source, name, message)
	print('hey there ' .. name)

	client:SendMessage('[' .. tostring(GetPlayerName(source)) .. ']: ' .. gsub(message, '^%d', ''), '#fourdeltaone')
end)

client:ConnectAsync()

AddEventHandler('onResourceStop', function(name)
	if name == GetInvokingResource() then
		client:Quit('Resource stopping.')
	end
end)