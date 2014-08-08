--Server commands
AddEventHandler('giveWeapon', function(weapon, ammo)
GiveWeaponToChar(GetPlayerPed(), weapon, ammo)
end)

function GetIDFromName(name)
  for i = 0,31 do
    if(string.lower(GetPlayerName(i)) == string.lower(name)) then
      return i
    end
  end
  return 255
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
  if(isNumber(nameorid) and IsNetworkPlayerActive(tonumber(nameorid))) then target = tonumber(nameorid)
  else target = GetIDFromName(nameorid)
  end

  if(target == 255) then
    ShowText("~r~Invalid player name/ID.")
    return
  end
  if(target == GetPlayerId(_r)) then
    ShowText("~r~You can't teleport to yourself.")
    return
  end
  pos = table.pack(GetCharCoordinates(GetPlayerChar(target, _i), _f, _f, _f))
  table.insert(pos, GetCharHeading(GetPlayerChar(target, _i), _f))
  SetCharCoordinatesNoOffset(GetPlayerPed(), pos[1], pos[2], pos[3])
  SetCharHeading(GetPlayerPed(), pos[4])
  ShowText("~g~You've successfully teleported to ~y~" .. GetPlayerName(target) .. "~g~.")
end)

function round(num, idp)
  local mult = 10^(idp or 0)
  return math.floor(num * mult + 0.5) / mult
end

AddEventHandler('sendPos', function()
  pos = table.pack(GetCharCoordinates(GetPlayerPed(), _f, _f, _f))
  TriggerServerEvent('savePos', round(pos[1], 5), round(pos[2], 5), round(pos[3], 5))
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
  else TriggerEvent('chatMessage', 'CarSpawner', { 0, 0x99, 255 }, "^1That's an unknown vehicle. Usage: /veh (vehicle name).")
  end
end)

function CreateNewCar(x, y, z, heading, model, throwin)
  CreateThread(function()
  RequestModel(model)
  while not HasModelLoaded(model) do Wait(0) end
  local car = CreateCar(model, pos[1], pos[2], pos[3], _i, true)
  SetCarHeading(car, heading)
  SetCarOnGroundProperly(car)
  MarkModelAsNoLongerNeeded(model)
  if(throwin == true) then
    WarpCharIntoCar(GetPlayerPed(), car)
  end
  end)
end

function isNumber(str)
  num = tonumber(str)
  if not num then return false
  else return true
  end
end