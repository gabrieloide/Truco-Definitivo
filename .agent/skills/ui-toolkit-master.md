# Skill: Unity UI Toolkit & Modular Interface Design
**Trigger:** Designing user interfaces, creating menus, HUD implementation, or styling UI elements using UXML and USS.

**Design Context:**
Gabriel Leandro Fuentes, Senior Software Engineer. The objective is to move away from legacy uGUI towards the modern UI Toolkit, utilizing XML-based hierarchies and CSS-like styling to ensure responsive, performant, and maintainable interfaces.

**UXML Structure & Hierarchy:**
- **Semantic XML:** Use UXML to define a clear and logical hierarchy of visual elements. Avoid deeply nested structures that increase the complexity of the visual tree.
- **Reusable Templates:** Prioritize the use of Template assets for recurring UI components like buttons, health bars, or inventory slots to maintain consistency across the project.
- **Decoupling from GameObjects:** UI logic must reside in specialized C# scripts that manipulate the VisualElement tree, rather than being scattered across multiple GameObjects.

**USS Styling & Flexbox:**
- **BEM Convention:** Adopt the Block Element Modifier (BEM) naming convention for USS classes to ensure styles are unambiguous and easy to debug.
- **External Style Sheets:** Strictly prohibit inline styling within UXML files. All visual properties must be defined in external USS files to allow for easy theme switching and global updates.
- **Responsive Layout:** Leverage the Flexbox model provided by UI Toolkit to create responsive layouts that adapt to different aspect ratios and resolutions (Mobile vs. PC) without manual adjustments.

**Performance & Best Practices:**
- **Visual Tree Optimization:** Minimize the number of visual elements. Prefer using background images or border properties over adding extra containers for purely aesthetic purposes.
- **Batching & Draw Calls:** Ensure that UI elements sharing the same texture are grouped together to maximize batching and reduce draw calls.
- **Asset Management:** Use the Addressables system to load and unload UI assets dynamically, keeping the memory footprint lean during intense gameplay.

**Output & Narrative Constraints:**
- Strictly avoid the use of lists, bullet points, or itemized formatting.
- Explanations must be written in fluid, professional, and objective paragraphs.
- Focus on the technical advantages of UI Toolkit, such as its retained mode rendering and separation of concerns.