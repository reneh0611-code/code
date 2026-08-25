# CHEAT ON YOUR DAY ONES — Phase 1 Foundation

## Target
Unity 6.3 LTS, 3D project, C#, Netcode for GameObjects.

This pack contains the Phase 1 gameplay foundation:
- Network player
- Server-authoritative wallet
- Aura
- Needs
- Inventory data model
- Universal interaction interface
- Basic server-validated interaction
- Development host/client launcher
- Basic HUD binding
- Simple third-person movement/camera foundation

## Install packages in Unity

Open:
Window > Package Manager

Install:
1. Netcode for GameObjects
2. Unity Transport
3. Input System
4. TextMeshPro (normally already present)
5. Multiplayer Play Mode (recommended for testing)

Then:
Edit > Project Settings > Player > Active Input Handling
Set to "Input System Package (New)" or "Both".

Restart Unity if prompted.

## Recommended project folders

Assets/
  Art/
  Audio/
  Materials/
  Prefabs/
    Player/
    World/
    UI/
  Scenes/
  ScriptableObjects/
    Items/
    Vehicles/
    Businesses/
    Jobs/
  Scripts/
    Camera/
    Core/
    Player/
    Interaction/
    Inventory/
    Items/
    Jobs/
    Economy/
    Businesses/
    NPC/
    Police/
    Vehicles/
    SocialMedia/
    Casino/
    UI/
    Multiplayer/
    World/
    SaveSystem/

## Scene setup

Create scene:
Assets/Scenes/Prototype_Street.unity

### 1. NetworkRoot

Create empty GameObject:
NetworkRoot

Add:
- NetworkManager
- UnityTransport
- DevNetworkLauncher

NetworkManager:
- Player Prefab = Player.prefab (created below)

### 2. Player prefab

Create Capsule:
Player

Add:
- NetworkObject
- CharacterController
- PlayerAgent
- PlayerData
- PlayerWallet
- AuraSystem
- NeedsSystem
- PlayerInventory
- NetworkPlayerController
- PlayerInteractor

CharacterController recommended:
Center: (0, 1, 0)
Radius: 0.4
Height: 2.0

Important:
DO NOT add NetworkTransform to this Phase 1 prefab.
NetworkPlayerController currently synchronizes a server-authoritative transform itself.

Add child:
CameraRoot
Position ~ (0, 1.6, 0)

Add child under CameraRoot:
PlayerCamera

Components:
- Camera
- AudioListener
- ThirdPersonCamera

For ThirdPersonCamera:
Target = Player root

For NetworkPlayerController:
Camera Target = CameraRoot
Player Camera = PlayerCamera
Audio Listener = PlayerCamera's AudioListener

For PlayerInteractor:
Player Camera = PlayerCamera

Drag Player into:
Assets/Prefabs/Player/Player.prefab

Delete scene instance.

Assign Player.prefab to NetworkManager > Player Prefab.

### 3. Ground

Create Plane:
Ground

Scale large enough for testing.
Add Collider if necessary.

### 4. Networked test job

Create Cube:
TestJob

Add:
- NetworkObject
- TestCashInteractable
- BoxCollider

Add TestJob to NetworkManager's Network Prefabs list if your NGO version requires explicit registration for scene/network objects.

Interaction:
walk close, look at cube, press E.
Expected:
Cash +$100
Aura +5

### 5. HUD

Create Canvas:
HUD

Add:
- Cash TMP Text
- Bank TMP Text
- Aura TMP Text
- Interaction TMP Text
- Health Slider
- Hunger Slider
- Energy Slider

Slider:
Min Value = 0
Max Value = 1

Add PlayerHUD to Canvas and wire references.

Suggested layout:
Top-left:
Cash
Bank
Aura

Bottom-left:
Health
Hunger
Energy

Bottom-center:
Interaction prompt

## Multiplayer test

Fastest:
1. Install Multiplayer Play Mode.
2. Configure one virtual player.
3. Press Play.
4. Main editor: Start Host.
5. Virtual player: Start Client.

Alternative:
Make a standalone development build.
Start Host in editor and Client in build.

## Security model

Critical state is server-write-only:
- Cash
- Bank
- Aura
- Health
- Hunger
- Energy
- Inventory

The client sends requests.
The server validates and applies them.

Example:
Client presses E.
Client sends target NetworkObjectId.
Server checks:
- target exists
- distance is valid
- object implements IInteractable
- CanInteract() is true

Only then does server execute the interaction.

## Known Phase 1 limitation

Movement is deliberately simple and server-authoritative. It favors correctness over final game feel.
Before a public build, replace it with a proper predicted/reconciled movement solution or tune NGO authority/prediction.

This is intentional: economy/ownership/business rules should be secure before polishing movement.

## Next milestones

Phase 2:
- Item database/catalog
- FoodData
- Shop terminal
- Server-authoritative purchase transaction
- Supermarket stock
- Consume food
- Hunger/energy feedback
- money/aura popup event UI

Phase 3:
- vehicle system
- fuel
- taxi job state machine
- NPC passenger
- fare calculation
- job ratings

Phase 4:
- business ownership
- stock/revenue/expenses
- kiosk
- NPC purchasing simulation

Phase 5:
- CityGram
- posts
- followers
- business reputation
- first sabotage mechanic
- cooldowns/evidence/counterplay
