# Skill: Unity Design Patterns & Architecture
**Trigger:** Architecture design, core system creation, decoupling challenges, and state management.

**Design Context:**
Gabriel Leandro Fuentes, Senior Software Engineer. Absolute priority on decoupling, scalability, and extensibility through classical design patterns adapted for Unity and mobile performance.

**Creational Patterns (Instance Management):**
- **Factory Method & Abstract Factory:** Use for creating complex entities (NPCs, weapons, UI elements) without coupling client code to concrete classes. Essential for modular inventory systems.
- **Builder:** Apply when constructing complex game configurations or initializing multiplayer systems step-by-step.
- **Singleton (Restricted):** Only allowed if strictly necessary for global Managers. Dependency Injection is always favored over direct static access.

**Structural Patterns (System Composition):**
- **Facade:** Provide a simplified interface to complex subsystems, such as physics engines, Wwise integrations, or external APIs (Firebase, third-party SDKs).
- **Decorator:** Use to add behaviors or modifiers to objects at runtime (e.g., temporary power-ups) without altering the base class.
- **Adapter:** Implement to integrate external libraries or services whose interfaces do not match the game’s domain.
- **Flyweight:** Mandatory for managing large amounts of similar objects on mobile, optimizing RAM usage by sharing intrinsic state.

**Behavioral Patterns (Communication & State):**
- **Observer:** The foundation for the project's **Event Bus**. Notify state changes to multiple subscribers without direct coupling.
- **Command:** Encapsulate actions (skills, movement, inputs) to enable replay systems, undo/redo functionality, and streamlined multiplayer logic using Mirror.
- **State:** Use to manage complex Finite State Machines (FSM) for AI (e.g., enemy behaviors) or global game states.
- **Strategy:** Define families of algorithms that are interchangeable at runtime, such as different movement behaviors or attack types within the **AttackManager**.

**Output & Narrative Constraints:**
- Strictly avoid the use of lists, bullet points, or itemized formatting.
- Explanations must be written in fluid, professional, and objective paragraphs.
- Always justify pattern selection based on code scalability, maintainability, and mobile optimization.