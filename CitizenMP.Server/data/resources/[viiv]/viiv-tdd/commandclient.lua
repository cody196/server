--Client event handler, usually for commands

AddEventHandler('giveWeapon', function(weapon, ammo)
GiveWeaponToChar(GetPlayerPed(), weapon, ammo)
end)

AddEventHandler('kickPlayer', function(player)
  p = GetPObjFromName(player)
  if not IsNetworkPlayerActive(p) or not IsThisMachineTheServer() then
    return
  end
  NetworkKickPlayer(p)
  ShowText("~g~Kicking player ~r~" .. GetPlayerName(p))
end)

AddEventHandler('playAnimStuff', function(animationid, animationset, speed, loop, x, y, z, ms)
  CreateThread(function()
    RequestAnims(animationset)
    while not HaveAnimsLoaded(animationset) do Wait(0) end
    TaskPlayAnimNonInterruptable(GetPlayerPed(), animationid, animationset, speed, loop, x, y, z, -1)
  end)
end)

AddEventHandler('createCheckpoint', function()
  pos = table.pack(GetCharCoordinates(GetPlayerPed(), _f, _f, _f))
  CreateCheckpoint(3, pos[1]+6, pos[2], pos[3], pos[1], pos[2]+10, pos[3], 1.0)
  ShowText("Checkpoint created.")
  end)

AddEventHandler('hudOff', function()
  DisplayAmmo(false)
  DisplayCash(false)
  DisplayHud(false)
  DisplayRadar(false)
  end)
  
AddEventHandler('hudOn', function()
  DisplayAmmo(true)
  DisplayCash(true)
  DisplayHud(true)
  DisplayRadar(true)
  end)

AddEventHandler('playAudioStuff', function(audio)
  PlayAudioEvent(audio)
  end)

function GetIDFromName(name)
  local players = GetPlayers()
  for _, i in ipairs(players) do
    if(string.lower(i.name) == string.lower(name)) then
      return i.serverId
    end
  end
  return nil
end

function GetPObjFromName(name)
  local players = GetPlayers()
  for _, i in ipairs(players) do
    if(string.lower(i.name) == string.lower(name)) then
      return i
    end
  end
  return nil
end

function ShowText(text, timeout)
  if(timeout == nil) then PrintStringWithLiteralStringNow("STRING", text, 2000, 1)
  else PrintStringWithLiteralStringNow("STRING", text, timeout, 1)
  end
end

AddEventHandler('showScreenMsg', function(text, timeout)
  if(timeout == nil) then PrintStringWithLiteralStringNow("STRING", text, 2000, 1)
  else PrintStringWithLiteralStringNow("STRING", text, timeout, 1)
  end
end)

AddEventHandler('tpToPlayer', function(nameorid)
  local target = 255
  if(isNumber(nameorid) and GetPlayerByServerId(tonumber(nameorid)) ~= nil) then target = GetPlayerByServerId(tonumber(nameorid))
  elseif(GetIDFromName(nameorid) ~= nil) then target = GetPlayerByServerId(GetIDFromName(nameorid))
  end

  if(target == 255) then
    ShowText("~r~Invalid player name/ID.")
    return
  end
  if(target.name == GetPlayerName(GetPlayerId(), _s)) then
    ShowText("~r~You can't teleport to yourself.")
    return
    end
  if(IsCharInAnyCar(target.ped)) then
    if(IsCharInAnyCar(GetPlayerPed()) and GetCarCharIsUsing(target.ped, _i) == GetCarCharIsUsing(GetPlayerPed(), _i)) then
    ShowText("~r~You can't teleport to yourself.")
    return
    end
    if(GetMaximumNumberOfPassengers(GetCarCharIsUsing(target.ped, _i), _i) == GetNumberOfPassengers(GetCarCharIsUsing(target.ped, _i), _i)) then
      ShowText("~r~There's no more free seats in " .. target.name .. "'s vehicle! ~g~Warping to the vehicle.")
      return TeleportToChar(target.ped)
    end
    WarpCharIntoCarAsPassenger(GetPlayerPed(), GetCarCharIsUsing(target.ped, _i))
    return ShowText("~g~You've successfully teleported into ~y~" .. target.name .. "~g~'s vehicle.")
  end
  TeleportToChar(target.ped)
  ShowText("~g~You've successfully teleported to ~y~" .. target.name .. "~g~.")
end)

function TeleportToChar(char)
  pos = table.pack(GetCharCoordinates(char, _f, _f, _f))
  table.insert(pos, GetCharHeading(char, _f))
  if(IsCharInAnyCar(GetPlayerPed())) then
    WarpCharFromCarToCoord(GetPlayerPed(), pos[1], pos[2], pos[3])
  end
  SetCharCoordinatesNoOffset(GetPlayerPed(), pos[1], pos[2], pos[3])
  SetCharHeading(GetPlayerPed(), pos[4])
end

function round(num, idp)
  local mult = 10^(idp or 0)
  return math.floor(num * mult + 0.5) / mult
end

AddEventHandler('sendPos', function()
  pos = table.pack(GetCharCoordinates(GetPlayerPed(), _f, _f, _f))
  table.insert(pos, GetCharHeading(GetPlayerPed(), _f))
  TriggerServerEvent('savePos', round(pos[1], 5), round(pos[2], 5), round(pos[3], 5), pos[4])
  TriggerEvent('createBlip', 63, 2)
end)

AddEventHandler('sendSaveCar', function()
pos = table.pack(GetCarCoordinates(GetCarCharIsUsing(GetPlayerPed(), _i), _f, _f, _f))
TriggerServerEvent('saveCar', round(pos[1], 5), round(pos[2], 5), round(pos[3], 5))
end)

AddEventHandler('createBlip', function(sprite, color)
pos = table.pack(GetCharCoordinates(GetPlayerPed(), _f, _f, _f))
local blip = AddBlipForCoord(pos[1], pos[2], pos[3], _i)
ChangeBlipSprite(blip, tonumber(sprite))
ChangeBlipColour(blip, tonumber(color))
ShowText("~g~Blip created.")
end)

AddEventHandler('changeCarColor', function(col1, col2)
	if(IsCharInAnyCar(GetPlayerPed()) == false) then 
	return ShowText("~r~You are not in any vehicle!")
	end
	ChangeCarColour(GetCarCharIsUsing(GetPlayerPed(), _i), tonumber(col1), tonumber(col2))
	ShowText("~g~You have set your vehicles' colour.")
end)

AddEventHandler('setWeather', function(weatherid)
ForceWeatherNow(weatherid)
end)

AddEventHandler('setTime', function(hour)
SetTimeOfDay(hour)
end)

AddEventHandler('setPos', function(x, y, z)
  ShowText("~g~Warping to server sent coordinates ~y~" .. x .. " " .. y .. " " .. z .. " ~g~.")
  SetCharCoordinatesNoOffset(GetPlayerPed(), x, y, z)
end)

AddEventHandler('setHealth', function(amount)
SetCharHealth(GetPlayerPed(), amount)
end)

AddEventHandler('giveArmour', function(amount)
AddArmourToChar(GetPlayerPed(), amount)
end)

AddEventHandler('fixVehicle', function(amount)
	if(IsCharInAnyCar(GetPlayerPed()) == false) then 
	return ShowText("~r~You are not in any vehicle!")
	end
	FixCar(GetCarCharIsUsing(GetPlayerPed(), _i))
  ShowText("~g~You have fixed your vehicle.")
end)

AddEventHandler('flipVehicle', function(amount)
  if(IsCharInAnyCar(GetPlayerPed()) == false) then 
  return ShowText("~r~You are not in any vehicle!")
  end
  FlipVehicle(GetCarCharIsUsing(GetPlayerPed(), _i))
  ShowText("~g~You have flipped your vehicle.")
end)

function FlipVehicle(vehicle)
  pos = table.pack(GetCarCoordinates(vehicle, _f, _f, _f))
  table.insert(pos, GetCarHeading(vehicle, _f))
  SetCarCoordinates(vehicle, pos[1], pos[2], pos[3])
  SetCarHeading(vehicle, pos[4])
end

AddEventHandler('giveHealth', function(amount)
SetCharHealth(GetPlayerPed(), GetCharHealth(GetPlayerPed(), _i)+amount)
end)

AddEventHandler('takeHealth', function(amount)
SetCharHealth(GetPlayerPed(), GetCharHealth(GetPlayerPed(), _i)-amount)
end)

AddEventHandler('cleanYourCar', function()
  if(not IsCharInAnyCar(GetPlayerPed())) then 
  return ShowText("~r~You are not in any vehicle!")
  end
  SetVehicleDirtLevel(GetCarCharIsUsing(GetPlayerPed(), _i), 0.0)
  WashVehicleTextures(GetCarCharIsUsing(GetPlayerPed(), _i), 255)
  ShowText("~g~You've successfully cleaned your vehicle.")
end)

AddEventHandler('kill', function(amount)
SetCharHealth(GetPlayerPed(), 0)
ShowText("~y~You have committed suicide.")
end)

AddEventHandler('godmode', function(amount)
SetCharInvincible(GetPlayerPed(), true)
ShowText("~y~Godmode on.")
end)

AddEventHandler('createCarAtPlayerPos', function(modelname)
  if IsModelInCdimage(GetHashKey(modelname, _r)) then
    pos = table.pack(GetCharCoordinates(GetPlayerPed(), _f, _f, _f))
    table.insert(pos, GetCharHeading(GetPlayerPed(), _f))
    CreateNewCar(pos[1], pos[2], pos[3], pos[4], GetHashKey(modelname), true)
	ShowText("~g~You've spawned the ~y~" .. modelname .. "~g~.")
  else TriggerEvent('chatMessage', '', { 0, 0x99, 255 }, "^1That's an unknown vehicle. Usage: /veh (vehicle name).")
  end
end)

function CreateNewCar(x, y, z, heading, model, throwin)
  CreateThread(function()
  RequestModel(model)
  while not HasModelLoaded(model) do Wait(0) end
  local car = CreateCar(model, x, y, z, _i, true)
  SetCarHeading(car, heading)
  SetCarOnGroundProperly(car)
  SetVehicleDirtLevel(car, 0.0)
  WashVehicleTextures(car, 255)
  if(throwin == true) then
    WarpCharIntoCar(GetPlayerPed(), car)
  end
  MarkModelAsNoLongerNeeded(model)
  MarkCarAsNoLongerNeeded(car)
  end)
end

function IsPlayerNearCoords(x, y, z, radius)
	local pos = table.pack(GetCharCoordinates(GetPlayerPed(), _f, _f, _f))
	local dist = GetDistanceBetweenCoords3D(x, y, z, pos[1], pos[2], pos[3], _f);
	if(dist < radius) then return true
	else return false
	end
end

function IsPlayerNearChar(char, radius)
	local pos = table.pack(GetCharCoordinates(GetPlayerPed(), _f, _f, _f))
	local pos2 = table.pack(GetCharCoordinates(char, _f, _f, _f))
	local dist = GetDistanceBetweenCoords3D(pos2[1], pos2[2], pos2[3], pos[1], pos[2], pos[3], _f);
	if(dist <= radius) then return true
	else return false
	end
end

function isNumber(str)
  num = tonumber(str)
  if not num then return false
  else return true
  end
end