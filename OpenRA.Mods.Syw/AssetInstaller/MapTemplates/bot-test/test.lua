-- Japanese bot (South, 77,33) vs Korean bot (North, 114,3) on kmulti1 at 5x speed.
-- Both bots must build a base, one of them a full base and an army, and one must destroy an enemy building.
BaseTypes = { "khq", "kbarracks", "kmill", "kbeaconmound", "kheavyarmsworkshop", "kbarracks2", "kplaneworks", "ktemple",
    "kshamanhouse", "kstable", "karrowtower", "kcannontower", "kshipyard", "jhq", "jbarracks", "jmill", "jbeacon",
    "jheavyarms", "jbarracks2", "jairport", "jtemple", "jwitchhouse", "jstable", "jarrowtower", "jcannontower", "jshipyard" }
ArmyTypes = { "kfootman", "karcher", "kgunner", "kfirecar", "kcannon", "kcommander", "kmonk", "kshaman", "kfighter",
    "jfootman", "jarcher", "jgunner", "jarmoredcar", "jcannon", "jgeneral", "jpriest", "jwitch", "jfighter", "jbomber" }

WorldLoaded = function()
    north = Player.GetPlayer("North")
    south = Player.GetPlayer("South")
    peak = { North = { base = 0, army = 0 }, South = { base = 0, army = 0 } }
    destroyed = 0
    Camera.Position = Map.NamedActor("SouthHQ").CenterPosition
    Watch()
    -- Each bot must get going; the start positions are not equal (the north one has a cliff between its HQ and the
    -- crops), so only one of them has to reach a full base and army.
    WaitFor("the Korean bot builds a base (3+ buildings)", 15000, function() return peak.North.base >= 3 end)
    WaitFor("the Japanese bot builds a base (3+ buildings)", 15000, function() return peak.South.base >= 3 end)
    WaitFor("a bot builds a full base (4+ buildings) and an army (3+ units)", 15000, function()
        return (peak.North.base >= 4 and peak.North.army >= 3) or (peak.South.base >= 4 and peak.South.army >= 3)
    end)
    WaitFor("a bot destroys an enemy building", 30000, function() return destroyed > 0 end)
    FinishWhenDone("BOT", 30500)
end

watched = { }
Watch = function()
    for _, p in ipairs({ north, south }) do
        local base = p.GetActorsByTypes(BaseTypes)
        local army = p.GetActorsByTypes(ArmyTypes)
        local name = p.InternalName
        if #base > peak[name].base then peak[name].base = #base end
        if #army > peak[name].army then peak[name].army = #army end
        for _, b in ipairs(base) do
            local key = tostring(b)
            if not watched[key] then
                watched[key] = true
                Trigger.OnKilled(b, function(self, killer)
                    if killer and killer.Owner ~= self.Owner and (killer.Owner == north or killer.Owner == south) then
                        destroyed = destroyed + 1
                        print("bot: " .. killer.Owner.InternalName .. " destroyed " .. self.Type .. " at tick " .. DateTime.GameTime)
                    end
                end)
            end
        end
        if DateTime.GameTime % 3000 == 0 then
            local types = { }
            for _, b in ipairs(base) do types[b.Type] = (types[b.Type] or 0) + 1 end
            local parts = { }
            for t, n in pairs(types) do parts[#parts + 1] = t .. " " .. n end
            table.sort(parts)
            print("bot: tick " .. DateTime.GameTime .. " " .. name .. " buildings " .. #base .. " (" .. table.concat(parts, ", ") .. "), army " .. #army .. ", cash " .. (p.Cash + p.Resources))
        end
    end
    if not finished then Trigger.AfterDelay(250, Watch) end
end
