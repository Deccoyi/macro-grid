# Variable types and readable conditions

**Status: planned, not implemented.**

## Problem

When a user dynamizes a property (color, text, icon, animation) they pick a variable such as `system.audio.muted` and then must type a
comparison value. The editor gives no hint about **what the variable returns** or **what to type**:

- `system.audio.muted` is a `bool`. In a text template it renders as `Açık` / `Kapalı`, so the user guesses `Açık`, `true`, `1`...
- Only one of those guesses works, and the failure is silent (the rule just never matches).
- The value field hint only says "50 or text" (`dynamic.value.hint`), regardless of the variable.

## What happens today (verified in code)

| Piece | Behavior |
| --- | --- |
| `VariableInfo(Name, Description, Example, Category)` | No type, no unit, no allowed values. |
| `DynamicRuleEvaluator.EvaluateComparison` | A `bool` is not numeric (`TryToDouble` fails), so it is compared as text: `bool.ToString()` is `"True"` / `"False"`, matched **case-insensitively**. So `== true` / `== false` works, **`== 1` / `== 0` silently never matches**. |
| `Template.FormatBool` | Renders `Açık` / `Kapalı` (or custom `{name\|on/off}`). This is display only; `== Açık` does **not** match. |
| `editor/src/grid/evaluateDynamic.ts` | Mirror of the server evaluator. A JS `boolean` is also non-numeric, so `String(true)` = `"true"`. Same behavior. |
| `VariablePicker` | Shows name, description and a template example only. |
| Plugins | Each plugin describes its own variables through `IVariableCatalogSource`; several are `bool` (e.g. `*.connected`, `*.streaming`, `*.studioMode`) with the same problem. |

## Goals

1. Every variable declares its **type** (and where useful its unit and allowed values), and the editor shows it.
2. For a boolean variable the value field offers a fixed choice, so nothing is typed by guessing.
3. Booleans accept **both** `true` / `false` **and** `1` / `0` in conditions (server and editor mirror behave identically).
4. Existing profiles keep working with no migration.

## Design

### 1. Type metadata on `VariableInfo`

Extend the record in `MacroGrid.Plugin.Abstractions/IVariableCatalogSource.cs` in a backward-compatible way (new optional parameters at the end,
so existing plugins still compile and load):

```csharp
public enum VariableType { Text, Number, Boolean, Duration, DateTime }   // default: Text

public sealed record VariableInfo(
    string Name, string Description, string Example, string Category,
    VariableType Type = VariableType.Text,
    string? Unit = null,                       // "%", "GB", "kbps"...
    IReadOnlyList<string>? Values = null);     // allowed values for a fixed-choice text variable (e.g. a status)
```

- This is an additive change to the plugin abstraction, so bump the `MacroGrid.Plugin.Abstractions` minor version and note it in
  `docs/versioning.md` / both changelogs. Plugins that do not pass a type stay `Text` (today's behavior).
- The catalog API already serializes `VariableInfo` to the editor; add `type`, `unit`, `values` to `editor/src/api/types.ts`
  (`VariableInfo`), with the type as a string union.

### 2. Tag the built-in variables

- `SystemAudioProvider`: `system.audio.master` → `Number` + `%`; `system.audio.muted` → `Boolean`.
- `SystemMetricsProvider`: `system.time` → `DateTime`, `system.uptime` → `Duration`, `system.cpu` / `system.ram` → `Number` + `%`,
  `system.ram.used` / `system.ram.total` → `Number` + `GB`.
- Audit any other provider registered in `ServerApp.cs`.
- Official plugins (separate `macro-station-plugins` repository, own versioning): tag their boolean and numeric variables in a follow-up
  there. Do this only after the abstraction change is released; it must not block this work.

### 3. Boolean comparison in the evaluator

In `DynamicRuleEvaluator.EvaluateComparison` and its TS mirror, normalize booleans before comparing:

- If the live value is a `bool`, or the expected value text is one of `true` / `false` / `1` / `0` (case-insensitive) and the live value
  is a `bool`, compare as booleans: `true` ≡ `1`, `false` ≡ `0`.
- Only `==` and `!=` are meaningful for booleans; `>`, `<`, `between` stay false as today.
- Numeric and text paths are unchanged, so no existing rule changes meaning. (`1` compared to a non-bool number is still numeric.)
- The two implementations must stay identical; add matching test cases on both sides.

### 4. Editor: show the type, constrain the value

In `DynamizeModal.tsx` (condition row) and `VariablePicker.tsx`:

- **Picker:** show a small type badge next to each variable (`bool`, `number %`, `text`, `time`...) and, for booleans, the possible values
  in the description line. The catalog entry gives all of it.
- **Condition row, boolean variable:** replace the free text input with a select: `true` / `false` (labels via i18n, e.g. "On (true)" /
  "Off (false)"), and restrict the operator list to `==` / `!=`. Stored value is the literal `true` / `false`.
- **Condition row, variable with `Values`:** a select of those values.
- **Condition row, number variable:** keep the input, add the unit as a suffix and use it as the placeholder ("50 %").
- **Unknown or text variable:** keep today's free input, but keep the hint generic.
- Changing the variable resets the value/operator if they no longer fit the new type.
- New i18n keys in **both** `tr.ts` and `en.ts` (no hard-coded UI text): type names, boolean labels, the boolean hint
  ("true / false, 1 / 0 also accepted").

### 5. Docs

- Add a "Variable types" section to `docs/architecture.md` (what each type returns, how booleans compare, how a template renders them).
- Document the new `VariableInfo` fields in `MacroGrid.Plugin.Abstractions/README.md` so plugin authors declare types.
- Update `docs/CHANGELOG-developer.md` (detailed) and `docs/CHANGELOG.md` (one short line) under `[Unreleased]`.

## Out of scope

- Localizing the default `Açık` / `Kapalı` template words (tracked in the roadmap's known gaps).
- A general expression language or new operators.
- Changing how templates render values.

## Implementation order

1. `VariableInfo` extension + `VariableType` (abstractions), then tag the built-in providers.
2. Evaluator boolean normalization (server + TS mirror) with tests, verifying old rules are unchanged.
3. `types.ts`, picker badge, condition row select/units, i18n keys.
4. Docs and changelogs.
5. Follow-up in the plugins repository (tag plugin variables).

## Verification

- Unit tests: `bool` live value vs `true`, `True`, `1`, `false`, `0` with `==` / `!=`; numeric and text rules unchanged; TS and C# agree.
- Manual: dynamize a widget's color on `system.audio.muted`, confirm the editor offers a true/false select, mute/unmute and watch the preview
  and the phone change.
- Load an old profile with a `== true` rule and one from a plugin without type info; both must behave as before.
