# SCENE_SETUP.md — Hub Scene, Prefabs, Build Settings, Classes

> Load this file when: rebuilding the Hub scene, adding class prefabs, fixing build
> settings, Setup menu tools, scene order issues, prefab registration.

**Last verified:** 2026-06-28

---

## Scene Order (Build Settings)

| Index | Scene | Purpose |
|---|---|---|
| 0 | LoginScene | Username/password → POST /login → get JWT |
| 1 | CharacterSelect | POST /character → confirm class → connect to game |
| 2 | Hub | Main multiplayer hub — spawn, chat, portal, crafting |
| — | Arena/Portal | In progress — portal transition not yet complete |

**Rule:** Never reorder these. Unity loads scenes by index. If LoginScene isn't index 0, the startup flow breaks.

---

## Class Prefabs

| class_index | Class Name | Prefab name (match exactly) |
|---|---|---|
| 0 | Engineer | EngineerPlayer (or matching name) |
| 1 | Guardian | GuardianPlayer |
| 2 | Shadowblade | ShadowbladePlayer |
| 3 | Cleric | ClericPlayer |
| 4 | Arcanist | ArcanistPlayer |

**Rules:**
- Prefab array index in NetworkManager's `Registered Spawnable Prefabs` must match `class_index`
- Each prefab needs: `NetworkIdentity`, `NetworkTransform`, `PlayerController` (or equivalent), class-specific ability component
- All 5 prefabs must be in the Spawnable Prefabs list even if not all classes are used yet

---

## Hub Scene Structure

```
Hub (Scene)
├── NetworkManager          — Mirror NetworkManager + KcpTransport
├── EventSystem             — ONE only. Delete any duplicates.
├── MainCamera              — With CameraController script
├── HUDCanvas               — Screen-space overlay
│   ├── ChatPanel           — Chat input + message log
│   ├── OnlinePlayersPanel  — VerticalLayoutGroup list (see _scripts/OnlinePlayersHUD.cs)
│   ├── GoldDisplay         — Gold counter text
│   └── (future) XPBar, HotBar, MiniMap
├── Environment
│   ├── HubGeometry         — Ground, walls, decorative meshes
│   ├── PortalRoom          — Portal placeholder (transition in progress)
│   └── (future) ForgeNPC, VendorNPC
└── SpawnPoints             — Player spawn positions (tag: "SpawnPoint")
```

---

## NetworkManager Setup

On the NetworkManager GameObject:

| Setting | Value |
|---|---|
| Network Address | 15.204.243.36 |
| Max Connections | 100 |
| Transport | KcpTransport on same GameObject |
| KCP Port | 7777 |
| Player Prefab | Leave empty — server spawns by class index |
| Spawnable Prefabs | All 5 class prefabs + any networked world objects |

---

## Portal → Arena (In Progress)

The portal room exists in the Hub scene as a placeholder. The scene transition is not yet implemented.

**What needs to be built:**
1. Portal trigger volume (OnTriggerEnter → call NetworkManager.ServerChangeScene or additive load)
2. Arena scene (basic circular geometry, spawn points, enemy spawn zones)
3. Return to hub trigger in Arena
4. Scene unload / cleanup on return

---

## Setup Menu Tools

The Setup menu (Editor only, menu bar) contains helper tools for:
- Registering class prefabs into NetworkManager automatically
- Verifying scene build index order
- (Add any custom editor tools here as they're created)

These tools are editor-only and have no effect on builds.

---

## Build Settings Checklist

Before any build:
- [ ] All 5 class prefabs in NetworkManager Spawnable Prefabs
- [ ] Scene order: Login(0), CharSelect(1), Hub(2)
- [ ] Build target: Linux (server) or Windows (client)
- [ ] IL2CPP scripting backend (for server builds)
- [ ] Server build: enable "Server Build" checkbox in Build Settings
- [ ] Client build: disable "Server Build", ensure `#if !UNITY_SERVER` guards are in place on client-only components (GmConsole, etc.)

---

## Active Issues
- Portal → Arena scene transition not yet implemented (Week 2 gap)
- `GmConsole.cs` lacks `#if !UNITY_SERVER` guard — crashes server build every frame
- Arena scene geometry not yet built
