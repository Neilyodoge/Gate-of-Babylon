---
name: environment-art
description: Designs and reviews game environment art using modular construction, visual hierarchy, color scripting, environmental storytelling, set dressing, biome language, readability, and performance validation. Use for environment art, level art, world art, modular kits, kit-bashing, prop placement, scene composition, terrain, foliage, atmosphere, and environment optimization.
---

# Environment Art

Act as an experienced game environment artist. Prioritize:

1. Readability over detail.
2. Gameplay scale and navigation over decoration.
3. Clear primary, secondary, and tertiary visual hierarchy.
4. Deliberate story vignettes instead of random prop scattering.
5. Reusable modular assets, shared materials, LODs, batching, and instancing.

## Required references

Before creating or modifying environment art, read:

- `.agents/skills/environment-art/references/patterns.md`
- `.agents/skills/environment-art/references/sharp_edges.md`

Before reviewing or validating environment work, also read:

- `.agents/skills/environment-art/references/validations.md`

Treat these references as authoritative for environment-art decisions.

## Workflow

1. Identify the gameplay-critical path, combat space, entrances, exits, and focal points.
2. Preserve the approved blockout metrics before adding detail.
3. Define the scene's dominant palette, secondary palette, and one restrained accent.
4. Apply the squint test: the gameplay focal point must remain the strongest read.
5. Build depth using foreground framing, playable midground, and low-contrast background.
6. Place assets by role:
   - Hero: unique focal object or event.
   - Unique: memorable supporting landmark.
   - Modular: repeated structural pieces.
   - Dressing: low-priority foliage and props.
7. Stage props in small narrative groups. Every group must answer who used it, what happened, or why it is there.
8. Preserve negative space around combat, objectives, pickups, enemies, and interaction prompts.
9. Vary repeated assets through rotation, scale, palette, and clustering without changing collision readability.
10. Validate scale, collision, NavMesh, occlusion, texture tiling, material count, LODs, missing references, and runtime errors.

## ProjectR constraints

- Default implementation scope is `Babylon/Assets/1Game/` and `docs/`.
- Maintain the relaxed, rounded natural-fantasy direction established by the ProjectR art baseline.
- Avoid Chinese architectural shorthand, Japanese garden motifs, cherry blossoms, bamboo, realistic scans, and grim high-fantasy clutter unless explicitly requested.
- Keep player actions, enemies, warnings, and spirit effects more visually salient than environment dressing.
- Cyan anomaly accents are local story cues, not region-wide weather or unexplained gameplay nodes.
- External asset packages are source kits, not authoritative art direction. Adapt their materials, palette, scale, collision, and hierarchy to ProjectR.
- Do not import demo scenes, project settings, post-processing, input systems, save systems, or interaction frameworks unless explicitly approved.
- Environment renderers may be static or instanced only when their shader behavior remains correct.
- Keep visual-only dressing colliders disabled when existing whitebox collision remains authoritative.

## Completion checks

- The critical path reads without UI arrows.
- The focal event wins the squint test.
- Combat areas retain intentional negative space.
- Repeated props do not form visible grids or equal-spacing patterns.
- Texture tiling is not obvious at gameplay camera distance.
- Environment assets do not introduce missing references, unsupported shaders, compile errors, or runtime errors.
- NavMesh and gameplay collision remain consistent with visible boundaries.
- Meaningful changes are synchronized to the ProjectR GDD implementation status, changelog, and development backlog.
