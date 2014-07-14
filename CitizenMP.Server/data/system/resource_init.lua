-- compat setfenv function
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

-- local resource init stuff (similar to client resource_init)
RegisterInitHandler(function(initScript, isPreParse)
	local env = {

	}

    TriggerEvent('getResourceInitFuncs', isPreParse, function(key, cb)
        env[key] = cb
    end)

	if not isPreParse then
		env.server_scripts = function(n)
            if type(n) == 'string' then
                n = { n }
            end

            for _, d in ipairs(n) do
                AddServerScript(d)
            end
        end

        env.server_script = env.server_scripts
	else
		-- and add our native items
		env.description = function(n)
			SetResourceInfo('description', n)
		end

		env.version = function(n)
			SetResourceInfo('version', n)
		end

        env.client_scripts = function(n)
            if type(n) == 'string' then
                n = { n }
            end

            for _, d in ipairs(n) do
                AddClientScript(d)
            end
        end

        env.client_script = env.client_scripts

        env.files = function(n)
            if type(n) == 'string' then
                n = { n }
            end

            for _, d in ipairs(n) do
                AddAuxFile(d)
            end
        end

        env.file = env.files

		env.dependencies = function(n)
			if type(n) == 'string' then
				n = { n }
			end

			for _, d in ipairs(n) do
				AddResourceDependency(d)
			end
		end

		env.dependency = env.dependencies
	end

	local mt = {
		__index = function(t, k)
			if rawget(t, k) ~= nil then return rawget(t, k) end

			if _G[k] then return _G[k] end

			-- as we're not going to return nothing here (to allow unknown directives to be ignored)
			local f = function()
                return f
            end

            return function() return f end
		end
	}

	setmetatable(env, mt)
	setfenv(initScript, env)

	initScript()
end)

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
