# IngameDebugConsole

The `com.yasirkula.ingamedebugconsole` Git package supplies an in-game log/command console.
See [Dependencies](Dependencies.md) for installation and revision ownership, and the
[upstream README](https://github.com/yasirkula/UnityIngameDebugConsole) for its prefab and command APIs.

## Integration

Baseline installs the package but does not instantiate its console in Index. If a feature needs
the console, inspect the package's `IngameDebugConsole` prefab and its `DebugLogManager` component,
then author the necessary scene/prefab
reference. Keep application-specific commands in project code, not in the package cache.
Choose the intended development/release availability explicitly when integrating it.

## Audit

Check that the package resolves and its runtime assembly compiles. After adding scene integration,
test opening/closing the console, log capture, command execution, input focus and scene transitions.
Check for duplicate persistent consoles and verify the intended availability in a player build.
Do not claim these interactions were tested solely because the dependency imported successfully.
