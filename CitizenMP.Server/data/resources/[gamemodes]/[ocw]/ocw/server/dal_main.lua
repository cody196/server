DAL = {}

local dal_priv = {
	namespace = GetInvokingResource(),
	db = 'http://localhost:5984',
	metafields = {}
}

local function dbcall(method, schemaName, cb, subName, data)
	if data then
		data = json.encode(data)
	end

	schemaName = schemaName:lower()
	subName = subName and ('/' .. subName) or ''

	local url = dal_priv.db .. '/' .. dal_priv.namespace .. '_' .. schemaName .. subName

	PerformHttpRequest(url, function(code, result, headers)
		if not cb then
			return
		end

		if code ~= 0 then
			cb(code, nil)
			return
		end

		print(code, result)

		local decoded, pos, err = json.decode(result)

		if err then
			print(err)
		end

		cb(0, decoded)
	end, method, data, {
		["Content-Type"] = 'application/json'
	})
end

function DAL.Namespace(namespace)
	dal_priv.namespace = namespace
end

function DAL.Schema(schemaName)
	local getEventName = string.format('%s:get%s', dal_priv.namespace, schemaName)
	local retEventName = string.format('%s:ret%s', dal_priv.namespace, schemaName)
	local saveEventName = string.format('%s:save%s', dal_priv.namespace, schemaName)
	local retSaveEventName = string.format('%s:retSave%s', dal_priv.namespace, schemaName)

	dbcall('PUT', schemaName, function(code, result)
		print(code, result)
	end)

	local schema = {
		indices = {},
		queryChecks = {},
		writeChecks = {}
	}

	RegisterServerEvent(saveEventName)

	AddEventHandler(saveEventName, function(query)
		local lsource = source

		if not query.version or query.version ~= 1 then
			print("DAL query version invalid.")

			return
		end

		local object = query.data

		if #schema.writeChecks > 0 then
			for _, check in ipairs(schema.writeChecks) do
				if not check(object) then
					print('write not allowed from ' .. GetPlayerName(source))

					TriggerClientEvent(retSaveEventName, lsource, {
						version = 1,
						queryId = query.id,
						err = 403
					})

					return
				end
			end
		end

		local subUrl = object._id

		dbcall(subUrl and 'PUT' or 'POST', schemaName, function(code, data)
			if code ~= 0 then
				TriggerClientEvent(retSaveEventName, lsource, {
					version = 1,
					queryId = query.id,
					err = code
				})
			else
				TriggerClientEvent(retSaveEventName, lsource, {
					version = 1,
					queryId = query.id,
					id = data.id,
					rev = data.rev,
					err = 0
				})
			end
		end, subUrl, object)
	end)

	RegisterServerEvent(getEventName)

	AddEventHandler(getEventName, function(query)
		local lsource = source

		if not query.version or query.version ~= 1 then
			print("DAL query version invalid.")

			return
		end

		local where = {}

		if query.query then
			for k, v in pairs(query.query) do
				if type(v) == 'table' then
					if v.__type and v.__type == 'MetaField' then
						if v.fieldId and dal_priv.metafields[v.fieldId] then
							where = { k, dal_priv.metafields[v.fieldId](v) }
						end
					end
				else
					where = { k, v }
				end
			end
		end

		if #schema.queryChecks > 0 and #where > 0 and source ~= -1 then
			local q = {}
			q[where[1]] = where[2]

			for _, check in ipairs(schema.queryChecks) do
				if not check(q) then
					print('query not allowed from ' .. GetPlayerName(source))

					return
				end
			end
		end

		local subUrl = '_all_docs?include_docs=true'

		if #where > 0 then
			subUrl = '_design/' .. where[1] .. '/_view/query?key=' .. json.encode(where[2])
		end

		dbcall('GET', schemaName, function(code, data)
			if code ~= 0 then
				print('hm ' .. code)
				return
			end

			local rows = {}

			for _, row in ipairs(data.rows) do
				if not row.doc then
					row.doc = row.value
				end
				
				if not row.doc._id then
					row.doc._id = row.id
				end

				table.insert(rows, row.doc)
			end

			if lsource ~= -1 then
				TriggerClientEvent(retEventName, lsource, {
					version = 1,
					queryId = query.id,
					data = rows
				})
			else
				TriggerEvent(retEventName, {
					version = 1,
					queryId = query.id,
					data = rows
				})
			end

			print('returned properly and safely to ' .. GetPlayerName(lsource))
		end, subUrl)
	end)

	-- private event
	local retCallbacks = {}
	local retId = 0

	AddEventHandler(retEventName, function(object)
		if object.version ~= 1 then
			return
		end

		local cb = retCallbacks[object.queryId]

		if cb then
			cb(object.data)

			retCallbacks[object.queryId] = nil
		end
	end)

	function schema.Query(where, cb)
		local internalId = retId

		retCallbacks[internalId] = cb

		retId = retId + 1

		TriggerEvent(getEventName, {
			version = 1,
			where = where,
			id = internalId
		})
	end

	function schema.Index(field)
		dbcall('GET', schemaName, function(code, data)
			if code ~= 0 then
				data = {}
			end

			if code ~= 0 or not data.version or data.version ~= 1 then
				dbcall('PUT', schemaName, function(code, data)
					if code ~= 0 then
						print(code .. ' while creating index ' .. field)

						return
					end

					table.insert(schema.indices, field)
				end, '_design/' .. field, {
					_rev = data._rev,
					language = 'javascript',
					version = 1,
					views = {
						query = {
							map = 'function(doc) { if (doc.' .. field .. ') emit(doc.' .. field .. ', doc) }'
						}
					}
				})
			end
		end, '_design/' .. field)
	end

	function schema.Metafield(name)
		return function(cbTable)
			dal_priv.metafields[name] = cbTable[1]
		end
	end

	function schema.QueryCheck(cbTable)
		table.insert(schema.queryChecks, cbTable[1])
	end

	function schema.WriteCheck(cbTable)
		table.insert(schema.writeChecks, cbTable[1])
	end

	return schema
end