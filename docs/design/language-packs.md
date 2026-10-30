# Language packs

The editor ships Turkish and English. A person can add another language without a new build: export a table, translate it, import it.
A pack is a set of translated texts for the editor's dictionary; it never changes behaviour.

## Dictionary rules

- A dictionary text is a plain string. A text with values uses `{name}` placeholders; `editor/src/i18n/params.ts` lists the names of each key in call order, and `format()` replaces only those declared names in one pass.
- A text that depends on a number has two keys, `<base>.one` and `<base>.other`, and is read with `tn(base, count, ...)`, which picks the row by `Intl.PluralRules` of the active language. A pack may add `.zero`, `.two`, `.few` and `.many` rows.
- `tr.ts` and `en.ts` are `Record<DictKey, string>`; a test keeps the key sets equal and another records the formatted output of every text with values.

## Storage

The server stores each pack as `languages/<tag>.json` in its data folder and serves it under `/api/languages` to this computer only. It checks the tag (`^[a-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$`, never `tr` or `en`), the size (1 MB), the row count (6000) and the number of packs (20); it never interprets the strings. Files are written to a temporary name and renamed.

```json
{ "meta": { "format": 1, "tag": "de", "name": "Deutsch", "version": 2 },
  "strings": { "app.loading": "Laden…" },
  "sources": { "app.loading": "1f2e3d4c" } }
```

`sources` holds a short hash of the English text a row was translated from. When the English text changes, the row is counted as "needs review" on the Language page; it keeps working.

## What the editor checks

The editor checks every row again each time it loads a pack, because the file can be edited by hand. A row is dropped (English shows instead) when its key is unknown, its text is longer than 2000 characters, or its `{name}` placeholders differ from the English text. The cleaner removes direction overrides, zero-width and control characters, keeps a line break only where English has one, and normalises to NFC. A pack that is missing or damaged shows English.

Text is only ever drawn as text. A source scan in the test suite fails on `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `dangerouslySetInnerHTML`, `document.write`, `eval`, `new Function`, and on `srcdoc` outside the plugin widget launcher.

While a pack is active, every replaced value is wrapped in direction isolates, so a value in another script cannot reorder the words around it.

## Where a person is protected

- Permission, consent, import and removal screens show the pack's text with the English text in brackets next to it (`editor/src/i18n/critical.ts` lists them).
- The status bar always shows "Unofficial language pack: <name>" with an "English" button. Its words are not in the dictionaries, so a pack cannot change or hide them.

## The Language page

Preferences > Language lists the installed packs with how much is translated and how many rows need review, and offers Use, Export CSV, Import CSV and Remove. "New language" asks for a tag and a name, then exports an empty table.

The table has the columns `key`, `english`, `translation`, `note`, `max length`. It is written as UTF-8 with a byte order mark; a cell that starts with `=`, `+`, `-` or `@` gets a leading apostrophe that import removes again. Import reads the `key` and `translation` columns (comma or semicolon separated), shows a report of accepted, empty and refused rows, and saves only on confirmation.

## Plugins

A plugin's texts are read from its own `locales/<language>.json`, using the primary subtag of the active pack's tag, and fall back to English.

## Not done yet

The names and descriptions of actions and categories that come from the server (`catalogText.ts`) are still shown in English for a pack.
