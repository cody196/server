-- in-memory spawnpoint array for this script execution instance
local spawnPoints = {}

-- auto-spawn enabled flag
local autoSpawnEnabled = true

-- loads a set of spawn points from a JSON string
function loadSpawns(spawnString)
    -- decode the JSON string
    local data = json.decode(spawnString)

    -- do we have a 'spawns' field?
    if not data.spawns then
        error("no 'spawns' in JSON data")
    end

    -- loop through the spawns
    for i, spawn in ipairs(data.spawns) do
        -- and add it to the list (validating as we go)
        addSpawnPoint(spawn)
    end
end

function addSpawnPoint(spawn)
    -- validate the spawn (position)
    if not tonumber(spawn.x) or not tonumber(spawn.y) or not tonumber(spawn.z) then
        error("invalid spawn position")
    end

    -- heading
    if not tonumber(spawn.heading) then
        error("invalid spawn heading")
    end

    -- model (try integer first, if not, hash it)
    local model = spawn.model

    if not tonumber(spawn.model) then
        model = GetHashKey(spawn.model, _r)
    end

    -- is the model actually a model?
    if not IsModelInCdimage(model) then
        error("invalid spawn model")
    end

    -- is is even a ped?
    if not IsThisModelAPed(model) then
        error("this model ain't a ped!")
    end

    -- overwrite the model in case we hashed it
    spawn.model = model

    -- all OK, add the spawn entry to the list
    table.insert(spawnPoints, spawn)
end

-- changes the auto-spawn flag
function setAutoSpawn(enabled)
    autoSpawnEnabled = enabled
end

-- function as existing in original R* scripts
local function freezePlayer(id, freeze)
    local player = ConvertIntToPlayerindex(id)
    SetPlayerControlForNetwork(player, not freeze, false)

    local ped = GetPlayerChar(player, _i)

    if not freeze then
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

-- spawns the current player at a certain spawn point index (or a random one, for that matter)
function spawnPlayer(spawnIdx)
    -- if the spawn isn't set, select a random one
    if not spawnIdx then
        spawnIdx = GenerateRandomIntInRange(1, #spawnPoints + 1, _i)
    end

    -- get the spawn from the array
    local spawn = spawnPoints[spawnIdx]

    -- validate the index
    if not spawn then
        echo("tried to spawn at an invalid spawn index\n")

        return
    end

    -- freeze the local player
    freezePlayer(GetPlayerId(), true)

    -- load the model for this spawn
    while not HasModelLoaded(spawn.model) do
        RequestModel(spawn.model)

        Wait(0)
    end

    -- change the player model
    ChangePlayerModel(GetPlayerId(), spawn.model)

    -- preload collisions for the spawnpoint
    RequestCollisionAtPosn(spawn.x, spawn.y, spawn.z)

    -- spawn the player
    ResurrectNetworkPlayer(GetPlayerId(), spawn.x, spawn.y, spawn.z, spawn.heading)

    -- gamelogic-style cleanup stuff
    local ped = GetPlayerPed()

    ClearCharTasksImmediately(ped)
    SetCharHealth(ped, 300) -- TODO: allow configuration of this?
    RemoveAllCharWeapons(ped)
    ClearWantedLevel(GetPlayerId())

    -- why is this even a flag?
    SetCharWillFlyThroughWindscreen(ped, false)

    -- set primary camera heading
    SetGameCamHeading(spawn.heading)

    -- and unfreeze the player
    freezePlayer(GetPlayerId(), false)
end

-- automatic spawning monitor thread, too
local respawnForced

CreateThread(function()
    -- main loop thing
    while true do
        Wait(50)

        -- check if we want to autospawn
        if autoSpawnEnabled then
            if IsNetworkPlayerActive(GetPlayerId()) then
                if (HowLongHasNetworkPlayerBeenDeadFor(GetPlayerId(), _r) > 2000) or respawnForced then
                    spawnPlayer()

                    respawnForced = false
                end
            end
        end
    end
end)

AddEventHandler('playerInfoCreated', function()
    loadSpawns(json.encode({
        spawns = {
            { x = -238.511, y = 954.025, z = 11.0803, heading = 90.0, model = 'ig_brucie' },
            { x = -310.001, y = 945.603, z = 14.3728, heading = 90.0, model = 'ig_bulgarin' },
        }
    }))
end)

AddEventHandler('playerActivated', function()
    respawnForced = true

    -- TEMPTEMP
    DoScreenFadeIn(500)
end)
