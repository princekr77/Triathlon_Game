# Triathlon 3D (Triathlon Game)

A complete, multi-stage 3D triathlon sports simulation game developed in **Unity 6 (6000.3.21f1)** using the **Universal Render Pipeline (URP)**. Players compete across three distinct sporting disciplines—**Running**, **Swimming**, and **Cycling**—managing cross-stage stamina, rhythm timing, cadence pacing, and obstacle navigation to beat benchmark AI times and set high scores.

---


## Setup & Installation

### System & Engine Requirements
- **Unity Version:** `Unity 6000.3.21f1` (Unity 6 LTS)
- **Render Pipeline:** Universal Render Pipeline (URP 17.3.0)
- **Input System:** Unity Input System (`com.unity.inputsystem 1.20.0`) with Legacy Input fallback
- **UI Framework:** TextMeshPro (`com.unity.ugui 2.0.0`)
- **Scripting Backend:** Mono / IL2CPP (.NET Standard 2.1)

### How to Open and Run the Project in Unity Editor
1. **Clone or Download** the repository to your local machine.
2. Open **Unity Hub** and click **Add** > **Add project from disk**.
3. Select the root folder `Triathlon_Game`.
4. Ensure the editor version is set to **6000.3.21f1** (Unity 6).
5. Once loaded, open the Scenes folder located at `Assets/Scenes/`.
6. Double-click **`UIScene.unity`** to load the initial entry point.
7. Click the **Play** button in the Unity Editor toolbar.
8. Click **Start Race** on the Main Menu to begin the full triathlon sequence.


---


## Controls Reference

The game features comprehensive input handling for both **Desktop (PC / Standalone)** and **Mobile (Android Touch)**:

### 1. Stage 1: Running (`Run_Scene`)
| Action | PC Controls (Keyboard) | Android / Touch Controls |
| :--- | :--- | :--- |
| **Steer Left** | `A` or `Left Arrow` | Drag / Swipe Left on **Left Half** of screen ($X < 50\%$) |
| **Steer Right** | `D` or `Right Arrow` | Drag / Swipe Right on **Left Half** of screen ($X < 50\%$) |
| **Jump (Clear Obstacles)** | `Space` or `Up Arrow` | Tap anywhere on **Right Half** of screen ($X \ge 50\%$) |
| **Auto-Run Acceleration** | Automatic forward pacing ($10 \rightarrow 16\text{ m/s}$) | Automatic forward pacing ($10 \rightarrow 16\text{ m/s}$) |

### 2. Stage 2: Swimming (`Swim_Scene`)
| Action | PC Controls (Keyboard) | Android / Touch Controls |
| :--- | :--- | :--- |
| **Stroke (Rhythm Impulse)** | `Space` / `W` / `Up Arrow` (Hit in Green) | Tap On-Screen **`STROKE`** Button or Tap Right Screen Zone |
| **Steer Left / Right** | `A` / `D` or `Left` / `Right Arrow` | Drag horizontally on **Left Screen Zone** |
| **Enter Water from Dock** | Press `E` when prompted | Tap screen interaction prompt |
| **Exit Water to Dock** | Press `E` at canal exit | Tap screen interaction prompt |
| **Glide (Oxygen Regen)** | Release stroke inputs to coast | Release stroke inputs to coast |

### 3. Stage 3: Cycling (`Cycle_Scene`)
| Action | PC Controls (Keyboard) | Android / Touch Controls |
| :--- | :--- | :--- |
| **Steer Left / Right** | `A` / `D` or `Left` / `Right Arrow` | Swipe horizontally across upper screen |
| **Left Pedal** | Alternating rhythm key | Tap On-Screen **`[ LEFT ] PEDAL`** Button |
| **Right Pedal** | Alternating rhythm key | Tap On-Screen **`[ RIGHT ] PEDAL`** Button |
| **Rhythm Hit (Speed Burst)** | `Spacebar` (Time with green marker) | Tap On-Screen **`[ SPACE / HIT ]`** Button |

### 4. UI & Menu Navigation
- **PC:** Mouse Left-Click on buttons (Start, Restart, Menu).
- **Android:** Direct finger tap on UI elements.

---


## Device & Platform Specifications

| Specification | Target Configuration |
| :--- | :--- |
| **Primary Platforms** | Windows PC (Standalone x86_64) & Android Mobile |
| **Minimum Android Version** | Android 7.1 "Nougat" (API Level 25) |
| **Target Android Version** | Android 14 / 15 (API Level 34+) |
| **Screen Orientation** | **Landscape** (`defaultScreenOrientation: 4` / AutoRotation Landscape Left/Right) |
| **Tested Aspect Ratios** | 16:9 (Desktop 1920×1080), 19.5:9 (Modern Mobile 2340×1080), 20:9 (2400×1080) |
| **Target Framerate** | 60 FPS (V-Sync enabled on desktop; Application.targetFrameRate = 60 on mobile) |
| **Input Support** | Direct Keyboard/Mouse, Gamepad (InputSystem_Actions), Multi-Touch Screen |

---

## Bug Fixes, Optimizations & Edge Cases Handled

During development and QA testing, the following technical issues were identified and resolved in code:

1. **Solid Road Stripe Collider Interferences (`RunSceneBootstrap.cs`):**
   - *Problem:* Modular track prefabs included solid box colliders on decorative white lane divider stripes, impeding smooth lateral steering.
   - *Solution:* Implemented automated cleanup in `RunSceneBootstrap.Awake()` that iterates through colliders on load and disables any collider belonging to objects named `stripe`.
2. **Coin Prefab Scale Normalization (`Coin.cs`):**
   - *Problem:* Certain imported coin prefabs had transform scales inflated up to 100x upon instantiation.
   - *Solution:* Added safety clamping in `Coin.Awake()` to normalize any scale exceeding $3.0$ back to `Vector3.one`.
3. **Cross-Lane Proximity Guard (`Coin.cs`):**
   - *Problem:* Over-sized trigger boundaries allowed players running in Lane 1 to collect coins placed in Lane 3.
   - *Solution:* Added strict 2D Euclidean distance validation requiring the player to be within $1.6\text{m}$ horizontal distance and $2.2\text{m}$ vertical distance to confirm pickup.
4. **Cadence Spamming / Single-Key Exploits (`CyclingCadenceController.cs`):**
   - *Problem:* Players could rapidly mash a single pedal button or key without respecting physical cadence.
   - *Solution:* Integrated strict pedal alternating state checks (`lastPedal`). Consecutive taps of the same pedal cut current cadence in half as an anti-cheese penalty.
5. **Terrain / Canal Boundary Escapes (`SwimmerController.cs`):**
   - *Problem:* High lateral swimmer speed allowed players to clip through canal walls into outer terrain.
   - *Solution:* Enforced clamped coordinate boundaries ($X \in [153.0, 169.5]$, $Z \in [248.0, 472.0]$) in `SwimmerController.Update()`.
6. **Unity 6 API Velocity Compatibility:**
   - *Problem:* Unity 6 deprecated `Rigidbody.velocity` in favor of `Rigidbody.linearVelocity`.
   - *Solution:* Used `#if UNITY_6000_0_OR_NEWER` preprocessor guards throughout cycle controllers to maintain compatibility without compiler warnings.
7. **Graceful Null-Safety on Audio Clips:**
   - *Problem:* Optional hit/pickup audio slots triggered console warnings when audio assets were unassigned.
   - *Solution:* Wrapped all `AudioSource.PlayClipAtPoint` invocations in explicit `if (clip != null)` safety guards.

---


## Summary

This project represents an end-to-end triathlon video game experience featuring distinct mechanics per sport (obstacle evasion, rhythm swimming, cadence cycling), unified under a single stamina and scoring architecture. All core assignment criteria, platform adaptations, and technical polish items have been implemented and verified.
