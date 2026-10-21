# NaughtyAttributes

The `com.dbrizov.naughtyattributes` Git package supplies Inspector attributes and drawers.
See [Dependencies](Dependencies.md) for revision ownership and the
[upstream documentation](https://dbrizov.github.io/na-docs/) for attribute behavior and limitations.

## Use

Import `NaughtyAttributes` in scripts that need its attributes. Existing consumers include
`ContextManager` and `LocaleSelector`. Keep runtime code dependent on the Core assembly;
editor tooling belongs in editor-only code. When adding an assembly definition, check whether
an explicit `NaughtyAttributes.Core` reference is needed.

Use attributes to clarify authoring, not to replace runtime validation. Preserve serialized
field names and values when changing Inspector presentation. Keep custom drawers in project
editor code and never modify the downloaded package cache.

## Audit

Check compilation of runtime and editor consumers, then inspect the affected component and
exercise any Inspector buttons or conditional fields. The Git package replaces the old
`Assets/Editor/NaughtyAttributes` copy; duplicate Core/Editor assemblies must not remain.
Verify target player compilation when changing runtime assembly dependencies.
