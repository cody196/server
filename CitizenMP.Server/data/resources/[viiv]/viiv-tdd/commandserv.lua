print("[GAME] Commandserv initialising...")

local weapons = {}
weapons["baseballbat"] = 1
weapons["poolcue"] = 2
weapons["knife"] = 3
weapons["grenade"] = 4
weapons["molotov"] = 5
weapons["pistol"] = 7
weapons["deagle"] = 9
weapons["deserteagle"] = 9
weapons["shotgun"] = 10
weapons["baretta"] = 11
weapons["microuzi"] = 12
weapons["uzi"] = 12
weapons["mp5"] = 13
weapons["ak47"] = 14
weapons["m4"] = 15
weapons["sniper"] = 16
weapons["m40a1"] = 17
weapons["rpg"] = 18
weapons["rocketlauncher"] = 18

--commands

RegisterServerEvent('savePos')
	AddEventHandler('savePos', function(x, y, z)
	local f,err = io.open("pos.txt","a")
	if not f then return print(err) end
	f:write(tonumber(x) .. ", " .. tonumber(y) .. ", " .. tonumber(z) .. "\\n")
	f:close()
end)

RegisterServerEvent('saveCar')
	AddEventHandler('saveCar', function(x, y, z)
	local f,err = io.open("cars.txt","a")
	if not f then return print(err) end
	f:write("car_generator { " .. x .. ", " .. y .. ", " .. z .. " }\\n")
	f:close()
end)

RegisterServerEvent('saveRandomPickup')
	AddEventHandler('saveRandomPickup', function(x, y, z)
	local f,err = io.open("pickups.txt","a")
	if not f then return print(err) end
	f:write("createPickup(getPickupType(" .. math.random(0, 2) .. "), 23, 200, " .. x .. ", " .. y .. ", " .. z .. ")\\n")
	f:close()
end)

--Chat/command stuff
AddEventHandler('chatMessage', function(source, name, message)
	if(string.len(message) == 0) then
		CancelEvent();
	end
	if(string.sub(message, 1, 1) == "/" and string.len(message) >= 2) then
		TriggerEvent('handleCommandEntered', source, message);
		CancelEvent();
	end
end)

RegisterServerEvent('handleCommandEntered')
AddEventHandler('handleCommandEntered', function(source, fullcommand)
	name = GetPlayerName(source)
	--print(name .. " sent command " .. command)
	--TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "We copy, command " .. command)
	command = string.split(fullcommand, ' ')

	if(command[1] == "/commands") then TriggerClientEvent('chatMessage', source, 'Commands', { 0, 0x99, 255 }, "^3/vehicles | /veh (vehicle name) | /vehcol (colour ID) (colour ID) | /repair | /flip | /heal | /armour | /givewep | /suicide | /tp")

	elseif command[1] == "/goto" or command[1] == "/tp" then
		if command[2] == nil then TriggerClientEvent('chatMessage', source, 'Teleporter', { 0, 0x99, 255 }, "^1Invalid name. Usage: /tp (target name).")
		else TriggerClientEvent('tpToPlayer', source, command[2])
	end

	elseif(command[1] == "/vehicles") then TriggerClientEvent('chatMessage', source, 'ModdedVehicles', { 0, 0x99, 255 }, "^3Admiral | Ambulance | Annihilator | SuperGT | Faction | Infernus | Maverick | nstockade (NOOSE van) | Police | Police2 | polpatriot | Sultan | Taxi | Turismo. ^1 Do not use any CAPITAL letters in /veh.")

	elseif command[1] == "/giverpg" then TriggerClientEvent('giveWeapon', source, 18, 7)

	elseif command[1] == "/pos" or command[1] == "/savepos" then TriggerClientEvent('sendPos', source)

	elseif command[1] == "/savecar" then TriggerClientEvent('sendSaveCar', source)

	elseif(command[1] == "/givewep") then
	if not weapons[command[2]] then TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "^1That's an unknown weapon.")
	else TriggerClientEvent('giveWeapon', source, weapons[command[2]], 50000)
	end

	elseif(command[1] == "/blip") then
	if command[2] == nil or command[3] == nil or isNumber(command[2]) == false or isNumber(command[3]) == false then TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "^1Wrong syntax.")
	else TriggerClientEvent('createBlip', source, command[2], command[3])
	end

	elseif(command[1] == "/weather") then
	if tonumber(command[2]) < 0 or tonumber(command[2]) > 10 then TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "^1That's an unknown weather ID.")
	else TriggerClientEvent('setWeather', source, command[2])
	end

	elseif(command[1] == "/time") then
	if isNumber(command[2]) == false then TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "^1Invalid hour of day.")
	else TriggerClientEvent('setTime', source, command[2])
	end

	elseif(command[1] == "/veh") then
	if command[2] == nil or string.len(command[2]) < 1 or string.len(command[2]) > 30 then TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "^1That's an unknown vehicle.")
	else TriggerClientEvent('createCarAtPlayerPos', source, command[2])
	end

	elseif(command[1] == "/vehcol") then
	if command[2] == nil or command[3] == nil or isNumber(command[2]) == false or isNumber(command[3]) == false then TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "^1Invalid carcolours. Usage: /vehcol (id1) (id2).")
	else TriggerClientEvent('changeCarColor', source, command[2], command[3])
	end

	elseif command[1] == "/heal" then TriggerClientEvent('setHealth', source, 200)

	elseif command[1] == "/armor" or command[1] == "/armour" then TriggerClientEvent('giveArmour', source, 200)

	elseif command[1] == "/flip" then TriggerClientEvent('flipVehicle', source)

	elseif command[1] == "/fix" or command[1] == "/repair" then TriggerClientEvent('fixVehicle', source)

	elseif command[1] == "/kill" or command[1] == "/suicide" then TriggerClientEvent('kill', source)

	elseif command[1] == "/godmode" then TriggerClientEvent('godmode', source)

	else TriggerClientEvent('chatMessage', source, 'Server', { 0, 0x99, 255 }, "^1Sorry, but we don't know that command.")
	end
end)

function startswith(sbig, slittle)
  if type(slittle) == "table" then
    for k,v in ipairs(slittle) do
      if string.sub(sbig, 1, string.len(v)) == v then
        return true
      end
    end
    return false
  end
  return string.sub(sbig, 1, string.len(slittle)) == slittle
end

function isNumber(str)
	num = tonumber(str)
	if not num then return false
	else return true
	end
end

function string:split(delimiter)
  local result = { }
  local from  = 1
  local delim_from, delim_to = string.find( self, delimiter, from  )
  while delim_from do
    table.insert( result, string.sub( self, from , delim_from-1 ) )
    from  = delim_to + 1
    delim_from, delim_to = string.find( self, delimiter, from  )
  end
  table.insert( result, string.sub( self, from  ) )
  return result
end

print("[GAME] Commandserv initialised")
