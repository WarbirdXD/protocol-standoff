# Protocol Standoff - Scripts Overview

**Last Updated:** January 17, 2026

## 📁 Project Structure

```
scripts/
├── README.md (this file)
├── Documentation/
│   ├── INDEX.md (documentation index)
│   ├── SETUP_GUIDE.md
│   ├── FIREBASE_SETUP.md
│   ├── ACCOUNT_SYSTEM_SETUP.md
│   ├── MATCHMAKING_SETUP.md
│   ├── LOBBY_SETUP_GUIDE.md
│   ├── RANKING_INTEGRATION.md
│   ├── SECURITY_SETUP_GUIDE.md
│   ├── FIREBASE_SECURITY_LOGS_GUIDE.md
│   ├── MOVEMENT_SYSTEM.md
│   ├── CONTROLS_GUIDE.md
│   ├── UI_SYSTEM.md
│   ├── NETCODE_SETUP_GUIDE.md
│   └── CLEANUP_GUIDE.md
├── Core/
│   ├── AccountManager.cs
│   ├── FirebaseManager.cs
│   ├── MatchManager.cs
│   ├── MatchmakingManager.cs
│   ├── RankingSystem.cs
│   ├── SecurityManager.cs
│   ├── DynamicSpawnSystem.cs
│   ├── OnlinePlayersTracker.cs
│   └── UnityMainThreadDispatcher.cs
├── Networking/
│   ├── EOSNetcodeTransport.cs
│   ├── SimpleEOSManager.cs
│   ├── NetworkGameManager.cs
│   ├── NetworkPlayer.cs
│   ├── NetworkAnimationSync.cs
│   └── MatchmakingNetworkBridge.cs
├── Player/
│   ├── FPSController.cs
│   ├── PlayerHealth.cs
│   ├── WeaponController.cs
│   └── CalibrationMode.cs
├── UI/
│   ├── MainMenu.cs
│   ├── AccountUI.cs
│   ├── MatchmakingUI.cs
│   ├── LobbyUI.cs
│   ├── MatchHUD.cs
│   ├── Scoreboard.cs
│   ├── MatchCountdownUI.cs
│   ├── AmmoDisplay.cs
│   ├── DamageOverlay.cs
│   ├── DashChargeUI.cs
│   ├── DynamicCrosshair.cs
│   ├── HitMarker.cs
│   └── CalibrationUI.cs
├── Weapons/
│   ├── BulletTracer.cs
│   ├── ShellEjection.cs
│   ├── WeaponSway.cs
│   └── CameraShake.cs
├── Targets/
│   ├── PracticeTarget.cs
│   └── DummyPlayer.cs
├── Audio/
│   └── PlayerAudioManager.cs
└── Editor/
    └── InputManagerSetup.cs
```

---

## 🎮 Core Systems

### Firebase Backend (FirebaseManager.cs)
- **Authentication** - Email/password login and registration
- **Realtime Database** - Player profiles, stats, matchmaking
- **Session Enforcement** - Prevents multiple simultaneous logins
- **Presence System** - Online/offline status tracking
- **OnDisconnect** - Automatic cleanup on disconnect
- **User-friendly error messages** - Parsed Firebase errors

### EOS Networking (SimpleEOSManager.cs, EOSNetcodeTransport.cs)
- **Anonymous Authentication** - Device ID-based login (no Epic account needed)
- **P2P Networking** - Peer-to-peer connections via EOS
- **Custom Transport Layer** - Bridges EOS P2P with Unity Netcode
- **NAT Traversal** - Automatic hole punching and relay fallback
- **No Port Forwarding** - Works out-of-the-box for all players

### Matchmaking System (MatchmakingManager.cs)
- **Skill-Based Matching** - MMR-based matchmaking
- **Expanding Search Range** - Starts at ±50 MMR, expands every 10s
- **Queue System** - Firebase-coordinated matchmaking
- **Rate Limiting** - Max 10 attempts per minute (security)
- **Multiple Game Modes** - Separate queues and MMR per mode

### Ranking System (RankingSystem.cs)
- **7 Rank Tiers** - Chrome, Bronze, Silver, Gold, Platinum, Diamond, Radiant
- **MMR System** - Win: +25 MMR, Loss: -20 MMR (adjusted by opponent)
- **Statistics Tracking** - Wins, losses, win rate, highest MMR
- **Mode-Specific Rankings** - Different MMR for each game mode

### Security System (SecurityManager.cs)
- **Rate Limiting** - Login (5/min), Register (3/min), Matchmaking (10/min)
- **Input Validation** - Username, email, password validation
- **Anti-Cheat** - Speed hack detection, damage validation, position checks
- **Server Validation** - Match result validation before MMR changes
- **Firebase Logging** - All security events logged and categorized

### Dynamic Spawn System (DynamicSpawnSystem.cs)
- **Procedural spawn generation** - No static spawn points
- **Smart scoring system** - Distance, line of sight, combat zones
- **Team spawning** - Spawns near teammate but spread out
- **Anti-pattern system** - Prevents repetitive spawns

### Player Movement (FPSController.cs)
- **BO6-style omni-movement** (slide, dive, prone)
- **Extreme recoil movement system** (rocket jumping, recoil surfing)
- **Stance system** (standing, crouch, prone)
- **Air control** and momentum preservation
- **Sprint + B** = Slide
- **Sprint + Hold B** = Dive

### Weapon System (WeaponController.cs)
- **Recoil patterns** with customizable values
- **Recoil movement integration** (affects player physics)
- **Headshot detection** (2x damage)
- **Reload system** with animations
- **Extreme recoil** enables advanced movement techniques

---

## 🎯 UI System (Modular Design)

### In-Game HUD
- **AmmoDisplay.cs** - Current/reserve ammo, reload status
- **DamageOverlay.cs** - Red vignette damage feedback (COD-style)
- **DynamicCrosshair.cs** - Expands with weapon spread
- **HitMarker.cs** - Hit confirmation, headshot indicators
- **MatchHUD.cs** - Timer, team scores, match end screen
- **Scoreboard.cs** - Full player stats (Press Tab)

### Menus
- **MainMenu.cs** - Main menu, settings, quit
- **LobbyUI.cs** - Team selection, chat, ready system
- **CalibrationUI.cs** - Mouse sensitivity calibration

---

## 🔫 Weapon Effects

- **BulletTracer.cs** - Visible bullet trails
- **ShellEjection.cs** - Ejected shell casings
- **WeaponSway.cs** - Weapon movement while walking
- **CameraShake.cs** - Screen shake on shooting

---

## 🎯 Practice Targets

### PracticeTarget.cs (Recommended)
- Auto-respawn
- Health display
- Statistics tracking (hits, headshots, damage)
- Visual feedback

### DummyPlayer.cs
- Simple player dummy for testing

---

## 📋 Key Features

### Extreme Recoil Movement
- **Rocket Jumping:** Shoot down while grounded to launch up
- **Recoil Surfing:** Use recoil in air to gain speed/height
- **Building Climbing:** Chain shots downward to climb walls
- **Slide Boosting:** Recoil affects slide direction/speed

### BO6 Controls
- **Sprint + Tap B:** Slide
- **Sprint + Hold B:** Dive
- **Hold B (not sprinting):** Prone
- **X:** Reload only
- **Tab:** Scoreboard

---

## 🚀 Quick Start

1. **Player Setup:**
   - Add `FPSController.cs` to player
   - Add `PlayerHealth.cs` to player
   - Add `WeaponController.cs` to weapon
   - Assign camera references

2. **UI Setup:**
   - Create Canvas
   - Add UI scripts to appropriate panels
   - Connect references in Inspector

3. **Match Setup:**
   - Add `MatchManager.cs` to scene
   - Set spawn points for both teams
   - Configure match duration

4. **Testing:**
   - Add `PracticeTarget.cs` to cubes/spheres
   - Enable auto-respawn for continuous testing
   - Check console for statistics

---

## 📖 Documentation

**See [Documentation/INDEX.md](Documentation/INDEX.md) for complete documentation index.**

### Quick Links
- **[Setup Guide](Documentation/SETUP_GUIDE.md)** - Complete project setup
- **[Firebase Setup](Documentation/FIREBASE_SETUP.md)** - Backend configuration
- **[Security Setup](Documentation/SECURITY_SETUP_GUIDE.md)** - Security features
- **[Controls Guide](Documentation/CONTROLS_GUIDE.md)** - All controls and inputs

### All Documentation
The `Documentation/` folder contains 14 comprehensive guides covering:
- Setup and configuration
- Game systems (matchmaking, ranking, accounts)
- Security and monitoring
- Player movement and controls
- UI system
- Networking setup

---

## 🔧 Editor Tools

### InputManagerSetup.cs
Automatically sets up Unity Input Manager with required axes.

---

## ⚙️ Settings

### Player Preferences (Saved)
- Mouse sensitivity
- Master volume
- Graphics quality
- Resolution
- Fullscreen mode

---

## 🎨 Color Palette

```
Dark Background:    #0A0E1A
Medium Background:  #1A1F2E
Light Background:   #2A3142
Team 1 (Blue):      #00A8FF
Team 2 (Orange):    #FF6B00
Success:            #00FF88
Warning:            #FFD700
Danger:             #FF3333
```

---

## 📝 Notes

- **Modular UI:** Each UI element is a separate script for easy management
- **Event-driven:** Uses UnityEvents for loose coupling
- **Performance:** Cached references, object pooling where needed
- **Extensible:** Easy to add new features without breaking existing code

---

## 🐛 Common Issues

**Player floating:**
- Check gravity is negative (-20)
- Ensure CharacterController is grounded

**Recoil movement not working:**
- Verify WeaponController calls `fpsController.ApplyRecoilMovement()`
- Check recoil influence values (2.5 for air, 1.5 for slide)

**UI not showing:**
- Check Canvas render mode (Screen Space - Overlay)
- Verify all references are assigned in Inspector

---

**For detailed information, see the Documentation folder.**
