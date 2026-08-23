# Looga Advancement

Looga Advancement provides designer-first systems for progression graphs, perks, challenges, and abilities.

## Package boundary

The package owns:

- progression graphs, prerequisites, requirements, costs, effects, schedules, and evaluation;
- visual progression graph authoring;
- challenge metrics, objectives, repeat rules, progress evaluation, and reward contracts;
- ability definitions, tags, cooldowns, charges, and activation contracts.

The game owns:

- persistence and network authority;
- inventory, currency, faction, and account adapters;
- concrete reward grants;
- concrete ability behavior, animation, physics, and prediction;
- event-bus integration and UI composition.

This boundary lets a project use any backend, networking library, inventory system, or movement framework.

## Progression

Create a **Progression Graph** and open it with **LoogaSoft > Advancement > Progression Graph**. Root nodes select a branch. Connected nodes inherit their branch from their prerequisites. A node with prerequisites from multiple branches becomes a shared node.

Use a **Progression Program** to combine a graph with point, level, season, and persistence policies. `ProgressionEvaluator` evaluates state but does not change it. The game must validate and commit purchases through its authority layer.

## Challenges

Create challenge metrics for events such as eliminations, crafted items, or completed matches. A game adapter converts its events into `ChallengeSignal` values. `ChallengeEvaluator` updates progress and reports completion. The game then grants the referenced reward definitions through `IChallengeRewardHandler`.

## Abilities

Create ability and ability-tag assets. `AbilityController` applies tag, cooldown, and charge rules. An `IAbilityExecutor` performs the game-specific behavior. Do not put movement, animation, networking, or damage logic in the package.

## Stable data

Definitions generate stable IDs. Save stable IDs, ranks, and amounts instead of Unity object references. The snapshot classes do not select a serializer. A project can use JSON, MemoryPack, a database document, or another format in its adapter layer.

## Optional integrations

Looga Advancement keeps its core runtime independent of reactive and serialization libraries. Use the LoogaSoft package support window to enable integrations only when their dependencies are installed.

- **R3** exposes current progression ranks, levels, points, availability, challenge progress, and completion as observable state streams.
- **MemoryPack** provides version-tolerant DTOs and converters for progression and challenge snapshots.

Disabling either integration removes its assembly from compilation. It does not change the core snapshot or evaluation APIs.

## Existing project migration

Keep an existing definition script's Unity GUID when moving its implementation into this package. Remove the old script only after the package owns that GUID. This lets catalogs, graphs, programs, and their sub-assets resolve without reauthoring them.

Keep game-specific serialized save DTOs at the game boundary. Convert those DTOs to the package snapshots when loading, and convert package snapshots back before saving. Do not couple Looga Advancement to a game serializer or backend schema.
