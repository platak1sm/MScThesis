# Hybrid Surface-Air Interaction: Exploiting Passive Haptics and Gaze-Driven Context for Precise 3D Manipulation in VR

**Author:** Emmanouil Platakis
**Supervisor:** Ken Pfeuffer
**Context:** Master Thesis (30 ECTS)

---

This repository contains the implementation of a Master Thesis project exploring **Hybrid Surface-Air Interaction**. The project aims to bridge the gap between 2D surface precision and 3D spatial freedom using the Meta Quest Pro.

### ❓ The Problem
Six-degree-of-freedom (6DoF) interaction is the standard for object manipulation in VR. However, it suffers from significant limitations:
* **Lack of physical support:** Leading to hand tremors (jitter) and reduced precision.
* **"Gorilla Arm" effect:** Causing user fatigue over time.

While 2D input devices (like a stylus on a tablet) provide physical stability (passive haptics), mapping a 2D surface input to a 3D environment introduces **dimensional ambiguity**. Specifically, while the lateral (X) axis is consistent, it is unclear whether depth movement on a table should map to the vertical (Y) or depth (Z) axis in the virtual world.

### 💡 The Solution
This project leverages the **Meta Quest Pro Touch Controllers' pressure-sensitive stylus tips** to enable a seamless transition between 2D surface writing and 3D mid-air interaction.

**Main Research Question:**
> How can we exploit the precision control of a 2D pen on a physical surface to facilitate the 3D manipulation of objects in Virtual Reality?

---

## 🛠 Interaction Design

The system implements a "Clutching" mechanism where manipulation only occurs while the stylus tip is on a surface or the controller trigger is actively engaged in the air.

The project focuses on two primary interaction modalities:

### A. Direct Manipulation (Hybrid)
* **Surface (2D):** The user manipulates the object by touching its virtual "shadow" projected onto the table surface. This constrains movement to the XZ plane (floor), utilizing the table for stability.
* **Air (3D):** By lifting the pen and pressing the input button while intersecting the object, the user engages standard 6DoF manipulation for coarse, mid-air placement.

### B. Indirect Manipulation (Gaze-Assisted)
This modality introduces a **Gaze-Dependent Axis Mapping** algorithm to solve the 2D-to-3D mapping ambiguity implicitly.

* **Lateral Motion:** Pen movement Left/Right always maps to the object's **X-axis**.
* **Depth Motion (Forward/Backward):** The mapping changes based on where the user is looking:
    * 👁️ **Gaze on Object:** Maps to the **Y-axis** (Vertical translation).
    * 👁️ **Gaze on Shadow/Floor:** Maps to the **Z-axis** (Depth translation).
* **Gaze Override:** Looking at the physical controller overrides the mapping to the **Z-axis**, allowing depth adjustment without looking away from the hand.

---

## 💻 Tech Stack & Hardware

* **Engine:** Unity 6
* **Hardware:** Meta Quest Pro
* **Input Handling:**
    * Meta Quest Pro Stylus Mode (Inverted Grip)
    * Pressure sensitivity readings
    * Gaze tracking integration

