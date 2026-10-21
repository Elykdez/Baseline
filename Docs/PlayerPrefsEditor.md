# PlayerPrefsEditor

The editor-only `com.bgtools.playerprefseditor` Git package provides a PlayerPrefs inspection tool.
See [Dependencies](Dependencies.md) for installation and the
[upstream README](https://github.com/Dysman/bgTools-playerPrefsEditor) for window controls.

## Use

Open **Tools > CCG-Tools > PlayerPrefs Editor** to inspect the current project's preference keys.
Read a key's consumer before changing it. Baseline's current settings are primarily JSON-backed;
legacy PlayerPrefs values can participate in migration but are not the current settings store.
See [Saves](Saves.md) for paths and migration rules.

Inspect before editing. Do not clear all preferences to diagnose a single setting. Tests that
change keys must restore previous values or use isolated keys. Do not add a runtime dependency
on the package's editor assemblies or copy its implementation into `Assets`.

## Audit

Verify the editor assembly loads and its window opens for the current project. Confirm any
intentional preference edit through the actual consumer. JSON settings tests and PlayerPrefs
inspection verify different stores; use the relevant checks from [Saves](Saves.md).
