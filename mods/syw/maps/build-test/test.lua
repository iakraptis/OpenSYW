-- Construction and production, both factions: a Peasant builds a Barracks through the real build order,
-- the new Barracks trains a Footman, then repair and sell work on it.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    ko = Player.GetPlayer("Multi1")
    me.Cash = 20000
    ko.Cash = 20000
    BuildAndTrain(me, "jpeasant", "jbarracks", "jfootman", CPos.New(8, 6), CPos.New(4, 4))
    BuildAndTrain(ko, "peasant", "barracks", "footman", CPos.New(8, 40), CPos.New(4, 38))
    FinishWhenDone("BUILD", 4000)
end

BuildAndTrain = function(player, peasantType, barracksType, footmanType, site, start)
    local peasant = Actor.Create(peasantType, true, { Owner = player, Location = start })
    Later(5, function()
        local before = player.Cash
        SywTest.Construct(peasant, barracksType, site)
        Later(3, function()
            Check(player.Cash == before - Actor.Cost(barracksType), barracksType .. " costs " .. Actor.Cost(barracksType) .. " (paid " .. (before - player.Cash) .. ")")
        end)
        local barracks
        WaitFor(peasantType .. " finishes a " .. barracksType, 2000, function()
            local list = player.GetActorsByType(barracksType)
            barracks = list[1]
            return barracks ~= nil and SywTest.Finished(barracks)
        end, function(ok)
            if not ok then return end
            WaitFor(peasantType .. " comes back out after building", 100, function() return peasant.IsInWorld end)
            Train(player, barracks, footmanType)
        end)
    end)
end

Train = function(player, barracks, footmanType)
    -- The finished building needs a moment before its queue offers units.
    Later(10, function()
        local accepted = barracks.Build({ footmanType }, Expect(barracks.Type .. " trains " .. footmanType, function(units)
            Check(units[1].Type == footmanType, barracks.Type .. " trains " .. footmanType .. " (got " .. units[1].Type .. ")")
            RepairAndSell(player, barracks)
        end))
        Check(accepted, barracks.Type .. " accepts a " .. footmanType .. " order")
    end)
end

RepairAndSell = function(player, barracks)
    barracks.Health = barracks.MaxHealth / 2
    local damaged = barracks.Health
    local before = player.Cash
    barracks.StartBuildingRepairs()
    WaitFor(barracks.Type .. " repairs and charges for it", 300, function()
        return barracks.Health > damaged and player.Cash < before
    end, function()
        local cash = player.Cash
        barracks.Sell()
        WaitFor(barracks.Type .. " sells for a refund", 300, function() return barracks.IsDead and player.Cash > cash end)
    end)
end
