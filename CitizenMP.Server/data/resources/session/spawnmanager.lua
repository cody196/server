local spawns = {}
local respawnDelay = 2000

function loadSpawns(filename)
	echo("acting as if loading spawns from " .. filename .. "\n")

	local tempCoords = {
		{ -238.511, 954.025, 11.0803 },
		{ -310.001, 945.603, 14.3728 },
		{ -309.911, 922.181, 14.2549 },
		{ -138.094, 926.817, 11.4885 },
		{ -96.6894, 922.325, 14.3601 },
		{ -96.316, 946.309, 14.524 }
	}

	for k, i in ipairs(tempCoords) do
		table.insert(spawns, {
			x = i[1], y = i[2], z = i[3], heading = 0.0, model = GetPlayersettingsModelChoice(_r)
		})
	end
end

local respawnForced = false

function forceRespawn()
	respawnForced = true
end

AddEventHandler('playerJoinReady', function(playerID)
	if playerID ~= GetPlayerId() then
		return
	end

	forceRespawn()
end)

local function getSpawnPoint()
	local spawnIdx = math.random(1, #spawns)

	return spawns[spawnIdx]
end

local function advancedFade(fadeIn, timer)
	SetScreenFade(GetScreenViewportId(_i), 0, 0, fadeIn, 0, 0, 0, 255, timer, 1.0001, 1.0001)
end

local function freezePlayer(id, unfreeze)
	local player = ConvertIntToPlayerindex(id)
	SetPlayerControlForNetwork(player, unfreeze, false)
	
	local ped = GetPlayerChar(player, _i)
	
	if unfreeze then
		if not IsCharVisible(ped) then
			SetCharVisible(ped, true)
		end
		
		if not IsCharInAnyCar(ped) then
			SetCharCollision(ped, true)
		end
		
		FreezeCharPosition(ped, false)
		SetCharNeverTargetted(ped, false)
		SetPlayerInvincible(player, false)
	else
		if IsCharVisible(ped) then
			SetCharVisible(ped, false)
		end
		
		SetCharCollision(ped, false)
		FreezeCharPosition(ped, true)
		SetCharNeverTargetted(ped, true)
		SetPlayerInvincible(player, true)
		RemovePtfxFromPed(ped)
		
		if not IsCharFatallyInjured(ped) then
			ClearCharTasksImmediately(ped)
		end
	end	
end

function loadScene(x, y, z)
	StartLoadScene(x, y, z)
	
	while not UpdateLoadScene() do
		networkTimer = GetNetworkTimer(_i)
		
		exports.session:serviceHostStuff()
	end
end

local spawningEnabled = true

function disableSpawning()
	spawningEnabled = false
end

function enableSpawning()
	spawningEnabled = true
end

CreateThread(function()
	math.randomseed(GenerateRandomIntInRange(1, 5000000, _i))

	loadSpawns('dick')

	Wait(100)

	while true do
		Wait(0)

		if IsNetworkPlayerActive(GetPlayerId()) and spawningEnabled then
			if (HowLongHasNetworkPlayerBeenDeadFor(GetPlayerId(), _r) > respawnDelay) or respawnForced then
				echo("spawning local player?!\n")

				if IsScreenFadedIn() then
					advancedFade(false, 500)

					echo("spawning local player?! a\n")

					while not IsScreenFadedOut() do
						Wait(0)
					end

					echo("spawning local player?! b\n")
				else
					freezePlayer(GetPlayerId(), false)

					--TriggerEvent('onPlayerSpawning', GetPlayerId(), spawn)

					echo("spawning local player?! c\n")

					local spawn = getSpawnPoint()

					if not HasModelLoaded(spawn.model) then
						RequestModel(spawn.model)

						LoadAllObjectsNow()
					end

					ChangePlayerModel(GetPlayerIndex(), spawn.model)
					
					RequestCollisionAtPosn(spawn.x, spawn.y, spawn.z)
					ResurrectNetworkPlayer(GetPlayerId(), spawn.x, spawn.y, spawn.z, spawn.heading)
					
					local ped = GetPlayerPed()
					local player = GetPlayerIndex()
					
					-- testing variation stuff
					SetCharComponentVariation(ped, 0, 0, 0)
					SetCharComponentVariation(ped, 1, 3, 1)
					
					ClearCharTasksImmediately(ped)
					SetCharHealth(ped, 300)
					RemoveAllCharWeapons(ped)
					ClearWantedLevel(player)
					
					SetCharWillFlyThroughWindscreen(ped, false)
					SetGameCamHeading(spawn.heading)

					ForceLoadingScreen(true)
					loadScene(spawn.x, spawn.y, spawn.z)
					ForceLoadingScreen(false)

					--TriggerEvent('onPlayerSpawned', GetPlayerId(), spawn)
					
					advancedFade(true, 500)
					echo("i'm fading, i'm fading, i'm fading\n")

					freezePlayer(GetPlayerId(), true)

					respawnForced = false
				end
			end
		end
	end
end)