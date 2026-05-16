# quicksheet-regex

A regex pattern explainer extension for [QuickSheet](https://github.com/cemheren/QuickSheet) — breaks down regular expressions into human-readable components right in your spreadsheet.

## What it does

Type a regex pattern and get an instant breakdown of every component with plain-English explanations. No more guessing what `(?<=\d{2})\w+?[^aeiou]*$` means.

## Usage

In any QuickSheet cell, type:

```
regex ^[a-z]+\d{2,4}$
```

The extension outputs a table explaining each token:

| Token | Explanation |
|-------|-------------|
| `^` | Start of string anchor |
| `[a-z]+` | Any lowercase letter (one or more, greedy) |
| `\d{2,4}` | Any digit [0-9] (2 to 4 times) |
| `$` | End of string anchor |
| | **Summary:** Full-string match, 4 component(s) |

## Supported features

- **Anchors** — `^`, `$`
- **Character classes** — `[abc]`, `[a-z]`, `[^0-9]`
- **Escape sequences** — `\d`, `\w`, `\s`, `\b`, `\.`, etc.
- **Quantifiers** — `*`, `+`, `?`, `{n}`, `{n,}`, `{n,m}`, lazy variants
- **Groups** — capturing `()`, non-capturing `(?:)`, named `(?<name>)`
- **Lookahead/lookbehind** — `(?=)`, `(?!)`, `(?<=)`, `(?<!)`
- **Alternation** — `|`
- **Validation** — detects invalid patterns with error message

## Install

1. Clone this repo alongside your QuickSheet extensions directory
2. The extension registers with prefix `regex`

```bash
git clone https://github.com/cemheren/quicksheet-regex.git
```

## Requirements

- .NET 9 SDK
- [QuickSheet](https://github.com/cemheren/QuickSheet)

## Protocol

Uses the standard QuickSheet extension JSON-lines protocol:
- Receives `{"type":"init"}` → responds with registration
- Receives `{"type":"activate","id":"...","params":["pattern"]}` → responds with cell writes

## License

MIT
