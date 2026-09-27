# Redux interface principles and workflow review

Redux manages dense lists that users may spend hours arranging. Make the next action and its
result understandable without sacrificing useful information or existing customization. Review
the whole workflow before adjusting a single control's appearance.

## Shared rules

1. **Keep the three mod panes related.** Active, Inactive, and Override use the same header,
   scrollbar, column-header controls, selection, and separator components. Differences must explain
   real behavior: Active has game load-order numbers; Inactive and Override have visual organization.
   Override retains its semantic title color.
2. **Align data with its heading.** Center order numbers beneath section icons and align mod names with section labels.
   Headings, numbers, names, and interaction surfaces share one 20px child-level step.
   Other data columns remain aligned.
   Keep neutral rounded brackets and rotating chevrons outside the text. Use a compact leading inset based on whether numbers are present; do not add a blank number column to panes without load-order numbers. Name text stays inside its column, and resize limits apply throughout the drag. Resizing, hiding, and reordering columns must
   preserve usable labels and hit targets.
3. **Make states distinguishable.** A resting separator is a section heading. Hover is temporary;
   ordinary mod selection remains visible after the pointer leaves. Separators retain brief
   click feedback without a persistent selection box. Sorting must identify its column and direction. A disabled action must reflect whether it can run, not stale drag or busy state.
4. **Use shared semantic resources.** Text, focus, selection, borders, typography, and chevrons
   follow the active theme. Preserve custom separator colors, icons, optional lines, text-color
   preferences, and Reduce Motion. Do not hard-code purple as the interaction color.
5. **Explain scope and consequences.** Save stores a Redux order; Sync writes the game order.
   Persistent separator styling is shared, while placement belongs to each saved order.
   Expanded separator dragging moves the heading; collapsed dragging carries its section.
   Parent/child relationships must be understandable before the user moves or deletes a group.
6. **Keep everyday actions discoverable.** Use concrete verbs and nearby feedback. Context menus
   and shortcuts provide fast alternatives, but should not be the only discoverable route to an
   essential task. Add controls only when they resolve a demonstrated problem.
7. **Make recovery part of the interaction.** Validate undo/redo after organization changes,
   cancellation and failure after sync, and restoration after clearing filters. A success message
   must correspond to completed work. Do not silently discard hidden rows or user configuration.

## Interaction states

Keep structural guides below content and controls in visual emphasis. Separator branches use
the theme's neutral border role, not text or accent color. Reserve stronger color for meaningful
category identity, actions, focus, and state feedback. Apply the same roles throughout the app;
do not invent a new hover treatment for each component. This follows the hierarchy guidance in
[Microsoft Fluent](https://fluent2.microsoft.design/color) and the secondary resting emphasis in
[IBM Carbon's tree view](https://carbondesignsystem.com/components/tree-view/style/).

| State | Expected presentation |
|:--|:--|
| Resting | Quiet surface; separator hierarchy conveyed by heading size and neutral brackets in a separate grouping gutter |
| Hover | Temporary theme/custom-color emphasis, without looking selected at rest |
| Selected | Existing mod-row cue; separators use brief click confirmation without an added outline |
| Keyboard focus | Visible focus location; keyboard actions operate on the expected control |
| Drop target | Insertion feedback corresponds to the complete order, including filtered-out rows |
| Column sort | Direction glyph and accessible status on the currently sorted column |
| Busy/disabled | Only affected actions unavailable; restored on success, failure, and cancellation |

## Local review: 22 September 2026

This is a source and automated WPF review of the local development build, not a claim that a
complete user study or interactive desktop acceptance test has passed.

| Observation | Change or follow-up |
|:--|:--|
| An added separator selection outline competed with existing interaction feedback | Remove the outline; retain brief click confirmation and one quiet hover treatment |
| Category/separator editing became a large styling workspace | Pair name and icon, then show a color field and hue strip beside presets/saved colors. Keep hex entry visible, disclose description and precision sliders on demand, and omit the redundant live preview |
| Mod click flashes ignored the category-interaction preference | Use the theme accent unless category-colored interactions are enabled |
| Column sorting has no visible direction cue | Add a direction glyph and accessible status derived from the actual collection view |
| Separator controls shifted ordinary mod names | Inset child numbers and names together, leaving other columns fixed; remove the gutter and row offsets in views without separators |
| Separator counts competed with the pane header vocabulary | Reuse the actual pane count badge styles, displaying a number with descriptive tooltip/accessibility text |
| More space and decoration did not resolve hierarchy confusion | Use the approved outline-gutter prototype: continuous rounded parent/child brackets driven by the existing row-animation clock, rotating chevrons, paired number/name indentation, and fixed metadata columns; validate in complete lists |
| Users have missed mod-update and order-copy commands | Check whether a first-time user can find the current commands without instruction; further usability validation remains open |
| Past reports involve freezes and stale disabled toolbars | Existing regression coverage is necessary, but repeat the drag/collapse/sync workflow interactively before release |

## Acceptance workflow

Use a disposable profile or test data. Record what was actually checked and any unresolved issue.

- Find a mod by name/category; move it before a visible row while filtering. Clear the filter and
  check its full-order position and the relative order of hidden mods.
- Put mods between a parent and two sub-separators. Collapse each sub-separator, then the parent;
  expand again. Check membership, animation, selection, and responsiveness.
- Move an expanded heading and a collapsed section. Undo and redo each operation; confirm which
  rows move and that unrelated entries retain their positions.
- Repeat organization in Inactive and Override. Check column visibility, resizing, scrolling,
  sorting, and clearing sort. These panes must not imply a numbered game load order.
- Switch saved orders containing persistent separators. Verify shared styling but independent
  placement; return to the first order and confirm its organization remains intact.
- Save, then sync. Check Current, dirty state, and command availability. Exercise cancelled and
  failed sync as well as success, especially immediately after dragging between panes.
- Repeat visual checks in Dark, Light, Parchment, and a custom theme; include larger text, narrow
  panes, Windows display scaling, Reduce Motion, keyboard focus, and screen-reader status.

Automated tests should cover state transitions and geometry. Rendered previews can expose
clipping and alignment errors. Neither substitutes for checking whether someone can discover
and complete the task without coaching. Do not label a release ready solely because tests pass.

## Learn from actual modding workflows

Treat these as working audience hypotheses to validate, not researched personas:

- A new user needs to find updates, assign a category, and understand Save versus Sync.
- An experienced organizer needs predictable filtered dragging, nested sections, and fast undo
  across long lists and multiple saved orders.
- Someone using larger text, display scaling, or keyboard navigation needs reachable controls,
  readable states, and menus that remain usable without precise pointer movement.

Ask volunteers to perform the acceptance tasks without step-by-step coaching. Note where they
hesitate, what they expect to happen, whether they finish, and whether recovery makes sense.
Compare proposed changes against those observations. Existing issue reports identify real pain
points but do not establish how common each workflow is. Do not introduce interaction tracking
as part of this review; observation and voluntarily supplied feedback are sufficient to start.

## Reading behind this review

- [UX Design Institute: designing intuitive user interfaces](https://www.uxdesigninstitute.com/blog/design-intuitive-user-interfaces/)
- [Photonlines: an intuitive guide to interface design](https://photonlines.substack.com/p/an-intuitive-guide-to-interface-design)
- [Nectarbits: understanding user behavior](https://nectarbits.com/whitepaper/understanding-user-behavior-a-guide-to-designing-intuitive-software-interfaces.pdf)

The Nectarbits guide reinforces audience definition, observation, consistency, feedback, and
avoiding feature creep. It provides introductory guidance rather than measured evidence: its
Airbnb, Slack, and Duolingo examples are brief descriptions without study methods or results.

Apply consistency, feedback, hierarchy, and user validation to Redux's actual tasks. Treat general
layout heuristics as prompts for evaluation rather than universal rules for a dense desktop app.
