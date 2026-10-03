-- Spells, both factions. The local player (Multi0) owns the casters; Multi1 is the enemy.
-- Panel: each caster shows its spell buttons with icons. Then every spell is cast once.
Panels = { { "kmonk", 3 }, { "jpriest", 3 }, { "kshaman", 2 }, { "jwitch", 2 }, { "kminer", 2 } }

Unit = function(type, owner, x, y, holdFire)
    local a = Actor.Create(type, true, { Owner = owner, Location = CPos.New(x, y) })
    if holdFire then Later(1, function() if not a.IsDead then a.Stance = "HoldFire" end end) end
    return a
end

WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Multi1")
    me.Cash = 20000

    -- Spell panel for every caster (real UI).
    for i, p in ipairs(Panels) do
        local caster = Unit(p[1], me, 2 + i * 2, 62, true)
        Later(5 + i * 10, function() SywTest.Select(caster) end)
        Later(10 + i * 10, function()
            local buttons = SywTest.SpellButtons()
            local shown = Split(buttons, ",")
            Check(#shown == p[2] and not string.find(buttons, "noicon"), p[1] .. " panel shows " .. p[2] .. " spell buttons with icons (" .. buttons .. ")")
        end)
    end

    -- Heal (automatic) for both healers.
    for _, h in ipairs({ { "kmonk", "kfootman", 4 }, { "jpriest", "jfootman", 10 } }) do
        Unit(h[1], me, 4, h[3], false)
        local patient = Unit(h[2], me, 5, h[3], true)
        Later(2, function()
            patient.Health = patient.MaxHealth / 2
            local hurt = patient.Health
            WaitFor(h[1] .. " heals a wounded ally", 600, function() return patient.Health > hurt end)
        end)
    end

    -- Mass Heal (needs full mana, uses all of it).
    for _, h in ipairs({ { "kmonk", "kfootman", 16 }, { "jpriest", "jfootman", 22 } }) do
        local caster = Unit(h[1], me, 4, h[3], true)
        local a = Unit(h[2], me, 5, h[3], true)
        local b = Unit(h[2], me, 6, h[3] + 1, true)
        Later(3, function()
            a.Health = a.MaxHealth / 2
            b.Health = b.MaxHealth / 2
            local hurt = a.Health
            SywTest.Order(caster, "SywMassHeal")
            WaitFor(h[1] .. " Mass Heal heals every nearby ally and spends all mana", 100, function()
                return a.Health > hurt and b.Health > hurt and SywTest.Mana(caster) < 10
            end)
        end)
    end

    -- Disturb (Korean Monk): reveals a hidden enemy mine for a while.
    local monk = Unit("kmonk", me, 20, 4, true)
    local mine = Unit("syw-mine", enemy, 22, 4, false)
    Later(5, function()
        Check(not SywTest.Visible(mine, me), "enemy mine is hidden before Disturb")
        SywTest.Order(monk, "SywDisturb")
        WaitFor("Disturb reveals the enemy mine", 20, function() return SywTest.Visible(mine, me) end, function()
            WaitFor("the mine hides again when Disturb ends", 300, function() return not SywTest.Visible(mine, me) end)
        end)
    end)

    -- Disturb (Japanese Priest, same spell as the Monk): reveals a hidden enemy mine in range, not one beyond it.
    local priest = Unit("jpriest", me, 20, 10, true)
    local near = Unit("syw-mine", enemy, 22, 10, false)
    local far = Unit("syw-mine", enemy, 30, 10, false)
    Later(5, function()
        SywTest.Order(priest, "SywDisturb")
        WaitFor("the Priest's Disturb reveals a nearby enemy mine", 20, function() return SywTest.Visible(near, me) end, function()
            Check(not SywTest.Visible(far, me), "Disturb leaves mines out of range hidden")
        end)
    end)

    -- Lightning (Korean Shaman): kills an enemy infantry unit.
    local shaman = Unit("kshaman", me, 20, 16, true)
    local victim = Unit("kfootman", enemy, 24, 16, true)
    Later(5, function()
        SywTest.OrderOn(shaman, "SywCastLightning", victim)
        WaitFor("Lightning kills an enemy Footman", 200, function() return victim.IsDead end)
    end)

    -- Transform (Korean Shaman): one of your Peasants becomes another infantry unit.
    local shaman2 = Unit("kshaman", me, 20, 22, true)
    local peasant = Unit("kpeasant", me, 21, 22, true)
    Later(5, function()
        local spot = peasant.CenterPosition
        SywTest.OrderOn(shaman2, "SywTransform", peasant)
        WaitFor("Transform turns the Peasant into another unit", 200, function()
            if not peasant.IsDead then return false end
            for _, a in ipairs(Map.ActorsInCircle(spot, WDist.FromCells(1))) do
                if a.Owner == me and a ~= shaman2 and a.Type ~= "kpeasant" then return true end
            end
            return false
        end)
    end)

    -- Earthquake (Japanese Witch): damages an enemy at a ground target.
    local witch = Unit("jwitch", me, 4, 28, true)
    local quakeTarget = Unit("kfootman", enemy, 8, 28, true)
    Later(5, function()
        SywTest.OrderAt(witch, "SywEarthquake", CPos.New(8, 28))
        WaitFor("Earthquake damages an enemy at the target", 200, function()
            return quakeTarget.IsDead or quakeTarget.Health < quakeTarget.MaxHealth
        end)
    end)

    -- Bewilderment (Japanese Witch): an enemy unit changes sides.
    local witch2 = Unit("jwitch", me, 4, 34, true)
    local convert = Unit("kfootman", enemy, 7, 34, true)
    Later(5, function()
        SywTest.OrderOn(witch2, "SywBewilder", convert)
        WaitFor("Bewilderment takes over an enemy unit", 200, function() return convert.Owner == me end)
    end)

    -- Miner: a single mine at its feet, then a 3-mine line.
    local miner = Unit("kminer", me, 20, 28, true)
    local miner2 = Unit("kminer", me, 20, 40, true)
    Later(5, function()
        local cash = me.Cash
        local mana = SywTest.Mana(miner)
        SywTest.Order(miner, "SywLayMine")
        WaitFor("Miner lays a mine and pays mana and credits", 100, function()
            return #me.GetActorsByTypes({ "syw-mine", "syw-mine-building" }) >= 1 and me.Cash < cash and SywTest.Mana(miner) < mana
        end, function()
            local count = #me.GetActorsByTypes({ "syw-mine", "syw-mine-building" })
            SywTest.OrderAt(miner2, "SywLayMineLine", CPos.New(24, 40))
            WaitFor("Miner lays a 3-mine line", 400, function()
                return #me.GetActorsByTypes({ "syw-mine", "syw-mine-building" }) >= count + 3
            end)
        end)
    end)

    FinishWhenDone("SPELL", 2000)
end
