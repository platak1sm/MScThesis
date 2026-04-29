# Hybrid Surface-Air Interaction: Exploiting Passive Haptics and Gaze-Driven Context for Precise 3D Manipulation in VR

**Author:** Emmanouil Platakis  
**Supervisor:** Ken Pfeuffer  
**Context:** Master Thesis (30 ECTS)  

--- ### [Demo](https://youtu.be/bcVJdNiHlLo)

This repository contains the implementation of a Master Thesis project exploring **Hybrid Surface-Air Interaction**. The project aims to bridge the gap between 2D surface precision and 3D spatial freedom using the Meta Quest Pro.


### ❓ The Problem
Six-degree-of-freedom (6DoF) interaction is the standard for object manipulation in VR. However, it suffers from significant limitations:
* **Lack of physical support:** Leading to hand tremors (jitter) and reduced precision during fine tasks.
* **"Gorilla Arm" effect:** Causing user fatigue over time when reaching for distant objects.

While 2D input devices (like a stylus on a tablet) provide physical stability (passive haptics), mapping a 2D surface input to a 3D environment introduces **dimensional ambiguity**. Specifically, while the lateral (X) axis is consistent, it is unclear whether depth movement on a physical table should map to the vertical (Y) or depth (Z) axis in the virtual world.

### 💡 The Solution
This project leverages the **Meta Quest Pro Touch Controllers' pressure-sensitive stylus tips** combined with **Eye Tracking** to enable a seamless transition between 2D surface writing and 3D mid-air interaction. 

**Main Research Question:**
> How can we exploit the precision control of a 2D pen on a physical surface to facilitate the 3D manipulation of objects in Virtual Reality?

---

## 🛠 Interaction Design

The system implements a "Clutching" mechanism where manipulation only occurs while the stylus tip is actively pressed against a physical surface or the controller trigger is engaged in the air. 

The project focuses on two primary interaction modalities:

### A. Direct Manipulation (Proximity-Based)
* **Surface (2D):** The user manipulates the object by directly pressing its virtual "shadow proxy" projected onto the table surface. This constrains movement to the XZ plane (floor), utilizing the table for absolute physical stability.
* **Air (3D):** By lifting the pen and pressing the input button while physically intersecting the object in mid-air, the user engages standard 1:1 6DoF manipulation for coarse placement.

### B. Indirect Manipulation (The "Giant Trackpad" Paradigm)
To interact with objects beyond arm's reach (up to a 2x2m virtual workspace), the user's physical desk acts as a generic relative trackpad. A **Visual Angle Gain** algorithm scales small physical stylus movements into larger distant movements, eliminating the need to physically reach.

This modality introduces several advanced contextual sub-systems:

* **Robust Selection (Sticky Gaze):** Uses a time-buffered SphereCast to acquire distant targets. If natural eye micro-jitter causes the gaze to briefly slip, the object remains "acquired" for a short grace period (0.4s), ensuring stable pre-selection.
* **Gaze & Posture-Dependent Axis Mapping:** Solves the 2D-to-3D depth ambiguity by dynamically routing axes based on initial head orientation and contextual gaze shifts. This aligns the input mapping with the user's natural spatial perception—forward motion feels like "up" when looking at a wall, but feels like "away" when looking at the floor. Lateral (left/right) pen movement always maps to the X-axis, while forward/backward pen movement is routed as follows:
    * 👁️ **Gaze on Object (Head Straight):** Pushing the pen forward/backward maps naturally to the **Y-axis** (Vertical translation/Scrolling).
        * 👇 *Modifier (Glance at Pen):* Looking down at the physical hand toggles the routing to the **Z-axis** (Depth).
    * 👁️ **Gaze on Object (Head Diagonally Down):** Pushing the pen forward/backward maps naturally to the **Z-axis** (Depth translation).
        * 👇 *Modifier (Glance at Pen):* Looking down at the physical hand toggles the routing to the **Y-axis** (Vertical).
* **Wrist Rotation:** Physically twisting the stylus on the table extracts a delta-twist applied exclusively to the virtual object's Y-axis rotation.
* **Spatial Awareness (Smart Pen Minimap):** When the system detects the user shifting into Z-Axis (Depth) mode by looking down at the pen, a virtual smartwatch-style screen automatically activates on the controller. This provides a live, top-down 2D orthographic minimap of the target object to assist with depth placement.
* **Intent Communication:** A strict visual feedback system communicates system state via object/shadow outlines: Hovering (Green), Indirect Trackpad Manipulation (Blue), and Direct Physical Manipulation (Yellow).

---

## 💻 Tech Stack & Hardware

* **Engine:** Unity 6
* **Hardware:** Meta Quest Pro
* **Input Handling:**
    * Meta Quest Pro Stylus Mode (Inverted Grip)
    * Pressure sensitivity thresholds (Stylus tip)
    * Eye Tracking / Gaze origin & direction vectors
    * Headset posture tracking (Pitch/Orientation)
