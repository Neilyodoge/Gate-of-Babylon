---
name: level-design
description: >
 Design and build playable levels — the blockout/whitebox-to-playable workflow,
 player metrics and grid layout, pacing and flow (tension/rest curve), gating
 and the critical path, and encounter design. Engine-neutral practice. Use when
 the user mentions level design, blockout/whitebox/greybox, level layout, level
 pacing, encounter design, or the critical path through a level.
---

# Level design

A level is a **sequence of intentional experiences** delivered through space.
Good level design is a *process*: define the metrics movement is built on, block
out geometry with primitives, play it, then dress it — never the reverse.

## When to use

- Use to plan a level's structure: critical path, pacing, gating, encounters,
  and where the player learns versus where they are tested.
- Use the **blockout → test → iterate → dress** workflow.
- Use player movement and camera metrics to keep geometry reachable and fair.

For algorithmic generation, pair this skill with procedural-generation guidance.
For ProjectR, treat generated topology and authored room content as separate
layers: generation provides variety; authored constraints provide pacing.

## Core workflow

1. **Derive metrics first.** Measure run speed, traversal range, combat reach,
   camera range, dodge distance, room-clear time, and safe combat spacing.
2. **Block out.** Build the whole level from primitives at correct scale.
   Validate flow, sightlines, navigation, reachability, and combat readability.
3. **Define the critical path** from entry to goal and the expected golden path.
   Add optional or secret branches without obscuring progression.
4. **Pace the experience.** Alternate tension and rest in a deliberate curve.
5. **Teach, then test.** Introduce mechanics safely, develop them, add a twist,
   then test them under pressure.
6. **Gate with intent.** Use locks, resources, abilities, encounter completion,
   and one-way transitions to control order and pacing.
7. **Playtest and iterate.** Record where players get lost, stuck, bored, or
   killed unfairly. Fix the blockout before visual dressing.

## Patterns

### Player metrics drive dimensions

Maintain a small metrics sheet shared by design and implementation. Required
traversal uses comfortable values; limit tests belong in optional challenge
spaces. If movement changes, revalidate affected geometry.

### Pacing as a tension timeline

Author beats with an intended intensity:

```text
Entry/teach  1 → Combat 4 → Reward/rest 2 → Elite 7 → Preparation 3 → Boss 10
```

The curve should rise overall while retaining local dips. Avoid consecutive
high-intensity beats unless exhaustion is an explicit goal.

### Gating as a graph

Represent rooms as nodes and transitions as edges. Mark requirements and grants
on edges or nodes. Validate that the goal remains reachable in acquisition
order and that no required reward can spawn behind its own gate.

## Review checklist

- Critical path is solvable and cannot soft-lock.
- Required traversal and combat spaces respect player metrics.
- Tension forms a rising sawtooth with recovery beats.
- New mechanics are introduced before lethal tests.
- Lighting, sightlines, landmarks, and composition guide the player.
- Optional paths reward exploration and rejoin cleanly.
- The untextured blockout already plays well.
- Conclusions are supported by playtest observations.

## Pitfalls

- Dressing before the blockout is validated.
- Geometry based on intuition instead of movement and camera metrics.
- Flat pacing or uninterrupted combat.
- Testing a mechanic before teaching it.
- Soft-locks, unrecoverable dead ends, and untelegraphed one-way transitions.
- Using procedural generation as a substitute for authored pacing constraints.

## Additional reference

Read [pacing-and-flow.md](pacing-and-flow.md) when the task needs detailed
guidance on tension curves, teaching loops, gating, navigation, or blockout review.

## Source and license

Imported from
[gamedev-skills/awesome-gamedev-agent-skills](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/level-design/SKILL.md).
Licensed under the
[Apache License 2.0](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/LICENSE).
This local copy was adapted to remove unavailable companion-skill dependencies
and add ProjectR-oriented 3D ARPG guidance.
