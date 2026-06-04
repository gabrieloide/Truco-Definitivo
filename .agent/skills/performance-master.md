# Skill: Unity Core Optimization
**Trigger:** Code review, frame rate analysis, draw call reduction, memory profiling, and garbage collection management.

**Design Context:**
Gabriel Leandro Fuentes, Senior Software Engineer. The goal is to maximize performance within the traditional Unity architecture (GameObjects, MonoBehaviours) across mobile and PC environments, without relying on ECS/DOTS.

**CPU & Scripting Optimization:**
- **Update Loop Management:** Strictly avoid heavy calculations within `Update()`, `FixedUpdate()`, or `LateUpdate()`. Rely on event-driven execution (Observer pattern) or well-managed Coroutines.
- **Component Caching:** Prohibit the use of `GetComponent`, `Find`, or `FindWithTag` within recurring methods. All component references must be cached during `Awake()` or `Start()`.
- **String Handling:** Avoid string concatenation or string manipulations within loops to prevent heap allocations. Use `StringBuilder` for dynamic text generation.
- **Physics Efficiency:** Utilize non-allocating physics queries (e.g., `Physics.RaycastNonAlloc`) instead of standard methods that return arrays.

**Memory & Garbage Collection (GC):**
- **Zero Allocation Policy:** Minimize allocations during gameplay to prevent GC spikes. Avoid `LINQ` and boxing/unboxing operations in critical execution paths.
- **Object Pooling:** Mandatory implementation for all frequently instantiated or destroyed objects (projectiles, enemies, particle effects, UI elements). Instantiate and destroy calls must be minimized.
- **Structs vs. Classes:** Favor `structs` over `classes` for small, short-lived data payloads to utilize the stack instead of the heap.

**GPU & Rendering Optimization:**
- **Draw Call Reduction:** Combine meshes where possible and ensure materials are shared to maximize batching. Avoid redundant state changes in the rendering pipeline.
- **GPU Instancing:** Enable instancing on materials for identical objects scattered throughout the scene.
- **LODs and Culling:** Implement Level of Detail (LOD) groups aggressively. Utilize Occlusion Culling to avoid rendering geometry outside the camera's view.
- **UI Canvas Optimization:** Split large UI Canvases into smaller, localized Canvases based on update frequency. Static UI elements must not share a Canvas with frequently updated elements (like health bars).

**Output & Narrative Constraints:**
- Strictly avoid the use of lists, bullet points, or itemized formatting.
- Explanations must be written in fluid, professional, and objective paragraphs.
- Justify optimization strategies based on empirical performance improvements and resource management.