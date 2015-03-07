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

local PlayerTeamAssignment = DAL.Schema 'PlayerTeamAssignment'
PlayerTeamAssignment.Index 'PlayerGuid'

PlayerTeamAssignment.Metafield 'playerGuid' { function()
	return GetPlayerGuid(source)
end }

PlayerTeamAssignment.QueryCheck { function(row)
	if row.PlayerGuid ~= GetPlayerGuid(source) then
		return false
	end

	return true
end }

PlayerTeamAssignment.WriteCheck { function(row)
	row.PlayerGuid = GetPlayerGuid(source)

	return true
end }

RegisterServerEvent('ocw:checkSpawnTeam')

AddEventHandler('ocw:checkSpawnTeam', function(requestedTeamId)
	local thisSource = source

	PlayerTeamAssignment.Query({
		PlayerGuid = GetPlayerGuid(source)
	}, function(data)
		local valid = false

		if #data > 0 then
			if data[1].TeamId == requestedTeamId then
				valid = true
			end
		end

		if valid then
			TriggerClientEvent('ocw:spawnForTeam', thisSource, requestedTeamId)
		else
			-- TODO: kick player
		end
	end)
end)

-- variable spaces
varSpaces = {}
local varSpaceIdx = 1

RegisterServerEvent('ocw:varSpace:reqSet')

AddEventHandler('ocw:varSpace:reqSet', function(spaceId, key, value)
	local space = varSpaces[spaceId]

	if space then
		space[key] = value
	end
end)

RegisterServerEvent('ocw:varSpace:resync')

AddEventHandler('ocw:varSpace:resync', function()
	for k, v in pairs(varSpaces) do
		TriggerClientEvent('ocw:varSpace:create', -1, k, v)
	end
end)

function CreateVariableSpace()
	local idx = varSpaceIdx
	varSpaceIdx = varSpaceIdx + 1

	varSpaces[idx] = {}

	TriggerClientEvent('ocw:varSpace:create', -1, idx, {})

	setmetatable(varSpaces[idx], {
		__newindex = function(t, key, value)
			print('sync-setting varspace', idx, key, value)

			TriggerClientEvent('ocw:varSpace:set', -1, idx, key, value)

			rawset(t, key, value)
		end
	})

	return idx
end

-- objective manager
local objectiveSpace = CreateVariableSpace()

print('hui')

varSpaces[objectiveSpace]['objective:1'] = {
	typeId = 'TestObjective',
	spaceId = CreateVariableSpace()
}

TriggerClientEvent('ocw:regObjSpace', -1, objectiveSpace)

AddEventHandler('ocw:varSpace:resync', function()
	print('resyncing from ', GetPlayerName(source))

	local identifiers = GetPlayerIdentifiers(source)

	for _, v in ipairs(identifiers) do
		print('identifier', v)
	end

	TriggerClientEvent('ocw:regObjSpace', -1, objectiveSpace)
end)

RegisterServerEvent('ocw:objective:complete')

AddEventHandler('ocw:objective:complete', function(objectiveId, spaceId)
	TriggerClientEvent('chatMessage', -1, 'hey', { 255, 0, 255 }, 'hey')

	varSpaces[objectiveSpace]['objective:' .. tostring(objectiveId)] = true
	varSpaces[spaceId] = nil

	TriggerClientEvent('ocw:varSpace:set', -1, objectiveSpace, 'objective:' .. tostring(objectiveId), true)
end)


AddEventHandler('chatMessage', function(source, name, message)
	if message:StartsWith('!ocw ') then
		TriggerClientEvent('ocw:chatCommand', source, message)

		CancelEvent()
	end

	if message:StartsWith('!obj') then
		varSpaces[objectiveSpace]['objective:' .. tostring(varSpaceIdx)] = {
			typeId = 'TestObjective',
			spaceId = CreateVariableSpace()
		}
	end
end)
