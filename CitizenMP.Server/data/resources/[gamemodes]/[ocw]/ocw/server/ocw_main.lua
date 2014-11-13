-- handle chat commands
AddEventHandler('chatMessage', function(source, name, message)
	if message:sub(1, 1) == '!' then
		TriggerClientEvent('commandEntered', source, message:sub(2))

		CancelEvent()
	end
end)

-- transmit jobs
local jobs = {
	
}

local SpawnPoint = DAL.Schema 'SpawnPoint'
SpawnPoint.Index 'PlayerGuid'

SpawnPoint.Metafield 'playerGuid' { function()
	return GetPlayerGuid(source)
end }

SpawnPoint.QueryCheck { function(row)
	if row.PlayerGuid ~= GetPlayerGuid(source) then
		return false
	end

	return true
end }

SpawnPoint.WriteCheck { function(row)
	row.PlayerGuid = GetPlayerGuid(source)

	return true
	--[[if row.PlayerGuid ~= GetPlayerGuid(source) then
		return false
	end

	return true]]
end }