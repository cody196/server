-- nothing, yet

-- TODO: cleanup RPC environment stuff on coroutine end/error
local function RunRPCFunction(f, env)
    local co = coroutine.create(f)
    env.__co = client

    local success, err = coroutine.resume(co)

    if success then
        env.SendEvents()
    else
        print(err)
    end
end

local rpcIdx = 1
local rpcEnvironments = {}

if not setfenv then
  local function findenv(f)
    local level = 1
    repeat
      local name, value = debug.getupvalue(f, level)
      if name == '_ENV' then return level, value end
      level = level + 1
    until name == nil
    return nil end
  getfenv = function (f) return(select(2, findenv(f)) or _G) end
  setfenv = function (f, t)
    local level = findenv(f)
    if level then debug.setupvalue(f, level, t) end
    return f end
end

function CreateRPCContext(cl, f)
    local idx = rpcIdx
    rpcIdx = rpcIdx + 1

    local key = cl .. '_' .. idx

    local env = {
        getIdx = function()
            return idx
        end,
        getSource = function()
            return cl
        end
    }

    local lastEnv = _ENV

    setmetatable(env, {__index = _G})

    local _ENV = env
    rpcEnvironments[key] = env

    setfenv(f, env)

    local fRun = f()

    local virtenv_init = loadfile('system/virtenv_init.lua', 't', env)
    virtenv_init()

    _ENV = lastEnv

    RunRPCFunction(fRun, env)
end

RegisterServerEvent('svRpc:results')

AddEventHandler('svRpc:results', function(results)
    if not results.idx then
        return
    end

    local key = source .. '_' .. results.idx

    if not rpcEnvironments[key] then
        return
    end

    rpcEnvironments[key].HandleResults(results)
end)
