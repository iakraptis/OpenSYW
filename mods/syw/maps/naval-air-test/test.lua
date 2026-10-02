-- Naval and air, both factions. The local player (Multi0) is Japan; Multi1 is Korea. The lake is roughly x 34-60,
-- y 7-33, with the western shore at x 34.
-- 1. Shipyards and aircraft factories of both factions train.
-- 2. The Japanese Submarine and Torpedo are visible to the enemy (no stealth).
-- 3. Japanese Torpedoes only attack-move: they ram a Korean ship, or explode at their destination.
-- 4. An aircraft transport loads and unloads, and the cargo panel draws its passengers (real UI).
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    ko = Player.GetPlayer("Multi1")
    me.Cash = 30000
    ko.Cash = 30000

    -- 1. Production.
    local kyard = Actor.Create("shipyard", true, { Owner = ko, Location = CPos.New(34, 14) })
    local jyard = Actor.Create("jshipyard", true, { Owner = me, Location = CPos.New(34, 23) })
    local kair = Actor.Create("planeworks", true, { Owner = ko, Location = CPos.New(4, 40) })
    local jair = Actor.Create("airport", true, { Owner = me, Location = CPos.New(12, 40) })
    Later(5, function()
        for _, p in ipairs({ { kyard, "patrolship" }, { jyard, "torpedo" }, { kair, "fighter" }, { jair, "bomber" } }) do
            local owner = p[1].Owner
            p[1].Build({ p[2] })
            WaitFor(p[1].Type .. " trains " .. p[2], 1500, function() return #owner.GetActorsByType(p[2]) > 0 end)
        end
    end)

    -- 2. The Submarine and the Torpedo are not hidden: the enemy sees them.
    local sub = Actor.Create("submarine", true, { Owner = me, Location = CPos.New(56, 10) })
    local visibleTorpedo = Actor.Create("torpedo", true, { Owner = me, Location = CPos.New(56, 14) })
    Later(80, function()
        Check(SywTest.Visible(sub, ko), "the Submarine is visible to the enemy")
        Check(SywTest.Visible(visibleTorpedo, ko), "the Torpedo is visible to the enemy")
    end)

    -- 3. Torpedoes: attack-move only. One attack-moves past an enemy ship and rams it; one attack-moves to open
    -- water and explodes when it arrives.
    local torpedo = Actor.Create("torpedo", true, { Owner = me, Location = CPos.New(40, 30) })
    local prey = Actor.Create("cannonship", true, { Owner = ko, Location = CPos.New(44, 31) })
    local runner = Actor.Create("torpedo", true, { Owner = me, Location = CPos.New(40, 20) })
    Later(3, function() prey.Stance = "HoldFire" end)
    Later(60, function()
        Check(not torpedo.IsDead, "an idle Torpedo waits instead of hunting (enemy ship 4 cells away)")
        SywTest.OrderAt(torpedo, "AttackMove", CPos.New(48, 30))
        SywTest.OrderAt(runner, "AttackMove", CPos.New(46, 20))
        WaitFor("an attack-moving Torpedo rams an enemy ship and explodes", 600, function()
            return torpedo.IsDead and (prey.IsDead or prey.Health < prey.MaxHealth)
        end)
        WaitFor("a Torpedo explodes when it reaches its attack-move destination", 600, function()
            return runner.IsDead
        end)
    end)

    -- 4. Aircraft transport and the cargo panel.
    local transport = Actor.Create("jtransporter", true, { Owner = me, Location = CPos.New(10, 24) })
    local rider1 = Actor.Create("jpeasant", true, { Owner = me, Location = CPos.New(12, 26) })
    local rider2 = Actor.Create("jfootman", true, { Owner = me, Location = CPos.New(13, 26) })
    Later(5, function()
        transport.Move(CPos.New(12, 26))
        transport.CallFunc(function()
            rider1.EnterTransport(transport)
            rider2.EnterTransport(transport)
        end)
        WaitFor("two units board the Transporter", 800, function() return transport.PassengerCount == 2 end, function(ok)
            if not ok then return end
            SywTest.Select(transport)
            Later(10, function()
                Check(SywTest.CargoPortraits() == 2, "the cargo panel draws both passengers (" .. SywTest.CargoPortraits() .. ")")
                transport.UnloadPassengers(CPos.New(20, 30))
                WaitFor("the Transporter unloads", 800, function() return transport.PassengerCount == 0 end)
            end)
        end)
    end)

    FinishWhenDone("NAVAL AIR", 3000)
end
