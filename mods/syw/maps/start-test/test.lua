-- Start and economy, both factions. The local player (Multi0) is Japan; Multi1 is Korea.
-- 1. The training panel shows each HQ's workers (real UI, local player's buildings).
-- 2. Each HQ trains its Peasant at the rules cost; the Peasant gathers and delivers.
-- 3. The Bull needs the Heavy Arms building, then trains.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    ko = Player.GetPlayer("Multi1")
    me.Cash = 20000
    ko.Cash = 20000
    jhq = Actor.Create("jhq", true, { Owner = me, Location = CPos.New(4, 4) })
    khq = Actor.Create("khq", true, { Owner = ko, Location = CPos.New(4, 40) })
    -- A Korean HQ owned by the local player, to check the Korean panel through the same UI.
    myKorean = Actor.Create("khq", true, { Owner = me, Location = CPos.New(20, 4) })

    Later(5, function() SywTest.Select(jhq) end)
    Later(15, function()
        local panel = Split(SywTest.TrainingPanel(), "|")
        Check(panel[1] == "Workers.Japan", "Japanese HQ panel shows its queue (" .. SywTest.TrainingPanel() .. ")")
        Check(Contains(Split(panel[2] or "", ","), "jpeasant") and tonumber(panel[3]) > 0, "Japanese HQ panel draws the Japanese Peasant button")
        SywTest.Select(myKorean)
    end)
    Later(25, function()
        local panel = Split(SywTest.TrainingPanel(), "|")
        Check(Contains(Split(panel[2] or "", ","), "kpeasant") and tonumber(panel[3]) > 0, "Korean HQ panel draws the Korean Peasant button (" .. SywTest.TrainingPanel() .. ")")
        TrainAndGather(me, jhq, "jpeasant")
        TrainAndGather(ko, khq, "kpeasant")
    end)
    FinishWhenDone("START", 4000)
end

TrainAndGather = function(player, hq, peasant)
    local before = player.Cash
    hq.Build({ peasant }, Expect(hq.Type .. " trains " .. peasant, function(units)
        local worker = units[1]
        Check(worker.Type == peasant, hq.Type .. " trains " .. peasant .. " (got " .. worker.Type .. ")")
        Check(player.Cash == before - Actor.Cost(peasant), peasant .. " costs " .. Actor.Cost(peasant) .. " (paid " .. (before - player.Cash) .. ")")
        local start = player.Cash
        worker.FindResources()
        WaitFor(peasant .. " gathers and delivers crops", 2500, function() return player.Cash > start end, function()
            TrainBull(player, hq)
        end)
    end))
end

TrainBull = function(player, hq)
    local bull = hq.Type == "jhq" and "jbull" or "kbull"
    local workshop = hq.Type == "jhq" and "jheavyarms" or "kheavyarmsworkshop"
    hq.Build({ bull })
    Later(10, function()
        Check(not hq.IsProducing(bull), bull .. " can't be trained without " .. workshop)
        local x = hq.Type == "jhq" and 12 or 12
        local y = hq.Type == "jhq" and 12 or 48
        Actor.Create(workshop, true, { Owner = player, Location = CPos.New(x, y) })
        Later(5, function()
            hq.Build({ bull }, Expect(hq.Type .. " trains " .. bull, function(units)
                Check(units[1].Type == bull, hq.Type .. " trains " .. bull .. " once " .. workshop .. " exists")
            end))
        end)
    end)
end
