# Drone Traceability & Telemetry Platform (MR/Unity)

A professional-grade real-time monitoring dashboard for autonomous drone fleets, built in Unity. This system is designed for resilience, precision, and multi-drone operational awareness.

---

## 🚀 Key Capabilities (What This System Does Well)

### 1. Robust Data Pipeline
The system implements a strict "Chain of Command" for data to prevent runtime errors:
- **Raw Layer**: Ingests messy, unpredictable JSON from WebSockets.
- **Sanitization Layer**: Converts raw data into type-safe C# models (`DroneTelemetryData`) before it touches any logic.
- **Result**: Zero null-reference exceptions from malformed packets, ensuring the dashboard stays alive even if the server sends garbage.

### 2. Smart Geospatial Engine
Unlike simple 2D image maps, this project uses a true GIS (Geographic Information System) coordinate integration:
- **Real-World Math**: `GeoMapContext` handles Lat/Lon-to-Meter conversions using spherical Earth projection.
- **Dynamic Interaction**: Supports full **Multi-Touch (Pinch-to-Zoom)** and **Pan** controls. You can scroll 50km away from the base, and drone markers remain mathematically accurate relative to the center.
- **Visual Clarity**: Auto-hides markers that fly outside the current view to prevent visual clutter.

### 3. Resilient Networking
The connection layer (`DroneNetworkClient`) is built for instability:
- **Auto-Reconnect**: Automatically retries connections with exponential backoff (1s → 2s → 4s...) if the server goes down.
- **Event Probing**: Peeks at JSON headers before parsing to handle unknown event types gracefully without crashing.
- **Thread Safety**: Uses a `MainThreadDispatcher` to ensuring background network events never crash the Unity UI thread.

### 4. Deterministic State Management ("The Slot System")
The platform solves the "Who owns what?" problem with an elegant **Active Slot Architecture**:
- **Slot 0 (Primary)**: Displays on the main dashboard (Cyan theme).
- **Slot 1 (Secondary)**: Reserved for comparison or secondary view (Orange theme).
- **Deterministic Assignment**: The `SelectionManager` acts as the single source of truth. Drones are assigned to specific slots, and the UI reacts instantly to these state changes.

---

## 🏗️ Architecture Overview

The codebase is structured to separate concerns, preventing "Spaghetti Code":

### Core Systems
| Component | Function |
|-----------|----------|
| **`SelectionManager`** | The "Brain". Manages which drone is in which slot. |
| **`GeoMapContext`** | The "GIS Engine". Handles all GPS math and map state. |
| **`MainThreadDispatcher`** | The "Bridge". Safely passes network data to the UI. |

### Visualization Layer
| Component | Function |
|-----------|----------|
| **`FleetUIManager`** | Manages the list of drones and routes data to the correct dashboard. |
| **`MapPanelController`** | Controls the map UI, handling zoom/pan inputs and marker spawning. |
| **`DroneVisualizer`** | Controls 3D drone models (propeller spin, tilt, hover) based on live physics. |

---

## 📂 Project Structure (`Assets/Scripts`)

- **`Core/`**: Singleton managers (State, Panels, Selection).
- **`Network/`**: WebSocket client and REST API parsers.
- **`UI/`**: 2D Dashboards, Map logic, and HUD elements.
- **`Map/`**: Pure math logic for Geospatial coordinate systems.
- **`Drone/`**: 3D object behaviors and visualizers.
- **`Models/`**: Strong-typed C# data structures (JSON contracts).

---

## 🛠️ Setup & Configuration

### 1. API Configuration
Find the `FleetUIManager` in your scene.
- **API Base URL**: Set to your local or remote server (e.g., `http://localhost:5101`).

### 2. Map Calibration
The map system is self-calibrating. Only one step is needed:
- In `MapPanelController`, set the **Top Left GPS** and **Bottom Right GPS** coordinates of your map background image.
- The system automatically calculates scale (`Pixels Per Meter`) on startup.

---

## 📝 Developer Notes

### Adding New Telemetry Fields
1. **Model**: Add the field to `WS_TelemetryDetails` (Raw) and `DroneTelemetryData` (Clean).
2. **Mapping**: Update `DroneNetworkClient` to copy raw value to clean model.
3. **UI**: Add a text field to `DroneTelemetryController` and update `UpdateVisuals()`.

### Handling New Network Events
1. Create a `WS_NewEvent` class in `DroneNetworkModels`.
2. Add a `case` in `DroneNetworkClient.ProcessMessageSafe` to deserialize it.
3. Fire a Unity Event to notify the UI.
