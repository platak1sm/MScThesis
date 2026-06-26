# Gaze and Stylus Interaction for Precise Object Manipulation in XR: System Design and User Evaluation.

**Authors:** Emmanouil Platakis(Me), Suqi Xiang and Xu Han
**Supervisor:** Ken Pfeuffer
**Context:** Master Thesis, 30 ECTS — Aarhus University, Department of Computer Science
**Grade:** **12** (Danish grading scale — highest mark)

[Demo](https://youtu.be/bcVJdNiHlLo)

---

## Overview

A hybrid VR interior-design tool that bridges 2D surface precision and 3D spatial freedom on the Meta Quest Pro. The system combines a pressure-sensitive stylus (Touch Pro in inverted grip) with eye-tracking to enable seamless transitions between writing on a physical desk and manipulating distant 3D objects in mid-air — with no explicit mode UI.

**Design question:** *How can we exploit the precision of a 2D pen on a physical surface to facilitate 3D object manipulation in VR?*

## My role

Lead system designer and engineer for the integrated application (Chapter 5). End-to-end ownership of the interaction architecture: the six-state FSM, the gaze-driven axis routing, the layered proxy stack, and the engineering tuning that holds it together.

## Key contributions

- **Six-state FSM** composing four manipulation techniques (direct/indirect × air/table) into a single continuous workflow. Transitions happen implicitly through gaze, pen posture, and pressure.
- **Novel mode — IndirectTableObject:** 2D pen motion on the desk mapped to 3D world axes via gaze-driven axis routing. Lateral pen movement always maps to X; forward/backward motion routes between Y (vertical) and Z (depth) based on head pitch and a "glance at pen" modifier.
- **Layered proxy stack:** permanent shadow proxy on the desk for ambient spatial context + transient on-pen minimap (smartwatch-style top-down view) for active depth disambiguation.
- **Sticky-gaze selection (300 ms buffer)** absorbs micro-saccades and brief desk glances without dropping targets.
- **Visual-angle gain** scales small pen movements into proportionally larger object displacements when reaching distant targets.
- **Engineering tuning:** posture hysteresis (17°/27°), pressure-threshold gating (~0.15), 1.30× table indirect gain multiplier, One Euro filter for gaze smoothing.

## Tech stack

Unity 6 · C# · Meta Quest Pro / Touch Pro (OVRInput, inverted stylus grip) · Eye Tracking SDK

## Authors

Joint thesis with Suqi Xiang and Xu Han, supporting a controlled user study evaluating the four manipulation techniques in isolation.

---

Manos Platakis · platakism11@gmail.com
