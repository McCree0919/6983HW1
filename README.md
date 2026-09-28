# TRANSFER / 01 - Physics Chamber

Coding Assignment #1. Unity 6000.3.24f1, Universal Render Pipeline, Input System.

Restored on 2026-09-30 from the original implementation in this conversation.
All chamber scripts compile against the installed Unity 6000.3.24f1 assemblies.
The earlier verification record below describes the original 6000.6.3f1 attempt;
a new full Play Mode verification has not been performed during restoration.

## Play

Open `Assets/Scenes/PhysicsChamber.unity` and press Play. The chamber is generated
by `ChamberBuilder.Awake`; its editable dimensions and physics settings are in
`Assets/PhysicsChamber/ChamberBuilder.cs`. The original SampleScene is retained.
The new chamber is the first scene in Build Settings.

- WASD: move the cyan Actor along the floor; W points toward the top of the maze.
- R or Restart: restore the crate, actor, gate, sensor and timer.
- Enter the coral sensor pad in the northwest alcove to remove the coral gate.
- Push the gold, banded Payload through the north opening in the first divider,
  south through the middle corridor, then through the south opening in the
  second divider. Push it north into the green DepositZone in the east corridor.
- The initial alcove wall requires taking the crate around its east end.
  Leave space between the crate and walls so you can move behind it for turns.

## Implementation

| Requirement | Implementation |
| --- | --- |
| WASD Actor | `ActorController.Update` samples keyboard input and normalizes diagonals. |
| Correct physics update | `FixedUpdate` applies a bounded force toward a target planar velocity; normal gameplay never moves the Actor or Payload transform directly. |
| Maze | Physical outer walls, alternating dividers and a sensor alcove create an S-shaped route. |
| Sensor roadblock | `ChamberSensor` accepts only the registered Actor Rigidbody; `ChamberRun.Unlock` deactivates the gate, including its collider, and logs once. |
| Physics Payload | Dynamic Rigidbody, moved through contact with the Actor. Decorative crate bands have disabled colliders. |
| Guarded deposit | `DepositZone` requires the registered Payload, its entire XZ collider footprint inside the zone, and an unlocked run. The actor and early payload entries do not win. |
| Elapsed time | Initial timing starts at scene Play (`Time.timeSinceLevelLoad`); restarts reset the baseline. Success logs seconds and freezes the displayed timer. Editor pause/timeScale also pauses this game-time timer. |
| Distinct physics | Actor: mass 2, linear damping 1.4, friction 0.05. Payload: mass 3.5, damping 0.65, friction 0.18. Floor: friction 0.45. |

Movement uses force 38 and target speed 4.5. Both bodies have frozen rotation,
continuous dynamic collision detection and interpolation. Gravity remains active.
Friction uses the Minimum combine mode, keeping the crate movable with the chosen
force. The floor supports both bodies; walls are higher than the crate and there
is no jump action. A resting crate already inside the zone can count after unlock,
because `OnTriggerStay` rechecks the state and full footprint; it can never count
while locked. Repeated overlaps cannot emit multiple success logs.

## Reflection

These answers describe this implementation. The verification section distinguishes
automated checks from manual playtesting; revise the subjective observations after
your own playthrough before submitting the assignment.

### 1. What must be true for success, and can the player cheat?

Physically, the registered gold Payload must overlap the DepositZone with its
entire collider footprint inside the green pad. In script state, `Unlocked` must
be true and `Complete` must still be false before `TryDeposit` accepts it.
The cyan Actor opens the gate by entering the sensor, and the disabled gate no
longer blocks Rigidbody contacts through that passage. A player could try to
touch the finish with the Actor or send the Payload there before opening the
gate, but the body-identity and unlock checks reject both attempts. The walls,
frozen body rotation and absence of jumping also prevent the intended player
controls from taking a shortcut over a divider.

### 2. What unexpected behavior occurred, and what changed?

During implementation review, I noticed that a basic trigger-enter solution
would count a crate as delivered when only its leading corner touched the pad.
That behavior comes from overlap callbacks reporting any collider intersection,
not full containment. `DepositZone` therefore checks the minimum and maximum X
and Z of the crate bounds against the pad bounds. It also checks in
`OnTriggerStay`, because a crate can move from partial to full containment without
generating another enter event. This is a code-review finding, not a claim that
the original bug was observed during manual playtesting.

### 3. What design tradeoff did you make?

I optimized for readable, controllable pushing by making the crate heavier than
the Actor while giving its contact material relatively low friction.
The Actor uses a force-limited velocity correction, so input still produces
physical acceleration and contact forces instead of teleporting either body.
Frozen rotation prevents the crate from tumbling and wedging itself diagonally
across a passage. The tradeoff is less realistic box motion and fewer emergent
solutions, but a more predictable puzzle for a first physics assignment.

### 4. What is the weakest part, and what would you change first?

The weakest part is recovery when the player pushes the crate tightly against
a wall and cannot get behind it. Restart always restores a playable state,
but it also discards progress and resets the timer. I would first manually
playtest the turning spaces with several players and widen any corner that
regularly traps the crate. This would improve fairness while preserving the
assignment's requirement that transport happens through physical pushing.

## Verification

Status: all chamber runtime and editor scripts compiled successfully against the
installed Unity 6000.6.3f1 assemblies and Input System assembly using Unity's C#
compiler. The isolated Unity batch run was blocked during license initialization
(`Connection to channel LicenseClient-17645 refused`), before the Play Mode checks
could run. No runtime-test passes or rendered screenshots are claimed.

`Assets/PhysicsChamber/Editor/ChamberVerification.cs` provides an isolated batch
Play Mode check through `PhysicsChamber.Editor.ChamberVerification.RunBatch`.
It checks locked-state rejection, actual trigger overlap before unlock,
sensor body identity, physical gate removal, partial/full deposit, one-shot
timing, restart momentum and actual Actor-to-Payload contact pushing.
It also captures desktop and portrait camera renders and checks for nonblank
pixels. This runner changes the open scene and exits Unity, so it is restricted
to batch mode in a separate project copy.

Manual acceptance: play the complete route with WASD, verify the Console contains
one roadblock-removal message and one timed deposit message, then restart and
repeat. Subjective control feel and a full keyboard-driven playthrough require
manual verification.
