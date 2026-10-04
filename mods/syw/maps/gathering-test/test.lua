-- Gathering automation. The local player (Multi0) is Japan; crop fields lie at x 4-8, y 10-14 and y 21-24.
-- 1. The HQ's rally point on a field: a trained Peasant walks there and starts harvesting.
-- 2. The rally point on plain ground: the Peasant just waits there.
-- 3. A Peasant that finishes a Mill starts harvesting the nearby crops.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    me.Cash = 20000
    hq = Actor.Create("jhq", true, { Owner = me, Location = CPos.New(4, 4) })

    Later(5, RallyOnField)
    Later(5, BuildMill)
    FinishWhenDone("GATHERING", 3000)
end

RallyOnField = function()
    hq.RallyPoint = CPos.New(6, 12)
    hq.Build({ "jpeasant" }, Expect("HQ trains a Peasant with the rally point on a field", function(units)
        local worker = units[1]
        local start = me.Cash
        WaitFor("Peasant trained to a field starts harvesting", 600, function() return SywTest.Harvesting(worker) end, function()
            WaitFor("Peasant trained to a field delivers crops", 1500, function() return me.Cash > start end, RallyOnGround)
        end)
    end))
end

RallyOnGround = function()
    local rally = CPos.New(16, 6)
    hq.RallyPoint = rally
    hq.Build({ "jpeasant" }, Expect("HQ trains a Peasant with the rally point on plain ground", function(units)
        local worker = units[1]
        WaitFor("Peasant trained to plain ground reaches the rally point", 600, function()
            return worker.IsIdle and math.abs(worker.Location.X - rally.X) <= 2 and math.abs(worker.Location.Y - rally.Y) <= 2
        end, function()
            Later(50, function()
                Check(not SywTest.Harvesting(worker), "Peasant trained to plain ground does not start harvesting")
            end)
        end)
    end))
end

BuildMill = function()
    local peasant = Actor.Create("jpeasant", true, { Owner = me, Location = CPos.New(14, 20) })
    Later(5, function()
        SywTest.Construct(peasant, "jmill", CPos.New(10, 22))
        Later(5, function()
            local mill = me.GetActorsByType("jmill")[1]
            Check(mill ~= nil, "Peasant places a Mill beside the crops")
            if mill == nil then return end
            WaitFor("Peasant finishes the Mill", 2500, function() return SywTest.Finished(mill) end, function()
                WaitFor("Peasant that built the Mill starts harvesting", 100, function()
                    return peasant.IsInWorld and SywTest.Harvesting(peasant)
                end)
            end)
        end)
    end)
end
