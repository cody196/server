-- session manager, manages finding, joining and hosting sessions
SessionManager = {}

function SessionManager:init()
	self:setCallback('foundGames', nil)
	self:setCallback('joinFailure', nil)
	self:setCallback('joinSuccess', nil)
	self:setCallback('joinReturned', nil)
	self:setCallback('hostReturned', nil)

	-- status callbacks for UI
	self:setCallback('stateUpdated', nil)
	self:setCallback('joiningGame', nil)

	-- set state after callbacks are set up, or we may reference nil
	self:setState('new')
end

function SessionManager:process_new()
	-- no-op
end

function SessionManager:process_idle()

end

-- start finding games matching default parameters
function SessionManager:process_find()
	NetworkFindGame(self.gameMode, self.ranked, self.episode, self.maxTeams)

	self:setState('finding')
end

-- finding games, start joining games (from the native join list) after results are obtained
function SessionManager:process_finding()
	if NetworkFindGamePending() then
		return
	end

	self:setState('idle')

	if self.onFoundGames then
		self:onFoundGames(NetworkGetNumberOfGames(_r))
	end
end

-- start joining a game
function SessionManager:process_join()
	if NetworkJoinGame(self.joinIndex) then
		self:setState('joining')
	end
end

-- joining a game
function SessionManager:process_joining()
	if NetworkJoinGamePending() then
		return
	end

	self:setState('idle')

	-- return the callback whether or not we errored
	if NetworkJoinGameSucceeded() then
		self:onJoinReturned(nil)
	else
		self:onJoinReturned(true)
	end
end

-- start hosting a game
function SessionManager:process_host()
	if not NetworkHostGameE1(self.gameMode, self.ranked, self.slots, self.private, self.episode, self.maxTeams) then
		self:onHostReturned('NETWORK_HOST_GAME_E1 returned false')

		self:setState('idle')
		return
	end

	self:setState('hosting')
end

-- hosting a game (e.g. starting to host)
function SessionManager:process_hosting()
	if NetworkHostGamePending() then
		return
	end

	self:setState('idle')

	-- return the callback whether or not we errored
	if NetworkHostGameSucceeded() then
		self:onHostReturned(nil)
	else
		self:onHostReturned('NETWORK_HOST_GAME_SUCCEEDED returned false')
	end
end

-- convenience function to join a game
function SessionManager:joinGame(index, callback)
	-- it may be this is a table result from a convenience function
	if type(index) == 'table' and index.gameIndex then
		self.joinIndex = index.gameIndex
	else
		self.joinIndex = index
	end

	self:setState('join')
	TriggerEvent('sessionJoining', self.joinIndex + 1, NetworkGetNumberOfGames(_r), "ii" .. self.joinIndex)

	self:onJoiningGame(self.joinIndex + 1, NetworkGetNumberOfGames(_r), "ii" .. self.joinIndex)

	if callback then
		self:setCallback('joinReturned', function(selfie, err)
			callback(err)
		end)
	else
		self:setCallback('joinReturned', nil)
	end
end

-- convenience function to host a game
function SessionManager:hostGame(gameMode, ranked, slots, private, episode, maxTeams, callback)
	self.gameMode = gameMode
	self.ranked = ranked
	self.slots = slots
	self.private = private
	self.episode = episode
	self.maxTeams = maxTeams

	self:setState('host')

	if callback then
		self:setCallback('hostReturned', function(selfie, err)
			callback(err)
		end)
	else
		self:setCallback('hostReturned', nil)
	end
end

-- convenience function to find games
function SessionManager:findGames(gameMode, ranked, episode, maxTeams, callback)
	self.gameMode = gameMode
	self.ranked = ranked
	self.episode = episode
	self.maxTeams = maxTeams

	self:setState('find')

	if callback then
		self:setCallback('foundGames', function(selfie, gameCount)
			-- get data for the found games (again, convenience :) )
			local foundGames = {}

			-- loop through all the returned games
			for i = 1, gameCount do
				table.insert(foundGames, {
					gameIndex = i - 1,
					hostName = NetworkGetHostName(i - 1, _s),
					serverName = NetworkGetHostServerName(i - 1, _s),
					latency = NetworkGetHostLatency(i - 1, _r),
					-- other unrelated things to be added
				})
			end

			callback(foundGames)
		end)
	else
		self:setCallback('foundGames', nil)
	end
end

-- empty handlers for default callbacks
function SessionManager:defaultOnJoinSuccess()

end

function SessionManager:defaultOnHostReturned(err)
	echo("defaultOnHostReturned called, this should be overridden\n")
end

function SessionManager:defaultOnJoinReturned()
	echo("defaultOnJoinReturned called, this should be overridden\n")
end

function SessionManager:defaultOnJoinFailure()
	echo("joining all games failed, such fun. probably should host!\n")

	self:hostGame(self.gameMode, self.ranked, self.slots, self.private, self.episode, self.maxTeams)
end

function SessionManager:defaultOnStateUpdated()

end

function SessionManager:defaultOnJoiningGame()

end

-- default 'found games' handler; ends up joining the retrieved sessions one by one
function SessionManager:defaultOnFoundGames(count)
	if count == 0 then
		self:onJoinFailure()

		return
	end

	local curGameToJoin = 0

	local joinCB = function(err)
		if err then
			curGameToJoin = curGameToJoin + 1

			if curGameToJoin >= count then
				self:onJoinFailure()
				return
			end

			self:joinGame(curGameToJoin, joinCB)
		else
			self.onJoinSuccess()
		end
	end

	self:joinGame(curGameToJoin, joinCB)
end

function SessionManager:setCallback(type, func)
	local typeU = type:gsub('^%l', string.upper)
	local name = 'on' .. typeU

	if not func then
		local defaultName = 'defaultOn' .. typeU

		self[name] = self[defaultName]
	else
		self[name] = func
	end
end

function SessionManager:setState(state)
	-- validate the state name
	local procFunc = 'process_' .. state

	if not self[procFunc] then
		error('tried to set bad session manager state: ' .. state)
	end

	if self.state then
		echo('SessionManager transitioning from ' .. self.state .. ' to ' .. state .. "\n")
	end

	TriggerEvent('sessionStateChanged', state)

	self.state = state
end

function SessionManager:tick()
	-- run the state-specific processing function
	local procFunc = 'process_' .. self.state

	if not self[procFunc] then
		error('unknown session manager state ' .. self.state)
	end

	self[procFunc](self)
end

function reset()
	SessionManager:init()
end

function findSessions(gameMode)
	SessionManager:findGames(gameMode, false, 0, 0, function(foundGames)
		TriggerEvent('sessionsFound', foundGames)
	end)
end

function joinSession(index)
	SessionManager:joinGame(index, function(err)
		if err then
			TriggerEvent('sessionJoinFailed', err)
		else
			TriggerEvent('sessionJoined')
		end
	end)
end

function hostSession(gameMode, slots)
	SessionManager:hostGame(gameMode, false, slots, false, 0, 0, function(err)
		if err then
			TriggerEvent('sessionHostFailed', err)
		else
			TriggerEvent('sessionHosted')
		end
	end)
end

CreateThread(function()
	while true do
		Wait(0)

		if SessionManager.state then
			SessionManager:tick()
		end
	end
end)