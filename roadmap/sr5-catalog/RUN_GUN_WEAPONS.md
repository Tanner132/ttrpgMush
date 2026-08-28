# Run & Gun Weapons Ledger (CHAR-816)

This is the CHAR-816 source ledger for Run & Gun's new weapons. It is a
review input for the runtime catalog change it accompanies, not a substitute
for the approved book. It extends [`WEAPONS_ARMOR.md`](WEAPONS_ARMOR.md),
which covers the `sr5-core` weapon catalog.

CHAR-816 is the second slice of the Run & Gun expansion project-owner
approved on 2026-08-28 (after CHAR-815's qualities), covering every weapon
product in the book that fits the existing catalog shape without requiring a
new gameplay mechanic. Weapon Accessories, Ammunition, Arrowheads, and a few
other items were out of CHAR-816's own scope; see the Scope and Explicit
Exclusions sections below. **Weapon Accessories and AMMO/Arrowheads have
since been published by CHAR-817 and CHAR-818** — see
[`RUN_GUN_WEAPON_ACCESSORIES.md`](RUN_GUN_WEAPON_ACCESSORIES.md) and
[`RUN_GUN_AMMO.md`](RUN_GUN_AMMO.md). Only Improvised Melee Weapons and the
other minor exclusions below remain out of scope; see
[`../SR5_CATALOG_DEFERRED_WORK.md`](../SR5_CATALOG_DEFERRED_WORK.md).

## Source

Only `run-gun` is used, already pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md) by CHAR-815. Every
citation in this ledger carries the same two-page printed/PDF offset
verified for CHAR-815, re-confirmed here against the footer text "18 ARSENAL
>>" on PDF page 20 matching printed page 18.

## Scope

Included: all 73 weapon products printed across the "Arsenal" chapter's
Blades through Flamethrowers sections (`run-gun` pp. 18-49, PDF 20-51),
published as 73 base catalog entries plus 7 generated alternate-configuration
profiles (80 new `weapons` entries total). Full inventory by category is
below.

Excluded from CHAR-816 itself (see `../SR5_CATALOG_DEFERRED_WORK.md` for the
full reasoning on what remains excluded today):

- **Weapon Accessories** (`run-gun` p. 50 onward, PDF 52+) — deferred at the
  time because the chapter's 6-slot mounting system (top, underbarrel, side,
  internal, barrel, stock) needed a `WeaponMount` schema change and new
  evaluator logic, not just catalog rows. Since published by CHAR-817 as a
  backward-compatible extension of the existing mount system, not a new
  mechanic — see
  [`RUN_GUN_WEAPON_ACCESSORIES.md`](RUN_GUN_WEAPON_ACCESSORIES.md). (The
  install-test mechanics — Matrix Search thresholds, Armorer + Logic extended
  tests, tool-kit vs. tool-shop requirements — remain excluded as
  GM-procedural rules, consistent with how this project treats other test
  procedures.)
- **AMMO section and Arrowheads** — deferred at the time on the mistaken
  belief that `sr5-core`'s own ammunition/grenade/rocket/missile/explosive
  catalog was never wired into the runtime `gear` array. That belief was
  incorrect: CHAR-812 had already published it in catalog version `1.4.0`.
  Since corrected and published by CHAR-818, which extends that existing
  ammunition catalog with Run & Gun's new AMMO types and Arrowheads — see
  [`RUN_GUN_AMMO.md`](RUN_GUN_AMMO.md).
- **Improvised Melee Weapons** — a GM-adjudicated damage-by-narrative-context
  rule, not a purchasable catalog product. Still excluded.
- **Standard chainsaw non-combat variants and other tool-not-weapon items**
  mentioned in the Exotic Melee section's prose — not printed with their own
  purchasable stat block distinct from the combat/monofilament chainsaws
  already cataloged.

## Cost-Modeling Convention

Every entry publishes a fixed `Cost` and `Availability` exactly as printed,
following the same modeling convention as `sr5-core`'s own weapon catalog.
No new parameterization, rating range, or included-component wiring was
required — every weapon in this chapter is a flat-stat, single-configuration
product except where a generated profile is called for (see below).

### Generated alternate-configuration profiles

Seven weapons in this chapter can be reconfigured into a distinct stat block
(the HK XM30's four barrel/receiver swaps, the AK-98's grenade-launcher
mode, the Nissan Optimum II's shotgun-barrel swap, and the Suruchin Bolas's
melee-wrap use). These are published as `GeneratedProfileIds` referencing
separate `Classification: Generated` catalog entries under the parent
weapon, matching the pattern documented (but never actually implemented) in
`sr5-core`'s own ledger. Generated profiles publish only their firing/combat
stat block (Accuracy, Damage, AP, Mode, RC, Ammo) and no separate
`Availability`/`Cost` — the book prices and licenses these as an accessory
or reconfiguration kit on the base weapon purchase, not as an independently
vended product, so this ledger does not model a second purchase.

## New Weapons By Category

### blades

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `highland-forge-claymore` | Highland Forge Claymore | 5 | 2 | (STR + 5)P | -5 | -- | -- | -- | 14R / Restricted | 4,500¥ | `run-gun` p. 18 (PDF 20) |
| `horizon-flynn-rapier` | Horizon-Flynn Rapier | 7 | 1 | (STR + 2)P | -3 | -- | -- | -- | 9R / Restricted | 500¥ | `run-gun` p. 18 (PDF 20) |
| `victorinox-memory-blade-sword` | Victorinox Memory Blade (sword) | 5 | 1 | (STR + 2)P | -2 | -- | -- | -- | 14R / Restricted | 1,500¥ | `run-gun` p. 19 (PDF 21) |
| `victorinox-memory-blade-dagger` | Victorinox Memory Blade (dagger) | 5 | none | (STR + 1)P | -2 | -- | -- | -- | 14R / Restricted | 1,250¥ | `run-gun` p. 19 (PDF 21) |
| `ares-one-monosword` | Ares "One" Monosword | 5 | 1 | (STR + 3)P | -3 | -- | -- | -- | 8R / Restricted | 900¥ | `run-gun` p. 19 (PDF 21) |
| `cougar-fineblades-short` | Cougar Fineblades (short) | 6 | none | (STR + 2)P | -1 | -- | -- | -- | 5R / Restricted | 350¥ | `run-gun` p. 20 (PDF 22) |
| `cougar-fineblades-long` | Cougar Fineblades (long) | 6 | none | (STR + 3)P | -1 | -- | -- | -- | 7R / Restricted | 600¥ | `run-gun` p. 20 (PDF 22) |

### clubs

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `nemesis-arms-maul-stun-staff` | Nemesis Arms Maul Stun Staff | 6 | 2 | 9S(e) | -5 | -- | -- | -- | 8R / Restricted | 1,000¥ | `run-gun` p. 20 (PDF 22) |

### exotic-melee

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `standard-garrote` | Standard Garrote | 5 | none | (STR + 4)S | -6 | -- | -- | -- | none / Legal | 50¥ | `run-gun` p. 20 (PDF 22) |
| `ares-queen-of-hearts-monofilament-garrote` | Ares "Queen of Hearts" Monofilament Garrote | 5 | none | (STR + 6)P | -8 | -- | -- | -- | 18F / Forbidden | 2,000¥ | `run-gun` p. 20 (PDF 22) |
| `bullwhip` | Bullwhip | 6 | 2 | (STR + 1)P | +3 | -- | -- | -- | 6 / Legal | 100¥ | `run-gun` p. 20 (PDF 22) |
| `ash-arms-combat-chainsaw` | Ash Arms Combat Chainsaw | 5 | 1 | 8P | -4 | -- | -- | -- | 6R / Restricted | 2,000¥ | `run-gun` p. 21 (PDF 23) |
| `ash-arms-monofilament-chainsaw` | Ash Arms Monofilament Chainsaw | 5 | 1 | 12P | -8 | -- | -- | -- | 8R / Restricted | 7,500¥ | `run-gun` p. 21 (PDF 23) |

### throwing-weapons

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `boomerang` | Boomerang | Physical - 1 | -- | (STR + 2)P | -- | -- | -- | -- | 4 / Legal | 50¥ | `run-gun` p. 24 (PDF 26) |
| `harpoon-javelin` | Harpoon/Javelin | Physical | -- | (STR + 3)P | -1 | -- | -- | -- | 6 / Legal | 125¥ | `run-gun` p. 24 (PDF 26) |
| `net` | Net | Physical - 2 | -- | -- | -- | -- | -- | -- | 6 / Legal | 350¥ | `run-gun` p. 24 (PDF 26) |
| `cavalier-arms-urban-tribe-tomahawk` | Cavalier Arms Urban Tribe Tomahawk | Physical + 1 | -- | (STR + 2)P | -1 | -- | -- | -- | 4 / Legal | 200¥ | `run-gun` p. 25 (PDF 27) |

### harpoon-guns (new category — Archery skill)

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `standard-harpoon-gun` | Standard Harpoon Gun | 5 | -- | 9P | -2 | SS | -- | 1 | 6R / Restricted | 200¥ | `run-gun` p. 22 (PDF 24) |
| `aquadyne-shark-xs-harpoon-gun` | Aquadyne Shark-XS Harpoon Gun | 5 | -- | 9P | -2 | SS | -- | 5 (m) | 8R / Restricted | 800¥ | `run-gun` p. 22 (PDF 24) |

### crossbows

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ranger-sliver-pistol-crossbow` | Ranger Sliver Pistol Crossbow | 7 | -- | 4P | -- | SS | -- | -- | 6R / Restricted | 300¥ | `run-gun` p. 23 (PDF 25) |

### slingshots (new category — Archery skill)

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ares-giantslayer-slingshot` | Ares Giantslayer Slingshot | 7 | -- | 2P | -- | SS | -- | -- | none / Legal | 50¥ | `run-gun` p. 23 (PDF 25) |

### exotic-ranged

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ares-screech-sonic-rifle` | Ares Screech Sonic Rifle | 6 | -- | 7S | * | SS | -- | 10 (c) | 16R / Restricted | 8,000¥ | `run-gun` p. 26 (PDF 28) |
| `blowgun` | Blowgun | 8 | -- | 1P | -- | SS | -- | 1 (ml) | 4 / Legal | 15¥ | `run-gun` p. 26 (PDF 28) |
| `standard-bola` | Standard Bola | Physical | -- | (STR + 3)S | +4 | -- | -- | -- | 6 / Legal | 75¥ | `run-gun` p. 26 (PDF 28) |
| `nemesis-arms-suruchin-monofilament-bolas` | Nemesis Arms Suruchin Monofilament Bolas | Physical | -- | (STR + 3)S | +4 | -- | -- | -- | 18F / Forbidden | 4,000¥ | `run-gun` p. 26 (PDF 28) |
| `fn-aal-gyrojet-pistol` | FN-AAL Gyrojet Pistol | 5 | -- | 10P | -2 | SA | -- | 10 (c) | 12F / Forbidden | 2,000¥ | `run-gun` p. 26 (PDF 28) |
| `mortimer-trafalgar-gun-cane` | Mortimer of London "Trafalgar" Gun Cane | 6 | -- | 7P | -- | SS | -- | 1 (b) | 9R / Restricted | 750¥ | `run-gun` p. 27 (PDF 29) |
| `mortimer-knockoff-gun-cane` | Mortimer of London Knockoff Gun Cane | 5 | -- | 9P | -- | SS | -- | -- | 6R / Restricted | 150¥ | `run-gun` p. 27 (PDF 29) |
| `sa-retiarus-net-gun-basic` | SA Retiarus Net Gun (basic) | 5 | -- | -- | -- | SS | -- | 4 (b) | 9 / Legal | 750¥ | `run-gun` p. 28 (PDF 30) |
| `sa-retiarus-net-gun-xl` | SA Retiarus Net Gun (XL) | 5 | -- | -- | -- | SS | -- | 2 (b) | 9 / Legal | 1,000¥ | `run-gun` p. 28 (PDF 30) |
| `tiffani-elegance-shooting-bracers` | Tiffani Élégance Shooting Bracers | 5 (6) | -- | 7P | -- | SS | -- | 1 (b) | 10R / Restricted | 1,250¥ | `run-gun` p. 28 (PDF 30) |

### tasers

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cavalier-safeguard` | Cavalier SafeGuard | 5 (6) | -- | 6S(e) | -5 | SA | -- | 6 (m) | none / Legal | 275¥ | `run-gun` p. 29 (PDF 31) |
| `tiffani-defiance-protector` | Tiffani-Defiance Protector | 5 (6) | -- | 7S(e) | -5 | SA | -- | 3 (m) | 2 / Legal | 300¥ | `run-gun` p. 29 (PDF 31) |

### hold-outs

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `fichetti-tiffani-self-defender-2075` | Fichetti-Tiffani Self-Defender 2075 | 4 | -- | 6P | -- | SS | -- | 4 (c) | 3R / Restricted | 350¥ | `run-gun` p. 30 (PDF 32) |

### light-pistols

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `fichetti-executive-action` | Fichetti Executive Action | 6 | -- | 7P | -- | SA/BF | -- | 18 (c) | 10R / Restricted | 300¥ | `run-gun` p. 30 (PDF 32) |
| `shiawase-armaments-puzzler` | Shiawase Armaments Puzzler | 4 | -- | 6P | -- | SA | -- | 12 (c) | 14R / Restricted | 900¥ | `run-gun` p. 31 (PDF 33) |
| `nitama-sporter` | Nitama Sporter | 6 (7) | -- | 6P | -- | SA | -- | 18 (c) | 10R / Restricted | 300¥ | `run-gun` p. 31 (PDF 33) |

### heavy-pistols

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `cavalier-deputy` | Cavalier Deputy | 6 | -- | 7P | -1 | SA | -- | 7 (cy) | 3R / Restricted | 225¥ | `run-gun` p. 32 (PDF 34) |
| `psk-3-collapsible-heavy-pistol` | PSK-3 Collapsible Heavy Pistol | 4 | -- | 8P | -1 | SA | -- | 10 (c) | 16F / Forbidden | 1,050¥ | `run-gun` p. 32 (PDF 34) |
| `savalette-guardian` | Savalette Guardian | 5 (7) | -- | 8P | -1 | SA/BF | 1 | 12 (c) | 6R / Restricted | 870¥ | `run-gun` p. 33 (PDF 35) |
| `onotari-arms-violator` | Onotari Arms Violator | 5 (7) | -- | 7P | -1 | SA | 1 | 10 (c) | 7R / Restricted | 550¥ | `run-gun` p. 33 (PDF 35) |

### machine-pistols

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ppsk-4-collapsible-machine-pistol` | PPSK-4 Collapsible Machine Pistol | 5 (6) | -- | 6P | -- | SA/BF | (1) | 30 (c) | 17F / Forbidden | 2,800¥ | `run-gun` p. 34 (PDF 36) |
| `onotari-arms-equalizer` | Onotari Arms Equalizer | 4 (5) | -- | 7P | -- | BF/FA | (1) | 12 (c) | 7R / Restricted | 750¥ | `run-gun` p. 34 (PDF 36) |
| `ultimax-70` | Ultimax 70 | 5 (6) | -- | 6P | -- | BF/FA | 2 | 15 (c) | 7R / Restricted | 800¥ | `run-gun` p. 35 (PDF 37) |

### submachine-guns

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ares-executioner` | Ares Executioner | 4 (6) | -- | 7P | -- | SA/BF/FA | (1) | 30 (c) | 14F / Forbidden | 1,000¥ | `run-gun` p. 35 (PDF 37) |
| `hk-urban-combat` | HK Urban Combat | 7 (9) | -- | 8P | -- | SA/BF/FA | 2 | 36 (c) | 16F / Forbidden | 2,300¥ | `run-gun` p. 36 (PDF 38) |

### assault-rifles

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ak-98` | AK-98 | 5 | -- | 10P | -2 | SA/BF/FA | -- | 38 (c) | 8F / Forbidden | 1,250¥ | `run-gun` p. 36 (PDF 38) |
| `ares-hvar` | Ares HVAR | 5 (7) | -- | 8P | -- | SA/BF/FA | 3 (4) | 50 (c) | 11F / Forbidden | 2,400¥ | `run-gun` p. 37 (PDF 39) |
| `hk-xm30-assault-rifle` | HK XM30 Assault Rifle | 6 (8) | -- | 9P | -2 | SA/BF/FA | (1) | 30 (c) | 15F / Forbidden | 4,500¥ | `run-gun` p. 37 (PDF 39) |
| `nissan-optimum-ii` | Nissan Optimum II | 5 (7) | -- | 9P | -2 | SA/BF/FA | 1 | 30 (c) | 10F / Forbidden | 2,300¥ | `run-gun` p. 38 (PDF 40) |

### sniper-rifles

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `terracotta-arms-am-47` | Terracotta Arms AM-47 | 7 (9) | -- | 15P | -4 | SA | 1 (3) | 18 (c) | 14F / Forbidden | 35,000¥ | `run-gun` p. 36 (PDF 38) |
| `onotari-arms-jp-k50` | Onotari Arms JP-K50 | 7 | -- | 12P | -3 | SA/BF | 1 | 25 (c) | 13F / Forbidden | 12,500¥ | `run-gun` p. 39 (PDF 41) |
| `pioneer-60` | Pioneer 60 | 5 | -- | 10P | -1 | SS | -- | 5 (m) | 2R / Restricted | 500¥ | `run-gun` p. 39 (PDF 41) |
| `barret-model-122` | Barret Model 122 | 7 (9) | -- | 14P | -6 | SA | (2) | 14 (c) | 20F / Forbidden | 38,500¥ | `run-gun` p. 40 (PDF 42) |

### shotguns

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `auto-assault-16` | Auto-Assault 16 | 4 | -- | 13P | -1 | SA/BF/FA | -2 | 10 (c) or 32 (d) | 18F / Forbidden | 1,800¥ | `run-gun` p. 40 (PDF 42) |
| `mossberg-am-cmdt` | Mossberg AM-CMDT | 5 (7) | -- | 12P | -1 | SA/BF/FA | -- | 10 (c) | 12F / Forbidden | 1,400¥ | `run-gun` p. 41 (PDF 43) |
| `franchi-spas-24` | Franchi SPAS-24 | 4 (6) | -- | 12P | -1 | SA/BF | -1 | 10 (c) | 12F / Forbidden | 1,050¥ | `run-gun` p. 41 (PDF 43) |
| `remington-990` | Remington 990 | 4 | -- | 11P | -1 | SA | -- | 8 (c) | 6R / Restricted | 950¥ | `run-gun` p. 42 (PDF 44) |

### machine-guns

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ge-vindicator-mini-gun` | GE Vindicator Mini-Gun | 4 (6) | -- | 9P | -4 | FA | -2 | 200 (belt) | 24F / Forbidden | 6,000¥ | `run-gun` p. 42 (PDF 44) |
| `sa-nemesis` | SA Nemesis | 5 (7) | -- | 9P | -2 | BF/FA | -2 | 50 (c) or 100 (belt) | 16F / Forbidden | 6,500¥ | `run-gun` p. 43 (PDF 45) |
| `fn-mag-5` | FN MAG-5 | 4 (5) | -- | 11P | -3 | FA | -2 (-8) | 50 (c) or 100 (belt) | 18F / Forbidden | 8,500¥ | `run-gun` p. 43 (PDF 45) |
| `ultamax-mmg` | Ultamax MMG | 5 (6) | -- | 10P | -2 | FA | -1 / -6 | 50 (c) or 100 (belt) | 16F / Forbidden | 7,600¥ | `run-gun` p. 44 (PDF 46) |
| `ruhrmetall-sf-20` | Ruhrmetall SF-20 | 5 (6) | -- | 12P | -4 | FA | -1 (-4) | 50 (c) or 100 (belt) | 18F / Forbidden | 19,600¥ | `run-gun` p. 44 (PDF 46) |
| `ultamax-hmg-2` | Ultamax HMG-2 | 4 (5) | -- | 11P | -4 | FA | -6 | 50 (c) or 100 (belt) | 16F / Forbidden | 16,000¥ | `run-gun` p. 44 (PDF 46) |

### cannons-launchers

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ares-thunderstruck-gauss-rifle` | Ares Thunderstruck Gauss Rifle | 7 (8) | -- | 15P | -8 | SA | (1) | 10 (c) + Energy | 12F / Forbidden | 26,000¥ | `run-gun` p. 45 (PDF 47) |
| `ogre-hammer-sws-assault-cannon` | Ogre Hammer SWS Assault Cannon | 6 | -- | 16P | -4 | SA | -- | 6 (c) | 20F / Forbidden | 32,000¥ | `run-gun` p. 46 (PDF 48) |
| `ares-vigorous-assault-cannon` | Ares Vigorous Assault Cannon | 4 | -- | 16P | -6 | SS | -- | 12 (c) | 18F / Forbidden | 24,500¥ | `run-gun` p. 46 (PDF 48) |
| `onotari-arms-ballista-mml` | Onotari Arms Ballista MML | Missile | -- | Missile | -- | SS | -- | 4 (m) | 19F / Forbidden | 7,500¥ | `run-gun` p. 46 (PDF 48) |
| `mitsubishi-yakusoku-mrl` | Mitsubishi Yakusoku MRL | Missile | -- | Missile | -- | SA/BF | -- | 4 x 2 (m) | 20F / Forbidden | 14,000¥ | `run-gun` p. 47 (PDF 49) |

### laser-weapons (new category)

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ares-redline` | Ares Redline | 9 | -- | 5P | -10 | SA | -- | 10 (c) or external source | 14F / Forbidden | 7,500¥ | `run-gun` p. 48 (PDF 50) |
| `ares-lancer-mp-laser` | Ares Lancer MP Laser | 7 | -- | 7P | -10 | SA | -- | 2x 10 (c) or external source | 18F / Forbidden | 16,000¥ | `run-gun` p. 48 (PDF 50) |
| `ares-archon-heavy-mp-laser` | Ares Archon Heavy MP Laser | 7 | -- | 10P | -10 | SA | -- | External source | 24F / Forbidden | 35,000¥ | `run-gun` p. 49 (PDF 51) |

### flamethrowers (new category)

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `shiawase-blazer` | Shiawase Blazer | 6 | -- | 10P | -6 | SA/BF/FA | -- | 4 (c) | 16F / Forbidden | 2,200¥ | `run-gun` p. 49 (PDF 51) |

### Generated reconfiguration/attachment profiles

| ID | Display name | Acc | Reach | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `nemesis-arms-suruchin-monofilament-bolas-wrap` | Nemesis Arms Suruchin Monofilament Bolas (wrap) | -- | -- | 12P | -8 | -- | -- | -- | n/a (see parent weapon) | n/a (see parent weapon) | `run-gun` p. 26 (PDF 28) |
| `ak-98-grenade-launcher` | AK-98 (grenade launcher) | 3 | -- | Grenade | Grenade | SS | -- | 6 (m) | n/a (see parent weapon) | n/a (see parent weapon) | `run-gun` p. 36 (PDF 38) |
| `hk-xm30-assault-rifle-sniper` | HK XM30 (sniper configuration) | 7 (9) | -- | 9P | -2 | SA | 2 (3) | 10 (c) | n/a (see parent weapon) | n/a (see parent weapon) | `run-gun` p. 37 (PDF 39) |
| `hk-xm30-assault-rifle-lmg` | HK XM30 (LMG configuration) | 6 (8) | -- | 9P | -2 | BF/FA | 2 (3) | 100 (belt) | n/a (see parent weapon) | n/a (see parent weapon) | `run-gun` p. 37 (PDF 39) |
| `hk-xm30-assault-rifle-shotgun` | HK XM30 (shotgun configuration) | 3 (5) | -- | 10P | -1 | SA | (1) | 10 (c) | n/a (see parent weapon) | n/a (see parent weapon) | `run-gun` p. 37 (PDF 39) |
| `hk-xm30-assault-rifle-grenade-launcher` | HK XM30 (grenade launcher configuration) | 4 | -- | Grenade | Grenade | SS | -- | 6 (c) | n/a (see parent weapon) | n/a (see parent weapon) | `run-gun` p. 37 (PDF 39) |
| `nissan-optimum-ii-shotgun` | Nissan Optimum II (shotgun configuration) | 4 (6) | -- | 10P | -1 | SA | 1 | 5 (m) | n/a (see parent weapon) | n/a (see parent weapon) | `run-gun` p. 38 (PDF 40) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| Weapon Accessories chapter (6-slot mounting system) | Excluded from CHAR-816's own scope; **published by CHAR-817**, see `RUN_GUN_WEAPON_ACCESSORIES.md`. Install-test rules (Matrix Search, Armorer + Logic tests) remain excluded as GM procedure. | `run-gun` p. 50+ (PDF 52+) |
| AMMO section and Arrowheads | Excluded from CHAR-816's own scope on a since-corrected belief that no base ammunition catalog existed; **published by CHAR-818**, see `RUN_GUN_AMMO.md`. | `run-gun` AMMO section and Arrowheads sidebar |
| Improvised Melee Weapons | Excluded — a GM-adjudicated narrative-damage rule, not a purchasable catalog product. | `run-gun` Exotic Melee section prose |
| Generated profiles' Availability/Cost | Not modeled as a second purchase; the book prices/licenses these as an accessory or reconfiguration kit on the parent weapon, consistent with treating them as alternate stat blocks rather than independent products. | See individual entries above |
| `harpoon-guns` and `slingshots` use the Archery skill (no distinct core skill exists for them) | Cataloged with the new `weaponCategoryId` values; skill assignment is a frontend/UX concern outside this catalog ledger's scope, matching how `bows`/`crossbows` are already handled. | `run-gun` pp. 22-23 (PDF 24-25) |
| SA Retiarus Net Gun's two SKUs (basic/XL) | Modeled as two separate catalog entries (`sa-retiarus-net-gun-basic`, `sa-retiarus-net-gun-xl`) since they carry distinct stat blocks and costs, matching how other size-variant products (e.g. Cougar Fineblades short/long) are handled. | `run-gun` p. 28 (PDF 30) |
| `ares-screech-sonic-rifle`'s AP value printed as a footnoted `*` rather than a number | Read literally as the string `"*"` and preserved verbatim, following the same "encode complex stat strings as-is" convention used for other dual-value/annotated fields in this catalog (e.g. Gauss Rifle's `"10 (c) + Energy"` ammo). | `run-gun` p. 26 (PDF 28) |

## Review Footer

- Reviewed weapon rules: `run-gun` pp. 18-49 (PDF 20-51).
- Approved-PDF weapon products in scope: 73 base products across 21
  categories (4 newly introduced: `laser-weapons`, `flamethrowers`,
  `harpoon-guns`, `slingshots`), plus 7 alternate-configuration profiles
  called out by name in the base products' own entries.
- Reconciliation: 80 new catalog `weapons` entries (73 base + 7 generated)
  account for all 73 in-scope products with no unexplained inventory
  difference. Combined with `sr5-core`'s existing 77 weapons, the runtime
  catalog now publishes 157 total weapons as of version `sr5-core` 1.6.0.
