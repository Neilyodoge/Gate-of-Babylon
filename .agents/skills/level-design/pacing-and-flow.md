# Pacing, flow, teaching, and guidance

Geometry is the medium; the goal is a felt rhythm.

## Difficulty and tension curve

- **Overall rise with local dips.** Difficulty and tension trend upward toward
  a climax, but spikes should be separated by exploration, story, rewards, or
  safe rooms.
- **Rest before climax.** Place a clear breather, preparation opportunity, or
  resource choice before the hardest beat.

Use the rhythm **introduce → build → peak → release → repeat higher**. Each
cycle's peak exceeds the last while releases prevent fatigue.

## Teaching loop

1. **Introduce:** Show the mechanic in isolation with low failure cost.
2. **Develop:** Combine it with movement, enemies, or a second system.
3. **Twist:** Change context so the player applies the idea rather than repeats
   a memorized action.
4. **Test:** Apply meaningful pressure after the player has demonstrated basic
   understanding.

## Paths and branches

- **Critical path:** Minimum route from entry to goal. It must always be valid.
- **Golden path:** Route most players are expected to take. Pace this route
  deliberately.
- **Branches:** Optional loops for exploration and reward. Keep them readable
  and let them rejoin without excessive backtracking.

## Gating tools

- Locks and keys for explicit order control.
- Ability or build gates for progression-dependent access.
- Encounter gates to regulate combat pacing.
- Resource gates to introduce risk/reward decisions.
- One-way transitions to enforce direction; telegraph them clearly.
- Soft gates such as enemy strength or environmental danger.

## Guidance without invisible walls

- Use light and contrast to attract attention.
- Use architecture and leading lines to point toward objectives.
- Place distinctive landmarks for orientation.
- Keep color and material language consistent.
- Frame future goals in vistas before the player reaches them.

When players get lost, test sightlines and landmark visibility before adding
waypoint UI.

## ProjectR 3D ARPG considerations

- Leave enough lateral room for dodge, enemy flanking, telegraphs, and camera
  collision.
- Evaluate spaces with the largest intended enemy group and effect footprint.
- Distinguish navigation space, combat arena, reward space, and transition space.
- For procedural maps, define generation constraints for mandatory beats,
  maximum dead-end depth, branch rewards, room adjacency, and boss preparation.
- Check each room both in isolation and in multiple generated sequences.

## Blockout review checklist

- Goal is reachable with all requirements obtainable in order.
- Required traversal stays inside player metrics.
- Combat readability survives the intended camera angle and enemy density.
- Tension rises with deliberate recovery beats.
- Every mechanic is introduced before being tested.
- Main route remains legible without waypoint UI.
- Optional content rewards its traversal cost.
- Layout works before art dressing.

## Source and license

Adapted from
[pacing-and-flow.md](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/level-design/references/pacing-and-flow.md)
in `gamedev-skills/awesome-gamedev-agent-skills`, licensed under the
[Apache License 2.0](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/LICENSE).
