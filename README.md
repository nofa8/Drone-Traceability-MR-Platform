# 🛸 Drone Traceability & Telemetry Platform (MR/Unity)

A professional-grade, real-time Ground Control Station (GCS) for autonomous drone fleets. This system utilizes a **Data Fusion Architecture** to ensure resilience, precision, and historical analysis capabilities.

---

## 🚀 Key Capabilities

### 1. Data Fusion Architecture (The "Single Source of Truth")

The system does not rely on simple pass-through networking. Instead, it implements a **Repository Pattern**:

* **Dual Ingestion**: Merges high-frequency **WebSocket Telemetry** (Live) with robust **REST Snapshots** (Historical/Offline).
* **Conflict Resolution**: Smart logic ensures that stale snapshots never overwrite fresh live telemetry.
* **Result**: The UI never flickers or shows "No Data" when a drone disconnects. It simply transitions to a "Stale" or "Offline" state, preserving the last known location.

### 2. Time-Travel Replay System ⏪

A built-in "Black Box" flight recorder:

* **Automatic Recording**: Every telemetry packet is cached in a circular buffer history.
* **Visual Scrubbing**: A slider allows operators to replay past flights instantly.
* **Ghost Markers**: Displays a "Ghost Drone" on the map to compare historical position vs. live position.
* **UI Hijacking**: The Replay Controller temporarily "hijacks" the Detail View to show historical battery/speed data without corrupting the live network stream.

### 3. "Slippy" Map Engine (GIS) 🗺️

The map system has evolved beyond static images into a dynamic **Tile Rendering Engine**:

* **Lazy Loading**: Only downloads map tiles (OSM/Satellite) for the area currently visible on screen.
* **Performance Optimized**: Tiles outside the viewport are unloaded to save memory.
* **Dynamic Panning**: Features "Logarithmic Damping"—panning is fast when zoomed in (Street View) but heavy/precise when zoomed out (Country View).

### 4. Deterministic State Management

The platform solves the "Who owns what?" problem with an **Active Slot Architecture**:

* **Slot 0 (Primary)**: The focused drone (Cyan theme).
* **Slot 1 (Secondary)**: Comparison view (Orange theme).
* **Visual Feedback**: Map markers and UI cards automatically change color/border based on which slot controls them.

---

## 🏗️ Architecture Overview

The codebase follows a strict **Unidirectional Data Flow**:

`Network/Disk` → `Repository` → `Events` → `UI Controllers`

### Core Systems

| Component | Function |
| --- | --- |
| **`DroneStateRepository`** | **The Heart.** Stores the definitive state of every drone (Live + History). Fuses data sources. |
| **`DroneNetworkClient`** | **The Ear.** Listens to WebSockets and feeds the Repository. |
| **`GeoMapContext`** | **The Brain.** Handles GPS-to-Screen math and global map scaling. |

### Visualization Layer

| Component | Function |
| --- | --- |
| **`TrailReplayController`** | Manages the timeline slider, ghost markers, and UI overrides. |
| **`MapTileRenderer`** | Handles the downloading, caching, and positioning of map background tiles. |
| **`DroneTelemetryController`** | The "Detail View". Can display **Live Data** OR **Replay Data** based on mode. |

---

## 📂 Project Structure (`Assets/Scripts`)

* **`Core/`**: `DroneStateRepository`, `MainThreadDispatcher`.
* **`Network/`**: WebSocket client (`NativeWebSocket`) and REST parsers.
* **`Map/`**:
* `Tiles/`: Tile providers and lazy-loading renderers.


* **`UI/`**: `DroneCardUI`, `FleetUIManager`, Slot logic, Buttons and Map Related UI.
* **`Drone/`**: 3D visualizers (Propeller spin, Tilt) and Physics.
* **`Models/`**: shared C# data contracts (`DroneState`, `DroneTelemetryData`).

---

## 🛠️ Setup & Configuration

### 1. API Connection

* Attach `DroneNetworkClient` to a persistent GameObject.
* Configure the **WS URL** (e.g., `ws://localhost:5101/telemetry`) and **HTTP URL** for snapshots.

### 2. Map Configuration

* The `MapPanelController` handles zoom/pan sensitivity.
* The `MapTileRenderer` handles cache size and tile servers.

---

## 📝 Safety & UX Features

### 🚦 Stale Data Protection

The system refuses to issue dangerous commands to "Ghost" drones.

* **Rule**: If no heartbeat is received for >5 seconds, the drone is marked **STALE**.
* **Effect**: "Arm/Takeoff" buttons become non-interactable. The Map Marker fades to 40% opacity.

### 🔌 Self-Healing UI

* If the Replay Controller loses its link to the Detail View, it **auto-detects** it on the next user interaction.
* If the Network drops, the Repository retains the last known state, preventing UI errors.

---

## ⚖️ Development Decisions (Why we did this)

1. **Repository over Direct Events**: We moved to a Repository so that late-joining components (like the Map) can ask "Where is everyone?" immediately on startup, rather than waiting for the next network packet.
2. **Replay Override vs. State Change**: We chose to "Hijack" the UI for replays rather than overwriting the actual drone data. This ensures that **Live Alerts** (e.g., "Battery Critical") can still trigger in the background even while the user is watching a replay.
3. **Tile System**: Static maps don't scale. The tile system allows the fleet to move anywhere in the world without the developer needing to manually update background images.