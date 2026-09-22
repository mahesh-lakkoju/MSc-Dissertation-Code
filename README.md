# Gaze Model for a Virtual Human

A Unity project in which a 3D virtual human moves its head and eyes to follow a
sequence of moving targets, and shows natural **idle gaze behaviour** (blinking,
small eye and head movements, looking around, looking away, eye contact) when
there is nothing to follow.

## Requirements

- **Unity 6.6 (`6000.6.0f1`)**. Other versions may not open the project cleanly.
- Universal Render Pipeline (already configured in the project).
- Windows, macOS or Linux with a GPU that supports Unity 6.

## How to run

1. Unzip the project.
2. In Unity Hub choose **Add > Add project from disk** and select the unzipped folder.
   The first open takes a few minutes while Unity rebuilds its cache.
3. If Unity opens an empty "Untitled" scene, open **Assets/Scenes/SampleScene**
   (the only scene in the project).
4. Press **Play**.

## What you should see

Watch the **Console** window for the log messages below.

1. **Start idle (3 s).** The character idles: blinking, small head sway, eye
   micro-movements and occasional glances. Console: `Idle`.
2. **Target tracking.** The head and eyes follow six moving targets one at a
   time, in name order (Football, Football_1, Target_Capsule_1, Target_Capsule_2,
   Target_Cube_1, Target_Cube_2). Each target moves out and back over about
   10 seconds and then pauses 1 second before the next one. Console:
   `Now looking at: <name>`.
3. **Idle again.** After the last target the gaze returns to a neutral pose and
   the idle behaviours resume. Console: `ALL 6 TARGETS COMPLETED!`, then `Idle`.

Blinking continues throughout, including while tracking.

## Idle behaviour

Implemented in `IdleGaze.cs`. The character repeatedly picks a behaviour by
weighted random choice and holds it for a random time:

| Behaviour | Description |
|---|---|
| Neutral | Returns to the rest pose and holds it |
| Look around | Gaze moves to a random point (about +-30 degrees yaw, +-12 degrees pitch) |
| Look away | A slight sideways and downward glance (up to about 20 degrees) |
| Eye contact | Looks at `eyeContactTarget` (defaults to the Main Camera), limited to 60 degrees from the rest pose |

Layered on top of every behaviour:

- **Head sway:** slow Perlin-noise drift of the head.
- **Micro-saccades:** small random eye jumps every 0.4 to 1.6 s.
- **Blinking:** every 2 to 6 s, with occasional double blinks and a blink on
  about half of gaze shifts. The eyes are closed by scaling their height
  (the model has no blink blendshape).
- **Head/eye split:** the head takes half of each gaze shift and the eyes cover
  the rest, and both eyes converge on a single point.

## Settings

Select the character's head object (the one with the `GazeController`
component) and use the Inspector.

| Section | Setting | Meaning |
|---|---|---|
| Tracking | `headSpeed`, `eyeSpeed`, `pauseTime` | Turn speeds and the pause after each target |
| Idle | `startIdleDuration` | Seconds of idle before the first target (0 = start tracking immediately) |
| Idle | `loopSequence`, `idleDuration` | Restart the target sequence after idling this long |
| Idle > Behaviour Mix | `*Weight` | Relative chance of each idle behaviour |
| Idle > Hold Time | `*Duration` | Min and max seconds for each behaviour |
| Idle > Gaze Range | `*Yaw`, `*Pitch`, `headShare` | How far the character looks, and how much the head does |
| Idle > Blinking | `blinkInterval`, `blinkDuration`, `doubleBlinkChance`, `eyeClosedScale` | Blink timing and depth |

## Project structure

```
Assets/
  Scenes/SampleScene.unity     The demo scene (character, six targets, camera)
  Scripts/
    GazeController.cs          Runs the target sequence and switches between tracking and idle
    IdleGaze.cs                Idle behaviours and blinking
    TargetMovement.cs          Moves a target out and back along one axis, then reports it has finished
  Models/                      Character model and the six target prefabs
  Materials/                   Materials
Packages/, ProjectSettings/    Unity configuration
```

## How it works

- `GazeController` finds all objects tagged `GazeTarget`, sorts them by name so
  runs are repeatable, and enables each target's `TargetMovement` in turn.
- While tracking, the head and both eyes are rotated toward the target each
  frame in `LateUpdate`, using frame-rate-independent smoothing.
- Idle directions are computed from the head's rest pose using world-space
  rotations, so they do not depend on how the bones' local axes are set up.
  The code assumes each bone's +Z axis points forward.

## Known limitations

- The eyelash mesh does not move when the eyes blink.
- The eye and head bones are assumed to face along their local +Z axis.
- Eye contact needs a target; with no Main Camera and no `eyeContactTarget`
  assigned, that behaviour is skipped.

## Credits and declarations

### Third-party assets

- **Character model (`Assets/Models/character.fbx`, duplicated at `Assets/Materials/character.fbx`):**
  an Adobe Mixamo character (mesh name prefix `Ch38`, rig `mixamorig`).
  The origin is recorded inside the FBX file, which references
  `Adobe_Mixamo_2019/.../Ch38_nonPBR.fbx`. Mixamo (https://www.mixamo.com) is
  operated by Adobe. Its assets are provided under Adobe's Mixamo terms, which, as I
  understand them, allow use in personal, non-profit and commercial projects
  but do not allow redistributing the assets on their own. The model is included here only as
  part of this project, for assessment. Check Adobe's current Mixamo terms of
  use on the Mixamo website before publishing this project anywhere public.
- **Unity packages:** Universal Render Pipeline, Input System and other
  standard Unity packages, used under the Unity licence.
