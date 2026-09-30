# TRANSFER / 01 – Physics Chamber
 
Coding Assignment #1. Unity 6000.3.24f1, URP, Input System.
 
## Play
 
Open the project in Unity 6000.3.24f1, open `Assets/Scenes/PhysicsChamber.unity` and hit Play. The scene looks empty in Edit Mode; that's normal, since `ChamberBuilder.cs` builds everything when Play starts.
 
- **WASD** to move, **R** to restart.
- Step on the coral pad (northwest) to remove the gate.
- Push the gold crate through the S-shaped maze into the green zone in the east corridor. You can't pull it, so leave room to get behind it on turns.
## Implementation
 
- **Movement:** input is read in `Update`, forces are applied in `FixedUpdate`. Nothing gets teleported.
- **Roadblock:** `ChamberSensor` only reacts to the Actor. It disables the gate, recolors the sensor and logs once.
- **Deposit:** `DepositZone` only counts the crate, fully inside the zone, entering after unlock. Logs elapsed time on success.
- **Restart:** resets positions, velocity, gate, sensor and timer.

| Setting | Actor | Payload |
| --- | ---: | ---: |
| Mass | 2 | 3.5 |
| Linear damping | 1.4 | 0.65 |
| Friction | 0.05 | 0.18 |
 
Floor friction is 0.45. Both bodies have frozen rotation.
 
## Reflection
 
**1. What counts as success? Can you cheat?**
The Actor has to hit the sensor first so the gate is actually gone. Then the crate has to enter the green zone and sit completely inside it. Walking the Actor onto the zone doesn't count, and neither does just poking the front of the crate in. If you park the crate on the zone before unlocking, it won't count until you push it out and back in. There's no jump and the walls are solid, so you can't skip the maze.
 
**2. Unexpected behavior**
The first time I ran it, both the Actor and the crate fell straight through the floor. The spawn positions were fine, so I checked at runtime and found the Default layer wasn't colliding with itself. After switching Unity versions, the collision matrix in the project settings wasn't loading correctly. I fixed the setting so Default collides with Default again, and added a test that checks both bodies stay on the floor after spawning and restarting.
 
**3. Design tradeoff**
I went for predictable pushing. The crate is heavier than the Actor but low friction, so a limited force can still move it. I froze its rotation so it can't tumble or wedge sideways in a doorway. It's less realistic, but way easier to control.
 
**4. Weakest part**
You can shove the crate flat against a wall and get stuck, since the Actor can't pull. Restart fixes it but you lose your progress and time. I'd have a few people play it and widen whichever corners trap the crate most.
 
## Testing
 
`Editor/ChamberVerification.cs` runs 48 Play Mode checks in batch mode, all passing as of 2026-09-30. It covers spawning, WASD, the gate, deposit rules, timing, restart, and pushing the crate through the route with simulated input. It opens the scene and quits Unity, so run it on a copy of the project:
 
```text
Unity.exe -batchmode -projectPath "<copy>" -executeMethod PhysicsChamber.Editor.ChamberVerification.RunBatch -logFile "<log>"
```
 
Don't add `-quit`, since it exits on its own. If you had the project open before the collision fix, restart Unity.
