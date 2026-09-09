---
name: level-design-planning
description: >-
 Plans game levels, environments, difficulty curves, pacing, encounter
 placement, accessibility, and spatial layouts. Use when producing a level
 concept, map plan, area design, encounter plan, tutorial level, or playtest
 specification.
---

# Level Design Planning

Use this skill for the planning and documentation pass. Pair it with `level-design`
when the task should continue from concept through blockout and playtest.

## Core principles

1. The level teaches the mechanic: introduce, test, then twist.
2. Pacing is rhythm: alternate intensity with exploration, story, or recovery.
3. The environment communicates narrative and gameplay without depending on text.
4. Keep a legible golden path and reward optional detours.
5. Difficulty rises as a curve with ramps, plateaus, spikes, and rests.
6. Accessibility and gameplay readability are design requirements.
7. Playtest evidence overrides designer intuition.

## Planning workflow

### 1. Concept

Define:

- Purpose in overall progression.
- Mechanics introduced, reinforced, tested, or remixed.
- Intended emotional arc.
- Estimated duration and difficulty band.
- Content, performance, production, and technical constraints.
- Success criteria that can be observed during playtests.

### 2. Layout

Create a top-down sketch or text graph marking:

- Entry, exit, and mandatory objective.
- Critical and golden paths.
- Optional areas and reconnection points.
- Encounters, recovery spaces, rewards, and landmarks.
- Gates, keys, irreversible transitions, and fallback routes.

Apply an **introduction → development → twist → test** structure to the main
mechanic. Ensure each major area can be described relative to a landmark.

### 3. Pacing

Plot encounter intensity from 1–10 against progression from 0–100%.
Insert valleys between major peaks. Place the climax late enough to benefit
from buildup, then provide resolution and reward.

### 4. Encounters

For each encounter, specify:

- What the player should learn or prove.
- Enemy or hazard composition and spatial role.
- Arena affordances and constraints.
- Intended intensity and approximate duration.
- Failure consequence and recovery path.
- Viable approaches and dominant-strategy risks.
- Reward and effect on subsequent pacing.

### 5. Environmental storytelling

Add optional, observable evidence about what happened in the location. Keep it
consistent with gameplay affordances and visual language; do not block critical
progress behind lore interpretation.

### 6. Accessibility and readability

- Verify critical information is not communicated by color alone.
- Pair important audio cues with visual alternatives.
- Avoid mandatory precision challenges unless appropriate alternatives exist.
- Check text, subtitles, input expectations, camera readability, and effect noise.

### 7. Playtest

Run silent observation when possible. Record where players:

- Get lost: navigation or guidance failure.
- Get stuck: teaching or difficulty failure.
- Get bored: pacing or density failure.
- Miss content: discoverability or reward-signaling failure.
- Misread danger: telegraph or visual-language failure.

Revise from repeated evidence, distinguishing isolated preference from systematic
failure.

## Recommended output

```markdown
# [Level or area name]

## Purpose and constraints
## Player experience and emotional arc
## Mechanics taught and tested
## Topology and route graph
## Beat and intensity timeline
## Encounter specifications
## Rewards and optional content
## Navigation and landmarks
## Environmental storytelling
## Accessibility and readability
## Blockout requirements
## Playtest hypotheses and acceptance criteria
## Open questions
```

## Pitfalls

- Straight corridors with no meaningful choices or spatial variation.
- Difficulty spikes without prior teaching and escalation.
- Empty traversal that contributes neither decisions nor atmosphere.
- Inconsistent interaction and navigation language.
- Tutorial content separated from the game's actual appeal.
- Using UI markers to conceal an unreadable layout.
- Designing for expert knowledge unavailable to first-time players.

## Source and license

Imported and locally adapted from
[fcsouza/agent-skills level-design](https://github.com/fcsouza/agent-skills/blob/main/skills/level-design/SKILL.md),
licensed under
[GNU GPL v3](https://github.com/fcsouza/agent-skills/blob/main/LICENSE).
Changes: renamed the skill to avoid collision, revised its trigger description,
removed unavailable prerequisite dependencies, and added a reusable output
template.
