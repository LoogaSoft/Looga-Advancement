# Architecture

## Rule

Looga Advancement describes and evaluates advancement. It does not own authoritative game state.

## Runtime flow

1. A game adapter loads authoritative state.
2. The package evaluates an authored request or signal.
3. The game validates domain-specific rules.
4. The game commits the authoritative result.
5. The game publishes the confirmed state to UI and gameplay systems.

## Adapters

Use adapters for inventory costs, currencies, faction levels, challenge events, reward grants, ability execution, and persistence. Keep SDK types and game-specific enums outside the package.

## Skills and perks

A skill tree and a perk tree use the same progression graph. Use branches for disciplines and nodes for ranks or perks. Do not create a second graph runtime for different labels.
