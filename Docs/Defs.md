# Definitions

Source: [Def](../Assets/Scripts/Core/Defs/Def.cs) and
[DefDatabase](../Assets/Scripts/Core/Defs/DefDatabase.cs).

1. Put a new tunable content type in a class deriving from `Baseline.Defs.Def`. Provide its
   `CreateAssetMenu` and override `Validate()` for required references and meaningful value ranges.
2. Create and fill the asset in Unity. Set explicit `defName` before shipping; an empty value falls
   back to the asset name, so renaming that asset changes its identity. Keep display localization
   in `labelKey` and author-only explanations in `editorNote`.
3. Create a database through **Create > Baseline > Defs > Database** and add the asset to its
   `definitions` array. This is one generic array, not separate game-specific catalogs.
4. Give the consuming scene/component a serialized database reference, or load it through a
   deliberately owned Addressables handle. Call `database.Validate()` after loading, before use.

`Find<T>(id)` returns null when absent, `Get<T>(id)` throws, and `All<T>()` enumerates registered
definitions. Validation rejects null/duplicate identities, calls each definition's validator,
checks quest references are registered, and rejects requirement cycles. A new cross-definition
contract needs its own validation; the database cannot infer every custom relationship.

There is no automatic database discovery, global database singleton or Index bootstrap registration.
Adding an `.asset` to a folder is not registration. The presentation ScriptableObjects discussed
in [Audio](Audio.md) and [FX](Fx.md) do not derive from `Def` and cannot go in this array.

## Audit

Run the relevant `BaselineCoreTests` cases and validate the actual database consumed by the feature. Add focused content checks for custom cross-definition invariants; a valid example database does not validate every future catalog.

Related: [Quests](Quests.md), [Assets](Assets.md), [Addressables](Addressables.md).
