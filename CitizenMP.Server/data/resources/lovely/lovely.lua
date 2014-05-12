local hi = 5

function cake(a1, a2, a3)
	echo("a1: " .. a1 .. "\n")
	echo("a2: " .. a2.hi .. "\n")
	echo("a3: " .. a3 .. "\n")

	return a2
end

AddEventHandler("resourceStarted", function(resourceName)
	echo("started " .. resourceName .. "\n")
end)

AddEventHandler("resourceStopping", function(resourceName)
	echo("stopping " .. resourceName .. "\n")

	if resourceName == 'lovely' then
		echo("WHAT THE FUCK THAT'S US\n");
	end
end)

AddEventHandler("playerBaked", function(playerName)
	echo("baked " .. playerName .. " :)\n")
end)

AddEventHandler("bigDick", function(a1, a2, a3)
	echo("a1: " .. a1 .. "\n")
	echo("a2: " .. a2.a .. "\n")
	echo("a3: " .. a3 .. "\n")

	PrintStringWithLiteralStringNow("STRING", "a2 = " .. a2.a, 5000, true)
end)

CreateThread(function()
	--exports.lovely:cake('hi', { hi = 'bye' }, 31)

	while true do
		Wait(0)

		--PrintStringWithLiteralStringNow("STRING", exports.lovely:cake('hi', { hi = 'bye' }, 31).hi, 50, true)
		--[[
		A: (0.3363037, -0.19152832, -0.12501526)
B: (-0.36584473, 0.12597656, 0.12501526)
C: (0.3363037, -0.111083984, -0.20314026)
D: (-0.30688477, 0.17675781, 0.20314026)
E: (0.3067627, -0.17663574, -0.20314026)
F: (-0.33642578, 0.111328125, 0.20314026)
G: (0.36572266, -0.12585449, -0.12501526)
H: (-0.33642578, 0.19165039, 0.12501526)]]

		if IsGameKeyboardKeyJustPressed(50) then
			TriggerRemoteEvent("bigDick", -1, "a", {a = 'b'}, 42)
		end

		local x, y, z = GetCharCoordinates(GetPlayerPed(), _f, _f, _f)

		y = y + 2

		local draw = function(xx, yy, zz, i)
			xx = x + xx
			yy = y + yy
			zz = z + zz

			if i == 1 then
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 255, 0, 0)
			elseif i == 2 then
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 0, 255, 0)
			elseif i == 3 then
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 0, 0, 255)
			elseif i == 4 then
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 0, 0, 0)
			elseif i == 5 then
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 255, 255, 0)
			elseif i == 6 then
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 0, 255, 255)
			elseif i == 7 then
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 255, 0, 255)
			else
				DrawCorona(xx, yy, zz, 2.5, 0, 0, 255, 255, 255)
			end
		end

		draw(0.3363037, -0.19152832, -0.12501526, 1)
		draw(-0.36584473, 0.12597656, 0.12501526, 2)
		draw(0.3363037, -0.111083984, -0.20314026, 3)
		draw(-0.30688477, 0.17675781, 0.20314026, 4)
		draw(0.3067627, -0.17663574, -0.20314026, 5)
		draw(-0.33642578, 0.111328125, 0.20314026, 6)
		draw(0.36572266, -0.12585449, -0.12501526, 7)
		draw(-0.33642578, 0.19165039, 0.12501526, 8)
	end	
end)