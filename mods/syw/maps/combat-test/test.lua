-- Combat, both factions: every weapon family (sword, bow, gun, rockets, cannon, bombs), the towers and the
-- aircraft. Each attacker holds fire and is ordered to attack only its own target, an unarmed Peasant owned by
-- the neutral Creeps, so no neighbour can take the credit. Passes when every target has taken damage.
Attackers = {
    { "kfootman", "Multi1" }, { "karcher", "Multi1" }, { "kgunner", "Multi1" }, { "kcommander", "Multi1" },
    { "kfirecar", "Multi1" }, { "kcannon", "Multi1" }, { "kfighter", "Multi1" }, { "kmonk", "Multi1" },
    { "kshaman", "Multi1" }, { "karrowtower", "Multi1" }, { "kcannontower", "Multi1" },
    { "jfootman", "Multi0" }, { "jarcher", "Multi0" }, { "jgunner", "Multi0" }, { "jgeneral", "Multi0" },
    { "jarmoredcar", "Multi0" }, { "jcannon", "Multi0" }, { "jfighter", "Multi0" }, { "jbomber", "Multi0" },
    { "jpriest", "Multi0" }, { "jwitch", "Multi0" }, { "jarrowtower", "Multi0" }, { "jcannontower", "Multi0" }
}

-- Slots on land, at least 12 cells apart (the longest range is 11): west of the lake and along the south.
Slots = function()
    local slots = { }
    for _, y in ipairs({ 4, 18 }) do
        for _, x in ipairs({ 4, 18 }) do slots[#slots + 1] = CPos.New(x, y) end
    end
    for _, y in ipairs({ 36, 49, 62 }) do
        for _, x in ipairs({ 4, 16, 28, 40, 52 }) do slots[#slots + 1] = CPos.New(x, y - 2) end
    end
    return slots
end

WorldLoaded = function()
    creeps = Player.GetPlayer("Creeps")
    local slots = Slots()
    local batch = { }
    for i, a in ipairs(Attackers) do
        batch[#batch + 1] = { type = a[1], owner = Player.GetPlayer(a[2]), slot = slots[((i - 1) % #slots) + 1], second = i > #slots }
    end
    -- More attackers than slots: the rest run in a second wave after the first finishes.
    RunWave(batch, false)
    Later(1300, function() RunWave(batch, true) end)
    DamageSmoke()
    FinishWhenDone("COMBAT", 3000)
end

-- Vehicles, ships and aircraft smoke below 50% health, not above.
DamageSmoke = function()
    local ko = Player.GetPlayer("Multi1")
    local units = {
        Actor.Create("kfirecar", true, { Owner = ko, Location = CPos.New(60, 44) }),
        Actor.Create("kpatrolship", true, { Owner = ko, Location = CPos.New(50, 20) }),
        Actor.Create("jfighter", true, { Owner = Player.GetPlayer("Multi0"), Location = CPos.New(60, 40) })
    }
    Later(3, function()
        for _, u in ipairs(units) do
            u.Stance = "HoldFire"
            u.Health = u.MaxHealth * 8 / 10
        end
    end)
    Later(10, function()
        for _, u in ipairs(units) do
            Check(not SywTest.Overlay(u, "damage-smoke"), u.Type .. " doesn't smoke at 80% health")
            u.Health = u.MaxHealth * 4 / 10
        end
    end)
    Later(20, function()
        for _, u in ipairs(units) do
            Check(SywTest.Overlay(u, "damage-smoke"), u.Type .. " smokes below 50% health")
        end
    end)
end

RunWave = function(batch, second)
    for _, b in ipairs(batch) do
        if b.second == second then
            local attacker = Actor.Create(b.type, true, { Owner = b.owner, Location = b.slot })
            local target = Actor.Create("kpeasant", true, { Owner = creeps, Location = CPos.New(b.slot.X + 4, b.slot.Y + 1) })
            Later(3, function()
                if attacker.HasProperty("Stance") then attacker.Stance = "HoldFire" end
                attacker.Attack(target, true, true)
            end)
            WaitFor(b.type .. " damages its target", 1200, function()
                return target.IsDead or target.Health < target.MaxHealth
            end, function()
                if not attacker.IsDead then attacker.Destroy() end
                if not target.IsDead then target.Destroy() end
            end)
        end
    end
end
