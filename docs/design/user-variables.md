# Global Variable List

Variables the person defines, kept by the server and used anywhere a variable can be used.

## What it is

- A variable has a **name**, a **type**, a **start value**, a **keep** switch and an optional **description**. The name is used as `{user.name}`: in a text, in a rule and in an action's text field.
- **Types:** Text, Number, True/False. They are stored as a string, a number and a boolean, so the existing rules and formats work on them unchanged.
- **Name and type are fixed** after creation (a profile refers to the name, and a value of another type could break a rule). To change either, delete the variable and add a new one.
- **Limits:** at most 200 variables; a name is a letter followed by letters, digits or `_` (at most 40 characters), unique ignoring case; a description at most 200 characters; a text value at most 1024 characters.
- A Number or True/False variable **without a start value has no value** and shows as unavailable (the same state as a variable a plugin does not provide). A Text variable always has a value, possibly empty.

## Where it lives

- `UserVariableService` (Core, `Variables/`) owns the list and publishes each variable into the shared `VariableStore` as `user.<name>`, so templates, rules, sliders and the live preview need no special case. It is also an `IVariableCatalogSource`: the picker shows the list under "Global Variable List".
- **Files in the data folder** (write to a temporary file, then rename; a file that cannot be read is renamed to `.broken` and the list starts empty):
  - `user-variables.json`: `{ "formatVersion": 1, "variables": [ { "name", "type", "initial", "keep", "description" } ] }`.
  - `user-variable-values.json`: `{ "formatVersion": 1, "values": { "<name>": value } }`, only for variables with **keep**. It is written 2 seconds after the last change and when the server stops. A variable without keep starts again from its start value after a restart.
- **Reserved prefix.** A plugin cannot write or remove a `user.*` name (the store a plugin gets ignores it), and the plugin ids `user`, `system`, `core` and `self` are refused at install. The prefix `self.` is reserved for a button's own state (see variable-types-and-conditions.md).

## The Set variable action
Four small actions (category "Variables"), each with a `variable` (`user.<name>`) picked in the variable picker, which lists only your own (writable) variables: `core.variable.set` (`value`), `core.variable.add` (`amount`, only Number variables), `core.variable.toggle` (only True / False variables) and `core.variable.reset`. The older all-in-one `core.setVariable` (settings `variable`, `mode`, `value`, `amount`) still runs so saved buttons keep working, but the editor no longer offers it. The table shows what each mode does.
`core.setVariable` (category "Variables"). Settings: `variable` (`user.<name>`), `mode` and, depending on the mode, `value` or `amount`.

| Mode | Does | Works for |
|---|---|---|
| `set` | Writes `value` (its `{templates}` are filled first). An empty value on a slider or knob writes the dragged number. | all types |
| `toggle` | Switches True/False. | True/False |
| `add` | Adds `amount` (default 1, may be negative); no value counts as 0. | Number |
| `reset` | Goes back to the start value. | all types |

It reports its outcome: a missing variable is `NotFound` (so is any name that does not start with `user.`), a wrong type or a value that does not fit is `InvalidParameter`, an empty variable setting is `NotConfigured`. Each write is atomic, so two presses at once do not lose an update. Plugin widgets cannot run `core.*` actions.

There is no limit on how often a variable is written and no check for a loop (a rule that changes a variable which changes the rule again). Rules that write variables on their own are capped by their start limits instead; see [automation-rules.md](automation-rules.md).

## Editor

- **Preferences → Global Variable List** adds, edits and deletes variables. Every change is sent as the whole list (`PUT /api/user-variables`); the server accepts all of it or none of it. Before a delete, `GET /api/user-variables/usage?name=` lists up to 50 widgets that use the variable (text, rules, action settings, properties) and the person confirms.
- The action form offers the variables of the list and only the modes that fit the type.
- The profile check reports `E220` when a Set variable action points at a variable that does not exist, and `W231` when a text, rule or action uses a `user.*` name that is not in the list.
