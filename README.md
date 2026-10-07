# IMDM 327 — Computational Virtual Reality

Unity course materials for **IMDM 327, Fall 2026**, taught by **Dr. Myungin Lee** at the University of Maryland. These examples are for classroom instruction, experimentation, and student projects.

[Course website](https://sites.google.com/umd.edu/imdm-327-lee-2026f/home)

## Course material

The course introduces computation, trails, flocking, data, XR interaction, sound, and images. The code examples in [Assets/327_CourseMaterial](Assets/327_CourseMaterial) build up in a few steps:

| Step | Examples | Focus |
| --- | --- | --- |
| 1. Motion | `ThreeBody.cs` | Forces, acceleration, velocity, and trails |
| 2. Data | `DataCSV.cs`, `DataJSON.cs`, `SolarSystemStarter.cs` | Load data and use it in a simulation |
| 3. Sound | `SoundFM.cs`, `SoundFMPiano.cs`, `AudioSpectrum.cs` | FM synthesis, notes, and audio visualization |
| 4. Flocking + sound | `PianoBoids.cs`, `FlockingSynth.cs`, `BoidFMSynth.cs` | Connect boid motion and sound parameters |
| 5. Gesture interaction | `MediaPipeBodyTracker.cs`, `InteractiveBody.cs` | Webcam landmarks and hand-controlled gravity |

Open the project in Unity and start with `Assets/327_CourseMaterial/Scenes/00_World.unity`. Scripts are in `Assets/327_CourseMaterial/Scripts`.

Gesture scenes are in `Assets/327_CourseMaterial/Scenes/Gesture`: `Gesture-basic` shows landmarks; `Gesture-flocking` adds boids. Select `MediaPipe` to toggle Debug Mode or use Show/Hide landmarks. Pinch and shake either hand to attract the boids. Stronger shaking creates stronger gravity, which fades when you stop moving. These samples use a desktop webcam and the bundled Windows MediaPipe runtime.

![Sound](https://github.com/user-attachments/assets/348a0502-0e68-42f1-b30e-58dc77cb91aa)

## Video

[![Watch the video on YouTube](https://img.youtube.com/vi/5UXLY4lZ8zo/hqdefault.jpg)](https://www.youtube.com/watch?v=5UXLY4lZ8zo)

## License

Course material code is released under the [MIT License](LICENSE). Third-party packages and assets retain their own licenses.
