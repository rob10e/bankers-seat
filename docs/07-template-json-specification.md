# Template JSON Specification

## Overview

The canonical machine-readable schema is:

```text
templates/schema/game-template.schema.json
```

This document explains the intent of the main fields.

## Root fields

| Field | Required | Purpose |
|---|---:|---|
| `schemaVersion` | Yes | Structural schema version |
| `templateId` | Yes | Stable lowercase kebab-case template ID |
| `templateVersion` | Yes | Semantic version of template content |
| `name` | Yes | Display name |
| `edition` | Yes | Edition identity and display metadata |
| `description` | No | Catalog and setup description |
| `playerCount` | Yes | Minimum and maximum participants |
| `currency` | Yes | Base-unit label and formatting |
| `denominations` | No | Optional physical-style denominations |
| `bank` | Yes | Starting values and overdraft behavior |
| `assets` | No | Logo and related images |
| `playerFields` | No | Custom tracked player state |
| `actions` | No | Declarative quick actions |
| `sessionOptions` | No | Host-selectable settings |
| `tags` | No | Catalog search tags |

## Edition

```json
{
  "id": "classic-edition",
  "name": "Classic Edition",
  "releaseLabel": "Original rules",
  "year": 2026
}
```

`edition.id` is part of template identity. Do not rely on `edition.name` as an ID.

## Currency

```json
{
  "code": "GAME_CREDIT",
  "symbol": "¤",
  "name": "credits",
  "baseUnitName": "credit",
  "fractionDigits": 0,
  "position": "before"
}
```

Although the schema permits a formatting declaration, the domain stores amounts as integers. The initial product should require `fractionDigits` to be zero unless a future fixed-point implementation is added.

## Denominations

```json
[
  { "value": 1, "label": "1", "asset": "assets/money-1.webp" },
  { "value": 5, "label": "5", "asset": "assets/money-5.webp" },
  { "value": 10, "label": "10", "asset": "assets/money-10.webp" }
]
```

Denominations are presentation metadata. Transactions may still use any integer amount unless a template explicitly restricts amounts to denomination combinations.

## Bank configuration

```json
{
  "startingPlayerBalance": 1500,
  "bankMode": "unlimited",
  "allowPlayerOverdraft": false
}
```

Supported initial bank modes:

- `unlimited` — player balances are tracked; bank supply is not depleted.
- `finite` — the bank has a tracked account and starting supply.

## Assets

```json
{
  "logo": "assets/logo.webp",
  "thumbnail": "assets/thumbnail.webp",
  "background": "assets/background.webp",
  "images": {
    "home": "assets/home.webp",
    "children": "assets/children.webp"
  }
}
```

All paths are relative to the template directory and validated by the server.

## Player fields

### Boolean

```json
{
  "id": "owns-home",
  "label": "Owns a home",
  "type": "boolean",
  "default": false,
  "visibility": "all",
  "editableBy": "host-and-owner",
  "iconAssetKey": "home"
}
```

### Counter

```json
{
  "id": "children-count",
  "label": "Children",
  "type": "counter",
  "default": 0,
  "minimum": 0,
  "maximum": 12,
  "step": 1,
  "visibility": "all",
  "editableBy": "host-and-owner"
}
```

### Enum

```json
{
  "id": "career",
  "label": "Career",
  "type": "enum",
  "default": "none",
  "options": [
    { "value": "none", "label": "None" },
    { "value": "technical", "label": "Technical" },
    { "value": "creative", "label": "Creative" }
  ],
  "visibility": "all",
  "editableBy": "host"
}
```

Supported initial field types:

- `boolean`
- `integer`
- `counter`
- `text`
- `enum`
- `currency`

Field visibility:

- `all`
- `owner-and-host`
- `host-only`

Edit permission:

- `host`
- `owner`
- `host-and-owner`
- `system`

## Actions

Actions use a closed set of declarative operation types.

### Fixed bank payment

```json
{
  "id": "payday",
  "label": "Payday",
  "category": "income",
  "scope": "single-player",
  "operation": {
    "type": "bank-to-player",
    "amount": 500
  },
  "confirmation": "never"
}
```

### Player fee

```json
{
  "id": "service-fee",
  "label": "Service fee",
  "category": "expense",
  "scope": "single-player",
  "operation": {
    "type": "player-to-bank",
    "amount": 100
  },
  "confirmation": "always"
}
```

### Update a field

```json
{
  "id": "buy-home",
  "label": "Buy home",
  "category": "life-event",
  "scope": "single-player",
  "operation": {
    "type": "composite",
    "steps": [
      {
        "type": "player-to-bank",
        "amount": 2000
      },
      {
        "type": "set-field",
        "fieldId": "owns-home",
        "value": true
      }
    ],
    "atomic": true
  },
  "confirmation": "always"
}
```

Initial operation types:

- `bank-to-player`
- `player-to-bank`
- `player-to-player`
- `adjust-player-balance`
- `set-field`
- `toggle-field`
- `increment-field`
- `composite`

No operation may contain source code or expressions. Future conditional behavior must use a tightly controlled declarative condition grammar documented and validated separately.

## Session options

```json
[
  {
    "id": "starting-balance",
    "label": "Starting balance",
    "type": "integer",
    "default": 1500,
    "minimum": 0,
    "maximum": 100000,
    "mapsTo": "bank.startingPlayerBalance"
  }
]
```

Only whitelisted paths may be overridden. Arbitrary JSON-path mutation is not allowed.

## Limits

Recommended initial limits:

- 100 player fields.
- 200 actions.
- 50 denominations.
- 25 options per enum.
- 50 steps per composite action.
- 200 characters per label.
- 2,000 characters per description.
- 10 MB total asset package by default.
- 5 MB per image by default.

Limits should be configurable by administrators within safe maximums.
