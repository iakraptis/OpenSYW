-- Capture and conquest victory. Conquest is on in this map, so every counted actor is preplaced (map.yaml).
-- 1. The Korean Occupier and the Japanese Thief capture enemy buildings and are used up.
-- 2. Once the enemy has nothing left, it loses and the local player wins.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Multi1")
    enemyLost = false
    iWon = false
    Trigger.OnPlayerLost(enemy, function() enemyLost = true end)
    -- Winning ends the game (the game-over screen stops the world), so report straight from the callback.
    Trigger.OnPlayerWon(me, function()
        Check(enemyLost, "the enemy lost before the local player won")
        Check(true, "the local player wins")
        Finish()
    end)

    Later(5, function()
        MyOccupier.Capture(EnemyBarracks)
        MyThief.Capture(EnemyMill)
        captured = 0
        local both = function(ok)
            captured = captured + 1
            if captured == 2 then Conquer() end
        end
        WaitFor("the Occupier captures the enemy Barracks", 600, function()
            return EnemyBarracks.Owner == me and MyOccupier.IsDead
        end, both)
        WaitFor("the Thief captures the enemy Mill", 600, function()
            return EnemyMill.Owner == me and MyThief.IsDead
        end, both)
    end)

    FinishWhenDone("CAPTURE VICTORY", 2000)
end

-- With both buildings captured, remove everything the enemy has left.
Conquer = function()
    for _, a in ipairs(enemy.GetActors()) do
        if a.HasProperty("Kill") and not a.IsDead then a.Kill() end
    end
    WaitFor("the enemy loses when nothing is left", 200, function() return enemyLost end)
    Later(300, function() if not finished then Check(false, "the local player wins") end end)
end
