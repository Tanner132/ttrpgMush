# Gun H(e)aven 3 Weapons Ledger (CHAR-821)

This is the CHAR-821 source ledger for Gun H(e)aven 3's weapon catalog. It is
a review input for the runtime catalog change it accompanies, not a
substitute for the approved book. It extends
[`WEAPONS_ARMOR.md`](WEAPONS_ARMOR.md) (the `sr5-core` weapon catalog) and
[`RUN_GUN_WEAPONS.md`](RUN_GUN_WEAPONS.md) (the `run-gun` weapon catalog).

CHAR-821 continues the project-owner-approved pass of porting every
character option from the newly-added sourcebooks that fits the existing
catalog shape without introducing a new gameplay mechanic. Gun H(e)aven 3 is
a weapons-only product catalog: 33 firearms, one printed per page, plus two
narrative weapon traits, a new range table, and a flamethrower usage rule.
There is **no separate gear or equipment chapter in this book** — see
Explicit Exclusions below.

## Source

Only `gun-heaven-3` is used, already pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md).

**Page-offset warning.** Unlike every other approved source in this project,
Gun H(e)aven 3 has a **1:1 printed-to-PDF page mapping** — there is no
two-page front-matter offset. This was verified against the page footers on
PDF pages 3, 4, 5, and 20, each of which prints its own PDF page number.
Every citation in this ledger therefore reads `p. N (PDF N)`. Reviewers
carrying over the two-page-offset habit from `run-gun` or `sr5-core` will
mis-locate every entry.

## Scope

Included: all 33 weapon products printed one per page across `gun-heaven-3`
pp. 4-36 (PDF 4-36), published as 33 `Selectable` catalog `weapons` entries.
Full inventory by category is below. No generated alternate-configuration
profiles were required — every product in this book is a flat-stat,
single-configuration weapon.

Excluded (see the Explicit Exclusions And Discrepancies table for the full
reasoning):

- The two new weapon traits, VINTAGE and CAP & BALL (`gun-heaven-3` p. 3).
- The Flamethrower usage rules (`gun-heaven-3` p. 3).
- The SR4A stat blocks printed on p. 38.
- The per-weapon "Standard Upgrades/Accessories" lists, which are recorded
  in the tables below for review but are not modeled as pre-installed
  attachments.

## New Weapon Category

`sporting-rifles` is introduced by this book. Sporting rifles are civilian
hunting and target rifles fired with the **Longarms** skill, using their own
range table printed on `gun-heaven-3` p. 3 (PDF 3): Short 0-50, Medium
51-250, Long 251-500, Extreme 501-750. Mechanically they are ordinary long
guns for accessory-mounting purposes, so `GearAttachmentEvaluator` gives
them the same mount set as `sniper-rifles` and `shotguns` (Top, Barrel,
Underbarrel); the frontend mount map and category description mirror this.
The range table itself is a lookup published for play reference, not a new
mechanic — it slots into the existing per-category range convention.

## Cost-Modeling Convention

Every entry publishes a fixed `Cost` and `Availability` exactly as printed,
following the same modeling convention as the `sr5-core` and `run-gun`
weapon catalogs. `AP` values printed as a dash are stored as the string
`"--"`; an `RC` printed as a dash is omitted from the entry entirely, both
matching the existing catalog convention. Ammo strings are preserved
verbatim, including the dual-feed and multi-clip forms.

## New Weapons By Category

### hold-outs

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `colt-new-model-revolver` | Colt New Model Revolver | 6 | 5P | -- | SA | -- | 5 (cy) | 4R / Restricted | 180¥ | none | `gun-heaven-3` p. 4 (PDF 4) |

### light-pistols

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `colt-agent-special` | Colt Agent Special | 5 | 8P | -- | SA | -- | 8 (c) | 5R / Restricted | 250¥ | none | `gun-heaven-3` p. 5 (PDF 5) |

### heavy-pistols

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `colt-future-frontier` | Colt Future Frontier | 5 | 8P | -1 | SS | -- | 7 (cy) | 6R / Restricted | 500¥ | Melee Hardening | `gun-heaven-3` p. 6 (PDF 6) |

### machine-pistols

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `fianchetti-military-100` | Fianchetti Military 100 | 5 (7) | 6P | -- | SA/BF/FA | -- | 20 (c) | 8R / Restricted | 850¥ | Smartlink | `gun-heaven-3` p. 7 (PDF 7) |
| `cavalier-evanator` | Cavalier Evanator | 5 (6) | 6P | -- | BF/FA | 1 (2) | 20 (c) | 8R / Restricted | 775¥ | Electronic Firing, Laser Sight, Folding Stock | `gun-heaven-3` p. 8 (PDF 8) |
| `remington-suppressor` | Remington Suppressor | 6 | 7P | -1 | SA/BF | -- | 15 (c) | 6R / Restricted | 700¥ | Sound Suppressor | `gun-heaven-3` p. 9 (PDF 9) |

### submachine-guns

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `krime-spree` | Krime Spree | 4 | 7P | -- | FA | 1 | 30 (c) | 6R / Restricted | 425¥ | Metahuman Adaptation | `gun-heaven-3` p. 10 (PDF 10) |
| `ares-sigma-3` | Ares Sigma-3 | 4 (6) | 8P | -- | SA/BF/FA | (2) | 50 (d) | 7R / Restricted | 1,000¥ | Collapsible Stock, Foregrip, Powered Slide Mount (Rating 2), Smartlink | `gun-heaven-3` p. 11 (PDF 11) |
| `cavalier-arms-gladius` | Cavalier Arms Gladius | 3 (4) | 7P | -- | BF/FA | 1 (2) | 32 (c) | 6R / Restricted | 400¥ | Collapsible Stock, Laser Sight | `gun-heaven-3` p. 12 (PDF 12) |

### assault-rifles

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `shiawase-arms-monsoon` | Shiawase Arms Monsoon | 5 | 10P | -1 | SA/FA | 1 | 20 (ml) x6 | 10F / Forbidden | 1,900¥ | Smartlink, Electronic Firing, Melee Hardening | `gun-heaven-3` p. 13 (PDF 13) |
| `colt-inception` | Colt Inception | 7 (8) | 10P | -1 | SA/BF | 1 (3) | 35 (c) | 11R / Restricted | 2,250¥ | Bipod, Electronic Firing, Laser Sight, Melee Hardening | `gun-heaven-3` p. 14 (PDF 14) |
| `krupp-arms-kriegfaust` | Krupp Arms Kriegfaust | 8 | 9P | -1 | SA/BF | 1 | 25 (d) | 10R / Restricted | 1,300¥ | Metahuman Customization, Melee Hardening, Imaging Scope (Vision Enhancement 1, Vision Magnification) | `gun-heaven-3` p. 15 (PDF 15) |
| `sbd-44` | SBd-44 | 3 | 10P | -1 | SA/BF/FA | -- | 32 (c) | 4R / Restricted | 500¥ | Vintage | `gun-heaven-3` p. 16 (PDF 16) |
| `ultimax-rain-forest-carbine` | Ultimax Rain Forest Carbine | 7 | 14P | -4 | SA | (1) | 18 (c) | 5R / Restricted | 2,800¥ | Imaging Scope (Flare Compensation, Image Link, Low-Light Vision), Retractable Stock | `gun-heaven-3` p. 32 (PDF 32) |

### shotguns

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `krime-boss` | Krime Boss | 3 | 13P | -1 | SA | 1 | 15 (d) | 11R / Restricted | 600¥ | Metahuman Customization | `gun-heaven-3` p. 17 (PDF 17) |
| `winchester-model-201` | Winchester Model 201 | 8 | 11P | -1 | SA | -- | 2 (b) | 8R / Restricted | 1,300¥ | none | `gun-heaven-3` p. 18 (PDF 18) |
| `winchester-model-2066` | Winchester Model 2066 | 4 | 11P | -1 | SS | -- | 5 (m) | 4R / Restricted | 1,000¥ | none | `gun-heaven-3` p. 19 (PDF 19) |
| `winchester-model-2054` | Winchester Model 2054 | 4 (5) | 11P | -1 | SA | (1) | 7 (m) | 6R / Restricted | 900¥ | Laser Sight, Retractable Stock | `gun-heaven-3` p. 20 (PDF 20) |
| `shiawase-arms-rain` | Shiawase Arms Rain | 4 | 10P | -1 | SA | (1) | 5 (ml) | 4R / Restricted | 450¥ | Retractable Stock | `gun-heaven-3` p. 21 (PDF 21) |
| `cavalier-falchion` | Cavalier Falchion | 5 (7) | 12P | -1 | SS | -- | 8 (m) | 9R / Restricted | 1,200¥ | Advanced Safety, Melee Hardening, Smartlink, Trigger Removal | `gun-heaven-3` p. 22 (PDF 22) |

### sporting-rifles (new category — Longarms skill)

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `springfield-2003` | Springfield 2003 | 9 | 12P | -2 | SS | -- | 5 (m) | 4R / Restricted | 3,600¥ | Vintage | `gun-heaven-3` p. 23 (PDF 23) |
| `winchester-model-2024` | Winchester Model 2024 | 6 | 12P | -- | SA | -- | 7 (m) | 4R / Restricted | 1,800¥ | Imaging Scope (Vision Magnification) | `gun-heaven-3` p. 24 (PDF 24) |
| `marlin-3468ss` | Marlin 3468SS | 4 | 13P | -1 | SS | -- | 4 (m) | 6R / Restricted | 1,000¥ | none | `gun-heaven-3` p. 25 (PDF 25) |
| `springfield-m1a` | Springfield M1A | 6 | 12P | -1 | SA | -- | 20 (c) | 6R / Restricted | 1,700¥ | Imaging Sight (Image Link, Vision Magnification, Vision Enhancement 1) | `gun-heaven-3` p. 26 (PDF 26) |
| `m1-garand` | M1 Garand | 5 | 12P | -1 | SA | -- | 8 (c) | 3R / Restricted | 1,100¥ | Vintage | `gun-heaven-3` p. 27 (PDF 27) |
| `springfield-model-1855-reproduction` | Springfield Model 1855 Reproduction | 2 | 10P | -- | SS | -- | 1 (cb) | 4R / Restricted | 850¥ | Vintage, Cap & Ball | `gun-heaven-3` p. 28 (PDF 28) |
| `marlin-3041-bl` | Marlin 3041 BL | 5 | 10P | -3 | SA | -- | 6 (m) | 5R / Restricted | 1,100¥ | Imaging Sight (Vision Magnification) | `gun-heaven-3` p. 29 (PDF 29) |
| `marlin-x71` | Marlin X71 | 5 | 12P | -4 | SS | -- | 5 (m) | 6R / Restricted | 1,500¥ | Extreme Environment Modification Level 1 (Arctic), Imaging Sight (Low-Light Vision, Vision Enhancement 2, Vision Magnification) | `gun-heaven-3` p. 30 (PDF 30) |
| `marlin-79s` | Marlin 79S | 4 | 6P | -- | SA | -- | 10 (c) | 3R / Restricted | 300¥ | none | `gun-heaven-3` p. 31 (PDF 31) |
| `winchester-model-2067` | Winchester Model 2067 | 5 | 8P | -1 | SA | -- | 15 (m) | 4R / Restricted | 650¥ | Vintage | `gun-heaven-3` p. 33 (PDF 33) |

### machine-guns

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `krime-wave` | Krime Wave | 5 | 10P | -2 | FA | (2) | 50 (c) or 100 (belt) | 11F / Forbidden | 2,000¥ | Bipod, Metahuman Customization | `gun-heaven-3` p. 34 (PDF 34) |

### cannons-launchers

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `krime-bomb` | Krime Bomb | 6 (7) | 16P | -6 | SS | -- | 4 (m) | 20F / Forbidden | 23,000¥ | Laser Sight, Powered Slide Mount (Rating 2) | `gun-heaven-3` p. 35 (PDF 35) |

### flamethrowers

| ID | Display name | Acc | Damage | AP | Mode | RC | Ammo | Availability / legality | Cost | Standard upgrades | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `shiawase-arms-incinerator` | Shiawase Arms Incinerator | 4 | 12P | -6 | SS | -- | 6 (c) | 12F / Forbidden | 10,000¥ | Powered Slide Mount (Rating 1), Reduced Weight | `gun-heaven-3` p. 36 (PDF 36) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| **No gear/equipment chapter exists in this book** | Gun H(e)aven 3 is a weapons-only product catalog. It contains no armor, electronics, survival gear, biotech, vehicle, or general-equipment section, so nothing outside `weapons` was available to port. This is a property of the source, not a scoping decision. | `gun-heaven-3` pp. 1-38 (whole book) |
| VINTAGE weapon trait | Excluded as a narrative/GM-procedural descriptor — it flags a weapon as a collectible antique affecting availability haggling and repair sourcing at the table, and adds no character-creation stat. Recorded per weapon in the "Standard upgrades" column above for review. | `gun-heaven-3` p. 3 (PDF 3) |
| CAP & BALL weapon trait | Excluded for the same reason — it describes the three-Complex-Action black-powder reload procedure in play, not a creation-time selection. Its one carrier's ammo code `1 (cb)` is preserved verbatim in the catalog so the trait remains visible on the sheet. | `gun-heaven-3` p. 3 (PDF 3) |
| Flamethrower usage rules | Excluded as in-play combat procedure (fuel burn, fire damage, and ignition adjudication), consistent with how this project treats other test procedures. The Shiawase Arms Incinerator itself is cataloged. | `gun-heaven-3` p. 3 (PDF 3) |
| Sporting Rifles range table | Not stored as a catalog row; published instead as the new `sporting-rifles` category with its range values recorded in this ledger (Short 0-50, Medium 51-250, Long 251-500, Extreme 501-750, Longarms skill). Range tables are per-category lookups in this project, not per-weapon catalog fields. | `gun-heaven-3` p. 3 (PDF 3) |
| Per-weapon "Standard Upgrades/Accessories" lists | Recorded in the tables above for reviewer traceability but **not** modeled as pre-installed attachments. The catalog has no schema for shipping a weapon with accessories already mounted and their cost pre-absorbed; inventing one would be a new mechanic. Every accessory named is already purchasable from the `run-gun` accessory catalog (CHAR-817). | `gun-heaven-3` pp. 4-36 (PDF 4-36) |
| SR4A stat blocks (p. 38) | Ignored entirely. This project targets Shadowrun Fifth Edition; the book's Fourth Edition Anniversary conversion table is out of scope. | `gun-heaven-3` p. 38 (PDF 38) |
| Consolidated SR5 stats table (p. 37) | Used only as a cross-check, never as the authoritative source. The table's weapon-name column is misaligned against its stat rows at the head of the table (aligning again at the tail), so the per-weapon "SHADOWRUN, FIFTH EDITION" stat blocks on pp. 4-36 were treated as authoritative throughout. | `gun-heaven-3` p. 37 (PDF 37) |
| **Discrepancy** — Krime Wave ammo | The p. 37 table prints `(D)` (drum); the weapon's own p. 34 stat block and the p. 38 SR4A block both print `50 (c) or 100 (belt)`. Resolved in favor of the weapon page, which is also the internally consistent reading (the p. 34 prose describes both a box magazine and a belt feed). Cataloged as `"50 (c) or 100 (belt)"`. | `gun-heaven-3` p. 34 (PDF 34) vs. p. 37 (PDF 37) |
| **Discrepancy** — Ultimax Rain Forest Carbine RC | The p. 37 table prints RC `1`; the weapon's own p. 32 stat block prints `(1)` (parenthesized, i.e. recoil compensation supplied by an accessory rather than the weapon itself). Resolved in favor of the weapon page. Cataloged as `"(1)"`. | `gun-heaven-3` p. 32 (PDF 32) vs. p. 37 (PDF 37) |
| Colt Agent Special's borrowed range/ammo rules | The book notes this weapon uses the taser range table and heavy-pistol ammunition despite being a light pistol. Cataloged under `light-pistols` with its printed stat block as-is; the cross-referenced range/ammunition behavior is a play-time lookup, not a creation-time field, and modeling it would require a per-weapon range-table override the schema does not have. | `gun-heaven-3` p. 5 (PDF 5) |
| Shiawase Arms Monsoon ammo `20 (ml) x6` | Preserved verbatim rather than normalized to a single number, following the same "encode complex stat strings as-is" convention used elsewhere in this catalog (e.g. the Gauss Rifle's `"10 (c) + Energy"`). | `gun-heaven-3` p. 13 (PDF 13) |

## Review Footer

- Reviewed weapon rules: `gun-heaven-3` pp. 3-37 (PDF 3-37); p. 38 (SR4A)
  excluded by scope.
- Approved-PDF weapon products in scope: 33 base products across 11
  categories, one product printed per page on pp. 4-36 with no page reused
  and no page in that range carrying anything other than a weapon.
- Reconciliation: 33 new catalog `weapons` entries account for all 33
  in-scope products with no unexplained inventory difference. One new
  category (`sporting-rifles`, 10 entries) was introduced. Combined with
  `sr5-core`'s 77 weapons and `run-gun`'s 80, the runtime catalog now
  publishes **190 total weapons**.
- Non-weapon content in scope: none. The book has no gear, armor,
  electronics, or vehicle sections.
