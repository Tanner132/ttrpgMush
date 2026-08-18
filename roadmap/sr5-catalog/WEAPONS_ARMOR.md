# SR5 Weapons And Armor Ledger

This file is the CHAR-801 row-level review of the core weapons, weapon
accessories, ammunition, grenades, rockets, missiles, explosives, clothing,
armor, shields, helmets, and armor modifications. It is a review input for
CHAR-802, not a runtime catalog. Only the pinned `sr5-core` PDF and approved
decisions in [`../SR5_RULE_DECISIONS.md`](../SR5_RULE_DECISIONS.md) were used.

## Field Conventions And Global Rules

- `none` means the source explicitly prints no value, usually an em dash.
  `not applicable` means the concept does not apply. `1 item` means one weapon,
  accessory, garment, or explosive device unless another unit is printed.
- Creation permits only numeric Availability 12 or lower. An `R` item requires
  an appropriate license; an `F` item cannot be licensed but is not excluded
  solely by that suffix. Purchasable Rating and Force are limited to 6; Capacity
  and quantities are not ratings. Sources: `sr5-core` pp. 94, 418-419 (PDF 96,
  420-421). Decisions: `gear.legality-at-creation`, `gear.rating-cap-force`.
- Listed costs are base costs. Dwarfs pay +10 percent and trolls +50 percent for
  gear. Sources: `sr5-core` pp. 94, 420 (PDF 96, 422). Decisions:
  `metatype.dwarf-costs`, `metatype.troll-costs`.
- Unless a rule says otherwise, division rounds up. This applies to expressions
  such as bow AP and quantities readied; explosives expressly override it where
  they say to round down. Source: `sr5-core` p. 48 (PDF 50).
- Firearm Ammo codes are break action `(b)`, detachable clip `(c)`, drum `(d)`,
  muzzle-loader `(ml)`, internal magazine `(m)`, cylinder `(cy)`, and belt-fed
  `(belt)`. Parenthesized Accuracy or RC is the value with all integral equipment
  deployed. `P`, `S`, `(e)`, and `(f)` mean Physical, Stun, electrical, and
  flechette damage. Source: `sr5-core` p. 417 (PDF 419).
- Integral firearm accessories do not consume mounts. Only one non-integral
  accessory may occupy each top, barrel, or underbarrel mount. Hold-outs have no
  mounts; pistols, machine pistols, and SMGs have top and barrel only; rifles and
  heavy weapons normally have all three. Projectile weapons accept only
  accessories designed for them. More-specific restrictions below override the
  general rule. Source: `sr5-core` p. 417 (PDF 419).
- `Eligible` means purchasable during normal creation subject to funds, licenses,
  host compatibility, and GM approval. `Unavailable` states the exact blocking
  fact. Included components are part of the parent price and are not separately
  purchased. Generated profiles are alternate uses of a purchased parent and
  never duplicate inventory.

## Melee Weapons

All rows have quantity `1 weapon`, no Rating, no Capacity, and no accessory
mounts. Accuracy `Physical` uses the wielder's Physical limit. Reach printed as
an em dash is `none`.

| ID | Exact display name | Classification / type | Accuracy | Reach | Damage | AP | Availability / legality | Cost | Skill or restriction; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `combat-axe` | Combat axe | selectable / blade | 4 | 2 | (STR + 5)P | -4 | 12R / Restricted | 4,000¥ | Blades; two-handed; Eligible with license. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `combat-knife` | Combat knife | selectable / blade | 6 | none | (STR + 2)P | -3 | 4 / Legal | 300¥ | Blades; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `forearm-snap-blades` | Forearm snap-blades | selectable / blade | 4 | none | (STR + 2)P | -2 | 7R / Restricted | 200¥ | Blades; external forearm sheath; Eligible with license. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `katana` | Katana | selectable / blade | 7 | 1 | (STR + 3)P | -3 | 9R / Restricted | 1,000¥ | Blades; two-handed; Eligible with license. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `knife` | Knife | selectable / blade | 5 | none | (STR + 1)P | -1 | none / Legal | 10¥ | Blades; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `pole-arm` | Pole arm | selectable / blade | 5 | 3 | (STR + 3)P | -2 | 6R / Restricted | 1,000¥ | Blades; Eligible with license. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `survival-knife` | Survival knife | selectable / blade | 5 | none | (STR + 2)P | -1 | none / Legal | 100¥ | Blades; includes GPS monitor, mini-multitool, micro-lighter, hidden compartment, and two-hour phosphorescent blade coating; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `sword` | Sword | selectable / blade | 6 | 1 | (STR + 3)P | -2 | 5R / Restricted | 500¥ | Blades; one-handed style is descriptive, not a separate product; Eligible with license. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `club` | Club | selectable / club | 4 | 1 | (STR + 3)P | none | none / Legal | 30¥ | Clubs; printed examples are not separate catalog products; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `extendable-baton` | Extendable baton | selectable / club | 5 | 1 | (STR + 2)P | none | 4 / Legal | 100¥ | Clubs; collapsed/extended concealability 0/+2; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `sap` | Sap | selectable / club | 5 | none | (STR + 2)P | none | 2 / Legal | 30¥ | Clubs; concealability -2; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `staff` | Staff | selectable / club | 6 | 2 | (STR + 3)P | none | 3 / Legal | 100¥ | Clubs; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `stun-baton` | Stun baton | selectable / club | 4 | 1 | 9S(e) | -5 | 6R / Restricted | 750¥ | Clubs; 10 charges; wired recharge 1/10 seconds; Eligible with license. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `telescoping-staff` | Telescoping staff | selectable / club | 4 | 2 | (STR + 2)P | none | 4 / Legal | 350¥ | Clubs; collapsible; Eligible. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `knucks` | Knucks | selectable / other melee | Physical | none | (STR + 1)P | none | 2R / Restricted | 100¥ | Unarmed Combat; Eligible with license. | `sr5-core` pp. 422-423 (PDF 424-425) |
| `monofilament-whip` | Monofilament whip | selectable / exotic melee | 5 (7 wireless) | 2 | 12P | -8 | 12F / Forbidden | 10,000¥ | Exotic Melee Weapon (Monofilament Whip); Eligible, not licensable. | `sr5-core` pp. 423-425 (PDF 425-427) |
| `shock-gloves` | Shock gloves | selectable / other melee | Physical | none | 8S(e) | -5 | 6R / Restricted | 550¥ | Unarmed Combat; 10 charges; wired recharge 1/10 seconds; Eligible with license. | `sr5-core` pp. 423-425 (PDF 425-427) |

Wireless component facts: forearm snap-blades, the extendable baton, and the
telescoping staff ready as a Free Action; the survival knife supplies maps, GPS,
and commcalls; the monofilament whip readies as a Free Action, retracts on a
glitch, and raises Accuracy to 7; stun baton and shock gloves inductively regain
one charge per hour. These are parent capabilities, not accessories or purchases.
Source: `sr5-core` pp. 422-423 (PDF 424-425).

## Projectile And Throwing Weapons

| ID | Exact display name | Classification / type | Full table stats | Availability / legality | Cost / quantity | Rating, host, and creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `bow` | Bow | parameterized / bow | Accuracy 6; DV (Rating + 2)P; AP -(Rating / 4); Ammo 1 arrow | Rating / Legal | Rating x 100¥ / 1 weapon | Rating 1-10 printed maximum; creation 1-6. Minimum STR equals Rating; use lowest STR, bow Rating, or arrow Rating for range/DV. | `sr5-core` pp. 423-424 (PDF 425-426) |
| `arrow` | Arrow | parameterized / bow ammunition | Accuracy, DV, AP not applicable independently | Rating / Legal | Rating x 2¥ / 1 arrow | Rating 1-10; creation Rating 1-6; Bow host; lower arrow Rating caps bow range/DV. Decision `gear.arrow-rating-range`. | `sr5-core` pp. 423-424 (PDF 425-426) |
| `injection-arrow` | Injection arrow | parameterized / bow ammunition | Accuracy, DV, AP not applicable independently | (Rating + 2)R / Restricted | Rating x 20¥ / 1 arrow | Rating 1-10; creation Rating 1-6; Bow host; one separately purchased drug/toxin dose; must deal at least 1 damage after resistance to inject. Eligible with license. Decision `gear.arrow-rating-range`. | `sr5-core` pp. 423-424 (PDF 425-426) |
| `light-crossbow` | Light | selectable / crossbow | Accuracy 7; DV 5P; AP -1; Ammo 4 (m) | 2 / Legal | 300¥ / 1 weapon | Archery; internal 4-bolt auto-loader; Eligible. | `sr5-core` p. 424 (PDF 426) |
| `medium-crossbow` | Medium | selectable / crossbow | Accuracy 6; DV 7P; AP -2; Ammo 4 (m) | 4R / Restricted | 500¥ / 1 weapon | Archery; internal 4-bolt auto-loader; Eligible with license. | `sr5-core` p. 424 (PDF 426) |
| `heavy-crossbow` | Heavy | selectable / crossbow | Accuracy 5; DV 10P; AP -3; Ammo 4 (m) | 8R / Restricted | 1,000¥ / 1 weapon | Archery; internal 4-bolt auto-loader; Eligible with license. | `sr5-core` p. 424 (PDF 426) |
| `bolt` | Bolt | selectable / crossbow ammunition | Accuracy, DV, AP not applicable independently | 2 / Legal | 5¥ / 1 bolt | Crossbow host; Eligible. | `sr5-core` p. 424 (PDF 426) |
| `injection-bolt` | Injection bolt | selectable / crossbow ammunition | Accuracy, DV, AP not applicable independently | 8R / Restricted | 50¥ / 1 bolt | Crossbow host; one separately purchased drug/toxin dose; must deal at least 1 damage after resistance to inject. Eligible with license. | `sr5-core` p. 424 (PDF 426) |
| `throwing-knife-shuriken` | Throwing knife/shuriken | selectable / throwing weapon | Accuracy Physical; DV (STR + 1)P; AP -1 | 4R / Restricted | 25¥ / 1 weapon | Throwing Weapons; Ready Weapon readies Agility / 2; Eligible with license. | `sr5-core` p. 424 (PDF 426) |

## Firearms, Special Weapons, Heavy Weapons, And Launchers

Every firearm row has quantity `1 weapon`, no Rating, and no Capacity unless the
restriction column says otherwise. All firearms include wireless capability and
a digital ammunition counter. Ammunition is purchased separately. Source:
`sr5-core` p. 424 (PDF 426).

| ID | Exact display name | Classification / category | Acc; DV; AP; Mode; RC; Ammo | Availability / legality | Cost | Mounts; integral components; generated profile; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `defiance-ex-shocker` | Defiance EX Shocker | selectable / taser | 4; 9S(e); -5; SS; none; 4 (m) | none / Legal | 250¥ | Top only; generated melee profile; Eligible. Decision `gear.defiance-ex-shocker-cost`. | `sr5-core` pp. 424-425 (PDF 426-427) |
| `yamaha-pulsar` | Yamaha Pulsar | selectable / taser | 5; 7S(e); -5; SA; none; 4 (m) | none / Legal | 180¥ | Top only; wireless darts; Eligible. | `sr5-core` pp. 424-425 (PDF 426-427) |
| `fichetti-tiffani-needler` | Fichetti Tiffani Needler | selectable / hold-out | 5; 8P(f); +5; SA; none; 4 (c) | 5R / Restricted | 1,000¥ | No mounts; flechette only; Eligible with license. | `sr5-core` p. 425 (PDF 427) |
| `streetline-special` | Streetline Special | selectable / hold-out | 4; 6P; none; SA; none; 6 (c) | 4R / Restricted | 120¥ | No mounts; MAD detection -2 dice; Eligible with license. | `sr5-core` p. 425 (PDF 427) |
| `walther-palm-pistol` | Walther Palm Pistol | selectable / hold-out | 4; 7P; none; SS/BF; none; 2 (b) | 4R / Restricted | 180¥ | No mounts; two barrels may fire together as short burst; Eligible with license. | `sr5-core` p. 425 (PDF 427) |
| `ares-light-fire-75` | Ares Light Fire 75 | selectable / light pistol | 6 (8); 6P; none; SA; none; 16 (c) | 6F / Forbidden | 1,250¥ | Top + barrel; integral smartgun and special silencer (-5 Perception total); Eligible, not licensable. | `sr5-core` pp. 425-426 (PDF 427-428) |
| `ares-light-fire-70` | Ares Light Fire 70 | selectable / light pistol | 7; 6P; none; SA; none; 16 (c) | 3R / Restricted | 200¥ | Top + barrel; accepts host-specific silencer below; Eligible with license. | `sr5-core` pp. 425-426 (PDF 427-428) |
| `beretta-201t` | Beretta 201T | selectable / light pistol | 6; 6P; none; SA/BF; (1); 21 (c) | 7R / Restricted | 210¥ | Top + barrel; integral detachable shoulder stock; BF requires Complex Action; Eligible with license. | `sr5-core` pp. 425-426 (PDF 427-428) |
| `colt-america-l36` | Colt America L36 | selectable / light pistol | 7; 7P; none; SA; none; 11 (c) | 4R / Restricted | 320¥ | Top + barrel; Eligible with license. | `sr5-core` pp. 425-426 (PDF 427-428) |
| `fichetti-security-600` | Fichetti Security 600 | selectable / light pistol | 6 (7); 7P; none; SA; (1); 30 (c) | 6R / Restricted | 350¥ | Top + barrel; integral detachable folding stock and laser sight; Eligible with license. | `sr5-core` pp. 426-428 (PDF 428-430) |
| `taurus-omni-6` | Taurus Omni-6 | selectable / light pistol | 5 (6); 6P/7P; 0/-1; SA/SS; none; 6 (cy) | 3R / Restricted | 300¥ | Top + barrel; integral laser sight and interchangeable cylinders; generated heavy-pistol-ammo profile; Eligible with license. | `sr5-core` pp. 426-428 (PDF 428-430) |
| `ares-predator-v` | Ares Predator V | selectable / heavy pistol | 5 (7); 8P; -1; SA; none; 15 (c) | 5R / Restricted | 725¥ | Top + barrel; integral smartgun; Eligible with license. | `sr5-core` pp. 426-428 (PDF 428-430) |
| `ares-viper-slivergun` | Ares Viper Slivergun | selectable / heavy pistol | 4; 9P(f); +4; SA/BF; none; 30 (c) | 8F / Forbidden | 380¥ | Top + barrel; integral silencer; metal-sliver/flechette ammunition only; Eligible, not licensable. | `sr5-core` pp. 426-428 (PDF 428-430) |
| `browning-ultra-power` | Browning Ultra-Power | selectable / heavy pistol | 5 (6); 8P; -1; SA; none; 10 (c) | 4R / Restricted | 640¥ | Top + barrel; integral top laser sight; Eligible with license. | `sr5-core` pp. 426-428 (PDF 428-430) |
| `colt-government-2066` | Colt Government 2066 | selectable / heavy pistol | 6; 7P; -1; SA; none; 14 (c) | 7R / Restricted | 425¥ | Top + barrel; Eligible with license. | `sr5-core` pp. 426-428 (PDF 428-430) |
| `remington-roomsweeper` | Remington Roomsweeper | selectable / heavy pistol | 4; 7P; -1; SA; none; 8 (m) | 6R / Restricted | 250¥ | Top + barrel; generated flechette profile uses heavy-pistol ranges and shotgun rules; Eligible with license. | `sr5-core` pp. 426-429 (PDF 428-431) |
| `ruger-super-warhawk` | Ruger Super Warhawk | selectable / heavy pistol | 5; 9P; -2; SS; none; 6 (cy) | 4R / Restricted | 400¥ | Top + barrel; Eligible with license. | `sr5-core` pp. 426-429 (PDF 428-431) |
| `ares-crusader-ii` | Ares Crusader II | selectable / machine pistol | 5 (7); 7P; none; SA/BF; 2; 40 (c) | 9R / Restricted | 830¥ | Top + barrel; integral gas-vent 2 and smartgun; Eligible with license. | `sr5-core` pp. 427-429 (PDF 429-431) |
| `ceska-black-scorpion` | Ceska Black Scorpion | selectable / machine pistol | 5; 6P; none; SA/BF; (1); 35 (c) | 6R / Restricted | 270¥ | Top + barrel; integral folding stock; Eligible with license. | `sr5-core` pp. 427-429 (PDF 429-431) |
| `steyr-tmp` | Steyr TMP | selectable / machine pistol | 4; 7P; none; SA/BF/FA; none; 30 (c) | 8R / Restricted | 350¥ | Top + barrel; integral top laser sight; Eligible with license. | `sr5-core` pp. 427-429 (PDF 429-431) |
| `colt-cobra-tz-120` | Colt Cobra TZ-120 | selectable / SMG | 4 (5); 7P; none; SA/BF/FA; 2 (3); 32 (c) | 5R / Restricted | 660¥ | Top + barrel; integral folding stock, top laser sight, gas-vent 2; Eligible with license. | `sr5-core` pp. 427-430 (PDF 429-432) |
| `fn-p93-praetor` | FN P93 Praetor | selectable / SMG | 6; 8P; none; SA/BF/FA; 1 (2); 50 (c) | 11F / Forbidden | 900¥ | Top + barrel; integral chamber RC 1, rigid stock, and flashlight reducing darkness one step; Eligible, not licensable. | `sr5-core` pp. 427-430 (PDF 429-432) |
| `hk-227` | HK-227 | selectable / SMG | 5 (7); 7P; none; SA/BF/FA; (1); 28 (c) | 8R / Restricted | 730¥ | Top + barrel; integral retractable stock, smartgun, sound suppressor; Eligible with license. | `sr5-core` pp. 427-430 (PDF 429-432) |
| `ingram-smartgun-x` | Ingram Smartgun X | selectable / SMG | 4 (6); 8P; none; BF/FA; 2; 32 (c) | 6R / Restricted | 800¥ | Top + barrel; integral gas-vent 2, smartgun, sound suppressor; Eligible with license. | `sr5-core` pp. 427-430 (PDF 429-432) |
| `sck-model-100` | SCK Model 100 | selectable / SMG | 5 (7); 8P; none; SA/BF; (1); 30 (c) | 6R / Restricted | 875¥ | Top + barrel; integral smartgun and folding stock; Eligible with license. | `sr5-core` pp. 427-430 (PDF 429-432) |
| `uzi-iv` | Uzi IV | selectable / SMG | 4 (5); 7P; none; BF; (1); 24 (c) | 4R / Restricted | 450¥ | Top + barrel; integral folding stock and top laser sight; Eligible with license. | `sr5-core` pp. 427-430 (PDF 429-432) |
| `ak-97` | AK-97 | selectable / assault rifle | 5; 10P; -2; SA/BF/FA; none; 38 (c) | 4R / Restricted | 950¥ | Top + barrel + under; Eligible with license. | `sr5-core` pp. 428-430 (PDF 430-432) |
| `ares-alpha` | Ares Alpha | selectable / assault rifle | 5 (7); 11P; -2; SA/BF/FA; 2; 42 (c) | 11F / Forbidden | 2,650¥ | All mounts; integral smartgun, chamber RC 2, and generated underbarrel grenade-launcher profile; Eligible, not licensable. | `sr5-core` pp. 428-430 (PDF 430-432) |
| `colt-m23` | Colt M23 | selectable / assault rifle | 4; 9P; -2; SA/BF/FA; none; 40 (c) | 4R / Restricted | 550¥ | All mounts; Eligible with license. | `sr5-core` pp. 428-430 (PDF 430-432) |
| `fn-har` | FN HAR | selectable / assault rifle | 5 (6); 10P; -2; SA/BF/FA; 2; 35 (c) | 8R / Restricted | 1,500¥ | All mounts; integral laser sight and gas-vent 2; Eligible with license. | `sr5-core` pp. 428-430 (PDF 430-432) |
| `yamaha-raiden` | Yamaha Raiden | creation-unavailable / assault rifle | 6 (8); 11P; -2; BF/FA; 1; 60 (c) | 14F / Forbidden | 2,600¥ | All mounts; integral sound suppressor, smartgun, chamber RC 1; Unavailable: Availability 14. | `sr5-core` pp. 428-430 (PDF 430-432) |
| `ares-desert-strike` | Ares Desert Strike | selectable / sniper rifle | 7; 13P; -4; SA; (1); 14 (c) | 10F / Forbidden | 17,500¥ | All mounts; integral rigid stock with shock pad and detachable imaging scope; Eligible, not licensable. | `sr5-core` pp. 428-431 (PDF 430-433) |
| `cavalier-arms-crockett-ebr` | Cavalier Arms Crockett EBR | selectable / sniper rifle | 6; 12P; -3; SA/BF; (1); 20 (c) | 12F / Forbidden | 10,300¥ | All mounts; integral rigid stock with shock pad and detachable imaging scope; Eligible, not licensable. | `sr5-core` pp. 428-431 (PDF 430-433) |
| `ranger-arms-sm-5` | Ranger Arms SM-5 | creation-unavailable / sniper rifle | 8; 14P; -5; SA; (1); 15 (c) | 16F / Forbidden | 28,000¥ | All mounts; integral silencer, imaging scope, rigid stock with shock pad; Unavailable: Availability 16. | `sr5-core` pp. 428-431 (PDF 430-433) |
| `remington-950` | Remington 950 | selectable / sniper rifle | 7; 12P; -4; SS; none; 5 (m) | 4R / Restricted | 2,100¥ | Top + barrel; explicitly no underbarrel; integral top imaging scope; Eligible with license. | `sr5-core` pp. 428-431 (PDF 430-433) |
| `ruger-101` | Ruger 101 | selectable / sniper/sport rifle | 6; 11P; -3; SA; (1); 8 (m) | 4R / Restricted | 1,300¥ | All mounts; integral imaging scope and rigid stock with shock pad; Eligible with license. Table provenance says `Ruger 100`; decision `gear.ruger-model-name`. | `sr5-core` pp. 428-429 (PDF 430-431) |
| `defiance-t-250` | Defiance T-250 | selectable / shotgun | 4; 10P; -1; SS/SA; none; 5 (m) | 4R / Restricted | 450¥ | All mounts; generated short-barreled profile; Eligible with license. | `sr5-core` p. 429 (PDF 431) |
| `enfield-as-7` | Enfield AS-7 | selectable / shotgun | 4 (5); 13P; -1; SA/BF; none; 10 (c) or 24 (d) | 12F / Forbidden | 1,100¥ | All mounts; integral top laser sight; select clip or drum when loading; Eligible, not licensable. | `sr5-core` p. 429 (PDF 431) |
| `pjss-model-55` | PJSS Model 55 | selectable / shotgun | 6; 11P; -1; SS; (1); 2 (b) | 9R / Restricted | 1,000¥ | All mounts; integral rigid stock with shock pad; both barrels may fire as short burst; Eligible with license. | `sr5-core` p. 429 (PDF 431) |
| `ares-s-iii-super-squirt` | Ares S-III Super Squirt | selectable / special weapon | 3; Chemical; none; SA; none; 20 (c) | 7R / Restricted | 950¥ | Top + under; Exotic Ranged Weapon; Light Pistol ranges; DMSO gel pack requires separately selected chemical and delivers it as Contact vector on a hit; no gel-pack purchase is exposed because its merchandise facts are absent; weapon is Eligible with license. Decision `gear.super-squirt-ammunition`. | `sr5-core` pp. 429-430 (PDF 431-432) |
| `fichetti-pain-inducer` | Fichetti Pain Inducer | selectable / special weapon | 3; Special; none; SS; none; Special | 11R / Restricted | 5,000¥ | Top + under; Exotic Ranged Weapon; SMG ranges; 10 charges; Power 8 Immediate toxin-like Body + Willpower resistance and sustained beam rules; Eligible with license. | `sr5-core` pp. 429-430 (PDF 431-432) |
| `parashield-dart-pistol` | Parashield Dart Pistol | selectable / special weapon | 5; as Drug/Toxin; none; SA; none; 5 (c) | 4R / Restricted | 600¥ | Top only; Exotic Ranged Weapon; Heavy Pistol ranges; injection darts and payload sold separately; Eligible with license. | `sr5-core` pp. 429-430 (PDF 431-432) |
| `parashield-dart-rifle` | Parashield Dart Rifle | selectable / special weapon | 6; as Drug/Toxin; none; SA; none; 6 (m) | 6R / Restricted | 1,200¥ | Top + under; integral top imaging scope; Exotic Ranged Weapon; sporting-rifle ranges; injection darts/payload sold separately; Eligible with license. | `sr5-core` pp. 429-430 (PDF 431-432) |
| `ingram-valiant` | Ingram Valiant | selectable / light machine gun | 5 (6); 9P; -2; BF/FA; 2 (3); 50 (c) or 100 (belt) | 12F / Forbidden | 5,800¥ | All mounts; integral rigid stock with shock pad, laser sight, gas-vent 2; Heavy Weapons; Eligible, not licensable. | `sr5-core` pp. 430-432 (PDF 432-434) |
| `stoner-ares-m202` | Stoner-Ares M202 | selectable / medium machine gun | 5; 10P; -3; FA; none; 50 (c) or 100 (belt) | 12F / Forbidden | 7,000¥ | All mounts; Heavy Weapons; intended mainly as mounted but no minimum STR is imposed; Eligible, not licensable. | `sr5-core` pp. 430-432 (PDF 432-434) |
| `rpk-hmg` | RPK HMG | creation-unavailable / heavy machine gun | 5; 12P; -4; FA; (6); 50 (c) or 100 (belt) | 16F / Forbidden | 16,300¥ | All mounts; integral detachable tripod; Heavy Weapons; Unavailable: Availability 16. | `sr5-core` pp. 430-432 (PDF 432-434) |
| `ares-antioch-2` | Ares Antioch-2 | selectable / grenade launcher | 4 (6); Grenade; none; SS; none; 8 (m) | 8F / Forbidden | 3,200¥ | Top + under; integral smartgun; minigrenades; Heavy Weapons; Eligible, not licensable. | `sr5-core` pp. 431-433 (PDF 433-435) |
| `armtech-mgl-12` | ArmTech MGL-12 | selectable / grenade launcher | 4; Grenade; none; SA; none; 12 (c) | 10F / Forbidden | 5,000¥ | Top + under; minigrenades; Heavy Weapons; Eligible, not licensable. | `sr5-core` pp. 431-433 (PDF 433-435) |
| `aztechnology-striker` | Aztechnology Striker | selectable / disposable rocket/missile launcher | 5; Missile; none; SS; none; 1 (ml) | 10F / Forbidden | 1,200¥ | Top + under; one rocket or missile; Heavy Weapons; Eligible, not licensable. | `sr5-core` pp. 431-433 (PDF 433-435) |
| `krime-cannon` | Krime Cannon | creation-unavailable / assault cannon | 4; 16P; -6; SA; (1); 6 (m) | 20F / Forbidden | 21,000¥ | Top + under; integral troll adaptation; Heavy Weapons; assault-cannon ammo only; Unavailable: Availability 20. | `sr5-core` pp. 431-433 (PDF 433-435) |
| `onotari-interceptor` | Onotari Interceptor | creation-unavailable / missile launcher | 4 (6); Missile; none; SS; none; 2 (ml) | 18F / Forbidden | 14,000¥ | Top + under; integral smartgun; two independently loaded rockets/missiles, no simultaneous double shot; Heavy Weapons; Unavailable: Availability 18. | `sr5-core` pp. 431-433 (PDF 433-435) |
| `panther-xxl` | Panther XXL | creation-unavailable / assault cannon | 5 (7); 17P; -6; SS; none; 15 (c) | 20F / Forbidden | 43,000¥ | Top + under; integral smartgun; Heavy Weapons; assault-cannon ammo only; Unavailable: Availability 20. | `sr5-core` pp. 431-433 (PDF 433-435) |

Machine guns, assault cannons, and launchers double uncompensated-recoil
modifiers. The prose says MMGs and HMGs *could* be carried at STR 8+ and 10+ but
does not make those values hard prerequisites. Source: `sr5-core` p. 430 (PDF
432).

Launched grenades use a 5-meter minimum/arming distance; rockets and missiles
use the more-specific 10-meter distance. Decision
`gear.launcher-arming-distance`.

### Generated Weapon Profiles

These profiles are included in their parent and have no independent cost,
Availability, quantity, Rating, Capacity, or purchase eligibility.

| ID | Exact display name | Classification | Parent | Generated full stats and restriction | Source |
| --- | --- | --- | --- | --- | --- |
| `defiance-ex-shocker/melee` | Defiance EX Shocker (melee) | generated | `defiance-ex-shocker` | Accuracy 3; Reach none; DV 8S(e); AP -5; uses integrated contacts. | `sr5-core` p. 424 (PDF 426) |
| `taurus-omni-6/heavy-pistol-ammo` | Taurus Omni-6 (heavy pistol ammo) | generated | `taurus-omni-6` | Accuracy 5 (6); DV 7P; AP -1; Mode SS; RC none; Ammo 6 (cy). Base profile uses light-pistol ammo, 6P, AP 0, SA. | `sr5-core` p. 426 (PDF 428) |
| `remington-roomsweeper/flechette` | Remington Roomsweeper w/ flechettes | generated | `remington-roomsweeper` | Accuracy 4; DV 9P(f); AP +4; SA; RC none; Ammo 8 (m); Heavy Pistol range with shotgun rules. | `sr5-core` pp. 426-429 (PDF 428-431) |
| `defiance-t-250/short-barreled` | Defiance T-250 (short-barreled) | generated | `defiance-t-250` | Accuracy 4; DV 9P; AP -1; SS/SA; RC none; Ammo 5 (m); Heavy Pistol range; Concealability +4. | `sr5-core` p. 429 (PDF 431) |
| `ares-alpha/grenade-launcher` | Grenade Launcher | generated | `ares-alpha` | Accuracy 4 (6); DV/AP from loaded minigrenade; Mode SS; RC none; Ammo 6 (c); integral underbarrel launcher. | `sr5-core` pp. 428-430 (PDF 430-432) |
| `ballistic-shield/melee` | Ballistic shield (melee) | generated | `ballistic-shield` | Exotic Melee Weapon; Accuracy 4; Reach none; DV (STR + 2)S; AP none. | `sr5-core` p. 438 (PDF 440) |
| `riot-shield/melee` | Riot shield (melee) | generated | `riot-shield` | Exotic Melee Weapon; Accuracy 4; Reach none; DV 9S(e); AP -5; uses shield charge. | `sr5-core` p. 438 (PDF 440) |

## Firearm Accessories

Mount `none` means the table prints an em dash or the item is worn rather than
mounted. Each row has quantity `1 accessory`; no Capacity applies except the
printed imaging/smartgun camera capacities.

| ID | Exact display name | Classification | Mount | Availability / legality | Cost | Rating, host/capacity restriction, included effect, and creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `airburst-link` | Airburst link | selectable | none | 6R / Restricted | 600¥ | Grenade/rocket launcher smartgun accessory; launcher and projectile must both be wireless; launched-grenade scatter reduction becomes 2m/net hit. Eligible with license. | `sr5-core` pp. 431-432 (PDF 433-434) |
| `bipod` | Bipod | selectable | Under | 2 / Legal | 200¥ | Weapon with underbarrel mount; deployed while prone/sitting gives RC 2. Eligible. | `sr5-core` pp. 431-432 (PDF 433-434) |
| `concealable-holster` | Concealable holster | selectable | none | 2 / Legal | 150¥ | Pistols/tasers only; Concealability -1, or another -1 wireless. Eligible. | `sr5-core` pp. 431-432 (PDF 433-434) |
| `gas-vent-system` | Gas-vent system (Rating 1-3) | parameterized | Barrel | (Rating x 3)R / Restricted | Rating x 200¥ | Rating 1-3; permanent after installation; RC equals Rating. Eligible with license. | `sr5-core` pp. 431-432 (PDF 433-434) |
| `gyro-mount` | Gyro mount | selectable | Under | 7 / Legal | 1,400¥ | Assault rifle or heavy weapon; worn harness neutralizes up to 6 recoil and movement modifiers. Eligible. | `sr5-core` p. 432 (PDF 434) |
| `hidden-arm-slide` | Hidden arm slide | selectable | none | 4R / Restricted | 350¥ | Hold-out, light pistol, or taser only; quick-draw threshold 2 and Concealability -1. Eligible with license. | `sr5-core` p. 432 (PDF 434) |
| `imaging-scope` | Imaging scope | selectable | Top | 2 / Legal | 300¥ | Capacity 3; includes micro camera and vision magnification; accepts vision enhancements. Eligible. | `sr5-core` p. 432 (PDF 434) |
| `laser-sight` | Laser sight | selectable | Top or Under | 2 / Legal | 125¥ | Accuracy +1 and wireless attack +1; neither stacks with smartlink. Eligible. | `sr5-core` p. 432 (PDF 434) |
| `periscope` | Periscope | selectable | Top | 3 / Legal | 70¥ | Shooting-around-corner penalty becomes -2; accepts vision enhancements; wireless required for printed penalty benefit. Eligible. | `sr5-core` p. 432 (PDF 434) |
| `quick-draw-holster` | Quick-draw holster | selectable | none | 4 / Legal | 175¥ | Machine-pistol size or smaller; quick-draw threshold -1. Eligible. | `sr5-core` p. 432 (PDF 434) |
| `shock-pad` | Shock pad | selectable | none | 2 / Legal | 50¥ | Requires rigid stock on rifle, shotgun, or heavy weapon; RC 1. Eligible. | `sr5-core` p. 432 (PDF 434) |
| `silencer-suppressor` | Silencer/suppressor | selectable | Barrel | 9F / Forbidden | 500¥ | Not revolvers or shotguns; Perception to notice/locate discharge -4. Eligible, not licensable. | `sr5-core` p. 432 (PDF 434) |
| `smart-firing-platform` | Smart firing platform | selectable | Under | 12F / Forbidden | 2,500¥ | One smartgun-equipped weapon; Device/Pilot 3, Targeting autosoft 3, 180-degree arc, 60-degree inclination, RC 5; remotely fireable wireless. Eligible, not licensable. | `sr5-core` pp. 432-433 (PDF 434-435) |
| `smartgun-system-internal` | Smartgun system, internal | parameterized | none | host Availability +2R / Restricted | Final weapon price x 2 | Installed in one firearm/projectile weapon; integral after retrofit and consumes no mount; includes camera/rangefinder with Capacity 1; Accuracy +2 with smartlink. Eligible only if final Availability <=12; license required. | `sr5-core` pp. 432-433 (PDF 434-435) |
| `smartgun-system-external` | Smartgun system, external | selectable | Top or Under | 4R / Restricted | 200¥ | Firearm/projectile host; camera Capacity 1; Accuracy +2 with smartlink; installation Armorer + Logic (4, 1 hour). Eligible with license. | `sr5-core` pp. 432-433 (PDF 434-435) |
| `spare-clip` | Spare clip | parameterized | none | 4 / Legal | 5¥ | Required parameter: one clip-fed weapon model; unloaded; capacity equals that weapon's clip. Eligible. | `sr5-core` pp. 432-433 (PDF 434-435) |
| `speed-loader` | Speed loader | parameterized | none | 2 / Legal | 25¥ | Required parameter: one revolver model; holds its cylinder's bullets and reloads it as a Complex Action. Eligible. | `sr5-core` pp. 432-433 (PDF 434-435) |
| `tripod` | Tripod | selectable | Under | 4 / Legal | 500¥ | Weapon with underbarrel mount; deployed kneeling/sitting gives RC 6. Eligible. | `sr5-core` pp. 432-433 (PDF 434-435) |
| `ares-light-fire-70-silencer` | Ares Light Fire 70 silencer | selectable | Barrel | none / Legal | 750¥ | Ares Light Fire 70 only; Perception modifier -5 instead of ordinary -4. Eligible. | `sr5-core` p. 425 (PDF 427) |

## Ammunition

Ammunition is sold per 10 shots. The required host profile is firearm class and,
where applicable, cased/caseless format; firearms normally accept one format,
not both. Modifiers apply to the host's base stats unless the row says they
replace them. Source: `sr5-core` pp. 433-434 (PDF 435-436).

| ID | Exact display name | Classification | Damage modifier | AP modifier | Availability / legality | Cost / quantity | Required host or payload; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `apds` | APDS | parameterized | none | -4 | 12F / Forbidden | 120¥ / 10 | Select firearm class and cased/caseless format; Eligible, not licensable. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `assault-cannon-ammo` | Assault cannon | selectable | none | none | 12F / Forbidden | 400¥ / 10 | Assault cannons only and their only ammunition; Eligible, not licensable. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `explosive-rounds` | Explosive rounds | parameterized | +1 | -1 | 9F / Forbidden | 80¥ / 10 | Select firearm class and format; critical glitch destroys weapon and attacks user with modified normal DV. Eligible, not licensable. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `flechette-rounds` | Flechette rounds | parameterized | +2 | +5 | 6R / Restricted | 65¥ / 10 | Select compatible firearm class and format; Eligible with license. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `gel-rounds` | Gel rounds | parameterized | +0S | +1 | 2R / Restricted | 25¥ / 10 | Select firearm class and format; changes damage to Stun and target Physical limit -2 for knockdown. Eligible with license. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `hollow-points` | Hollow points | parameterized | +1 | +2 | 4F / Forbidden | 70¥ / 10 | Select firearm class and format; Eligible, not licensable. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `injection-darts` | Injection darts | selectable | none | none | 4R / Restricted | 75¥ / 10 | Dart gun only; one separately purchased drug/toxin dose per dart; delivery needs 1 net hit unarmored or 3 armored. Eligible with license. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `regular-ammo` | Regular ammo | parameterized | none | none | 2R / Restricted | 20¥ / 10 | Select firearm class and cased/caseless format; Eligible with license. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `stick-n-shock` | Stick-n-Shock | parameterized | -2S(e) | -5 | 6R / Restricted | 80¥ / 10 | Select firearm class and format; damage becomes base DV -2 electrical Stun and AP is replaced by -5. Eligible with license. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `tracer` | Tracer | parameterized | none | none | 6R / Restricted | 60¥ / 10 | Full-Auto firearm only; select class/format; loaded every third round; Accuracy +1 when firing >1 round, stacks with laser but not smartgun. Eligible with license. | `sr5-core` pp. 433-434 (PDF 435-436) |
| `taser-dart` | Taser dart | selectable | none | none | 3 / Legal | 50¥ / 10 | Taser weapon only; Eligible. | `sr5-core` pp. 433-434 (PDF 435-436) |

## Grenades, Rockets, And Missiles

Actual grenades may be purchased in standard thrown or minigrenade form at the
same cost/effect; minigrenades require a grenade launcher and arm after 5m.
`flash-pak` is expressly not a grenade and has no minigrenade form. Grenade rows
are quantity `1 device`. Source: `sr5-core` pp. 434-435 (PDF 436-437).

| ID | Exact display name | Classification / form | Damage | AP | Blast | Availability / legality | Cost | Payload/rating/host restriction; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `flash-bang` | Flash-bang | selectable / standard or minigrenade | 10S | -4 | 10m Radius | 6R / Restricted | 100¥ | Thrown or grenade-launcher host; Eligible with license. | `sr5-core` pp. 434-435 (PDF 436-437) |
| `flash-pak` | Flash-pak | selectable / electronic unit | Special | none | Special | 4 / Legal | 125¥ | 10 charges, 1/Combat Turn; -4 attack dice to viewers, mitigated by flare compensation; no launcher host. Eligible. | `sr5-core` pp. 434-435 (PDF 436-437) |
| `fragmentation-grenade` | Fragmentation | selectable / standard or minigrenade | 18P(f) | +5 | -1/m | 11F / Forbidden | 100¥ | Thrown or grenade-launcher host; Eligible, not licensable. | `sr5-core` pp. 434-435 (PDF 436-437) |
| `high-explosive-grenade` | High explosive | selectable / standard or minigrenade | 16P | -2 | -2/m | 11F / Forbidden | 100¥ | Thrown or grenade-launcher host; Eligible, not licensable. | `sr5-core` pp. 434-435 (PDF 436-437) |
| `gas-grenade` | Gas | parameterized / standard or minigrenade | as Chemical | none | 10m Radius | 2 + chemical Availability / chemical legality | 40¥ + chemical cost | Required parameter: one separately purchased chemical/toxin payload; creation only when combined Availability <=12; cloud about 4 turns. | `sr5-core` pp. 434-435 (PDF 436-437) |
| `smoke-grenade` | Smoke | selectable / standard or minigrenade | none | none | 10m Radius | 4R / Restricted | 40¥ | Smoke visibility, about 4 turns; Eligible with license. Decision `gear.smoke-area`. | `sr5-core` pp. 434-435 (PDF 436-437) |
| `thermal-smoke-grenade` | Thermal smoke | selectable / standard or minigrenade | none | none | 10m Radius | 6R / Restricted | 60¥ | Thermal-smoke visibility, about 4 turns; Eligible with license. Decision `gear.smoke-area`. | `sr5-core` pp. 434-435 (PDF 436-437) |

Rockets and missiles are quantity `1 projectile`. A missile is the corresponding
rocket warhead plus internal guidance and a required Sensor rating. Missile DV,
AP, and Blast inherit the selected rocket row; Availability is rocket +4 and
cost is rocket + Sensor Rating x 500¥.

| ID | Exact display name | Classification / type | Damage | AP | Blast | Availability / legality | Cost | Rating/host restriction; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `anti-vehicle-rocket` | Anti-vehicle | creation-unavailable / rocket | 24P | -4 people / -10 vehicles and barriers | -4/m | 18F / Forbidden | 2,800¥ | Rocket launcher; no Rating; Unavailable: Availability 18. | `sr5-core` pp. 435-436 (PDF 437-438) |
| `fragmentation-rocket` | Fragmentation | selectable / rocket | 23P(f) | +5 | -1/m | 12F / Forbidden | 2,000¥ | Rocket launcher; no Rating; Eligible, not licensable. | `sr5-core` pp. 435-436 (PDF 437-438) |
| `high-explosive-rocket` | High-explosive | creation-unavailable / rocket | 21P | -2 | -2/m | 18F / Forbidden | 2,100¥ | Rocket launcher; no Rating; Unavailable: Availability 18. | `sr5-core` pp. 435-436 (PDF 437-438) |
| `anti-vehicle-missile` | Anti-vehicle missile | creation-unavailable / guided missile | 24P | -4 people / -10 vehicles and barriers | -4/m | 22F / Forbidden | 2,800¥ + Sensor Rating x 500¥ | Missile launcher; required Sensor-rating range is source-unspecified; no initial purchase exposed; Unavailable regardless: Availability 22. Decision `gear.missile-sensor-range`. | `sr5-core` pp. 435-436 (PDF 437-438) |
| `fragmentation-missile` | Fragmentation missile | creation-unavailable / guided missile | 23P(f) | +5 | -1/m | 16F / Forbidden | 2,000¥ + Sensor Rating x 500¥ | Missile launcher; required Sensor-rating range is source-unspecified; no initial purchase exposed; Unavailable: Availability 16. Decision `gear.missile-sensor-range`. | `sr5-core` pp. 435-436 (PDF 437-438) |
| `high-explosive-missile` | High-explosive missile | creation-unavailable / guided missile | 21P | -2 | -2/m | 22F / Forbidden | 2,100¥ + Sensor Rating x 500¥ | Missile launcher; required Sensor-rating range is source-unspecified; no initial purchase exposed; Unavailable: Availability 22. Decision `gear.missile-sensor-range`. | `sr5-core` pp. 435-436 (PDF 437-438) |

## Explosives

Explosive material is sold per kilogram. Its base DV is modified Rating times
the square root of kilograms, rounded down; blast is -2/m circular or -1/m in a
directional cone up to 60 degrees. Direct attachment halves target armor;
otherwise AP is -2. Source: `sr5-core` p. 436 (PDF 438).

| ID | Exact display name | Classification / type | Rating | Availability / legality | Cost / quantity | Host/component restriction; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `commercial-explosives` | Commercial | selectable / explosive | 5 | 8R / Restricted | 100¥ / kg | Requires a detonation method; Eligible with license. | `sr5-core` p. 436 (PDF 438) |
| `explosive-foam` | Foam | parameterized / explosive | 6-25 | 12F / Forbidden | Rating x 100¥ / kg | Creation permits Rating 6 only; aerosol plastic explosive; requires detonation method; Eligible at Rating 6, not licensable. | `sr5-core` p. 436 (PDF 438) |
| `plastic-explosives` | Plastic | creation-unavailable / explosive | 6-25 | 16F / Forbidden | Rating x 100¥ / kg | Requires detonation method; Unavailable: Availability 16 at every Rating. | `sr5-core` p. 436 (PDF 438) |
| `detonator-cap` | Detonator cap | selectable / explosive accessory | none | 8R / Restricted | 75¥ / 1 cap | Insert into explosive mass; programmable timer or radio signal; Eligible with license. | `sr5-core` p. 436 (PDF 438) |

## Clothing

| ID | Exact display name | Classification / type | Armor | Availability / legality | Cost / quantity | Rating/host/capacity restriction; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `clothing` | Clothing | parameterized / clothing | 0 | none / Legal | 20¥-100,000¥ / 1 outfit | Required parameter: price in printed range and authored appearance; no Capacity; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `electrochromic-modification` | Electrochromic modification | selectable / clothing modification | unchanged | host +2 / host legality | host +500¥ / 1 modification | Clothing or armored clothing host; changes color/text/images/patterns; no Rating/Capacity; Eligible if final Availability <=12. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `feedback-clothing` | Feedback clothing | selectable / clothing modification | unchanged | 8 / Legal | host +500¥ / 1 modification | Clothing host; supplies tactile AR feedback; no Rating/Capacity; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `synth-leather` | (Synth)Leather | selectable / clothing modification | 4 | none / Legal | host +200¥ / 1 modification | Jacket/duster clothing host; no Rating/Capacity; Eligible. Prose heading is `Leather jacket/duster`. | `sr5-core` pp. 436-437 (PDF 438-439) |

## Armor, Helmets, And Shields

Armor rows have quantity `1 item` and no purchasable Rating. Armor Capacity is
equal to Armor for modifications unless stated otherwise. Helmets and shields
add their Armor to worn armor rather than forming separate armor layers; using a
shield imposes -1 Physical limit when it can interfere. Source: `sr5-core` pp.
437-438 (PDF 439-440).

| ID | Exact display name | Classification / type | Armor | Availability / legality | Cost | Capacity, host restriction, included components/profile, and creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `actioneer-business-clothes` | Actioneer Business Clothes | selectable / armor | 8 | 8 / Legal | 1,500¥ | Capacity 8; includes concealable holster; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `armor-clothing` | Armor clothing | selectable / armor | 6 | 2 / Legal | 450¥ | Capacity 6; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `armor-jacket` | Armor jacket | selectable / armor | 12 | 2 / Legal | 1,000¥ | Capacity 12; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `armor-vest` | Armor vest | selectable / armor | 9 | 4 / Legal | 500¥ | Capacity 9; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `chameleon-suit` | Chameleon suit | selectable / armor | 9 | 10R / Restricted | 1,700¥ | Capacity 9; sensor/ruthenium coating adds 2 to Sneaking limit to hide and wireless +2 dice; Eligible with license. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `full-body-armor` | Full body armor | creation-unavailable / armor | 15 | 14R / Restricted | 2,000¥ | Capacity 15; optional full helmet, chemical seal, and environment adaptation; Unavailable: Availability 14. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `full-body-armor/full-helmet` | Full helmet | creation-unavailable / full-body component | +3 | none printed / host legality | host +500¥ | Full body armor only; helmet Capacity 6 for vision/audio enhancements; Unavailable because host is unavailable. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `full-body-armor/environment-adaptation` | Environment adaptation | creation-unavailable / full-body modification | unchanged | host +3 / host legality | host +1,000¥ | Full body armor only; select hot or cold environment; Unavailable because host is unavailable. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `lined-coat` | Lined coat | selectable / armor | 9 | 4 / Legal | 900¥ | Capacity 9; items hidden beneath gain Concealability -2; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `urban-explorer-jumpsuit` | Urban Explorer Jumpsuit | selectable / armor | 9 | 8 / Legal | 650¥ | Capacity 9; includes music player and biomonitor; Eligible. | `sr5-core` pp. 436-437 (PDF 438-439) |
| `helmet` | Helmet | selectable / helmet | +2 | 2 / Legal | 100¥ | Capacity 6 for trodes/vision enhancements; modifies worn armor. Eligible. Decision `gear.helmet-availability`. | `sr5-core` pp. 436, 438 (PDF 438, 440) |
| `ballistic-shield` | Ballistic shield | selectable / shield | +6 | 12R / Restricted | 1,200¥ | Capacity 6, only Chemical Protection, Fire Resistance, and Nonconductivity; generated melee profile; Eligible with license. | `sr5-core` p. 438 (PDF 440) |
| `riot-shield` | Riot shield | selectable / shield | +6 | 10R / Restricted | 1,500¥ | Capacity 6, same three modifications; 10 electrical charges and generated melee profile; Eligible with license. | `sr5-core` p. 438 (PDF 440) |

## Armor Modifications

Rated modifications consume Capacity equal to Rating. Their printed general
range is 1-6. Shield hosts accept only the three modifications explicitly noted
above. Source: `sr5-core` pp. 437-438 (PDF 439-440).

| ID | Exact display name | Classification | Capacity | Rating | Availability / legality | Cost / quantity | Host restriction/effect; creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `chemical-protection` | Chemical Protection | parameterized | [Rating] | 1-6 | 6 / Legal | Rating x 250¥ / 1 modification | Any worn armor or shield; +Rating dice against contact-vector toxins; Eligible. | `sr5-core` pp. 437-438 (PDF 439-440) |
| `chemical-seal` | Chemical Seal | creation-unavailable | 6 | none | 12R / Restricted | 3,000¥ | Full body armor including full helmet only; complete contact/inhalation protection for one hour; Unavailable because required host is Availability 14. Decision `gear.chemical-seal-table`. | `sr5-core` pp. 436-438 (PDF 438-440) |
| `fire-resistance` | Fire Resistance | parameterized | [Rating] | 1-6 | 6 / Legal | Rating x 250¥ / 1 modification | Any worn armor or shield; +Rating Armor against fire/catching fire; Eligible. | `sr5-core` pp. 437-438 (PDF 439-440) |
| `insulation` | Insulation | parameterized | [Rating] | 1-6 | 6 / Legal | Rating x 250¥ / 1 modification | Worn armor, not shields; +Rating Armor against Cold; Eligible. | `sr5-core` pp. 437-438 (PDF 439-440) |
| `nonconductivity` | Nonconductivity | parameterized | [Rating] | 1-6 | 6 / Legal | Rating x 250¥ / 1 modification | Any worn armor or shield; +Rating Armor against Electricity; Eligible. | `sr5-core` pp. 437-438 (PDF 439-440) |
| `shock-frills` | Shock Frills | selectable | 2 | none | 6R / Restricted | 250¥ / 1 modification | Worn armor, not shields; 10 charges; Unarmed Combat electrical contact attack; Eligible with license. | `sr5-core` pp. 437-438 (PDF 439-440) |
| `thermal-damping` | Thermal Damping | parameterized | [Rating] | 1-6 | 10R / Restricted | Rating x 500¥ / 1 modification | Worn armor, not shields; +Rating Sneaking limit against thermal detection and wireless +Rating dice; Eligible with license. | `sr5-core` pp. 437-438 (PDF 439-440) |

Shock frills and the riot shield hold 10 charges and recharge one per 10 seconds
from a power point or one per wireless-enabled hour by induction. Chemical Seal
wirelessly activates as a Free Action. Source: `sr5-core` pp. 437-438 (PDF
439-440).

## Source Discrepancies And Approved Interpretations

| Fact | Transcription / approved treatment | Source |
| --- | --- | --- |
| Ruger model name | Canonical ID/display are `ruger-101` / `Ruger 101`; `Ruger 100` is retained only as table provenance. Decision `gear.ruger-model-name`. | `sr5-core` pp. 428-429 (PDF 430-431) |
| Creation legality suffix | R/F does not itself block creation; only numeric Availability >12 does. Restricted requires a license and Forbidden cannot be licensed. Decision `gear.legality-at-creation`. | `sr5-core` pp. 94, 418-419 (PDF 96, 420-421) |
| Rating cap | Apply creation Rating 6 cap to explicit purchasable Rating/Force, not Capacity or quantity. Decision `gear.rating-cap-force`. | `sr5-core` pp. 94, 418 (PDF 96, 420) |
| Defiance EX Shocker price | Product table prints 250¥; Step Six example prints 210¥. Use the product-table value. Decision `gear.defiance-ex-shocker-cost`. | `sr5-core` pp. 96, 425 (PDF 98, 427) |
| Chemical Seal | Clothing/armor table prints Availability `+6`, cost `+6,000¥`; Armor Modification table prints Capacity 6, Availability 12R, cost 3,000¥. Use the dedicated modification-table values. Decision `gear.chemical-seal-table`. | `sr5-core` pp. 436, 438 (PDF 438, 440) |
| Helmet Availability | Clothing/armor table prints no Availability; helmet/shield table prints 2. Use Availability 2. Decision `gear.helmet-availability`. | `sr5-core` pp. 436, 438 (PDF 438, 440) |
| Smoke area | Table prints 10m Radius; prose prints 10m diameter. Thermal smoke says it is identical but its table also prints 10m Radius. Use 10m Radius for both. Decision `gear.smoke-area`. | `sr5-core` pp. 434-435 (PDF 436-437) |
| Launcher arming distance | Combat rules say launcher projectiles arm after 5m and all launchers have 5m minimum range; street-gear rocket/missile prose says rockets/missiles arm after 10m. Use 5m for grenades and 10m for rockets/missiles. Decision `gear.launcher-arming-distance`. | `sr5-core` pp. 182, 435 (PDF 184, 437) |
| Cavalier rifle image label | Product prose/table say `Cavalier Arms Crockett EBR`; adjacent illustration label says `Cavalier Arms Alamo EBR`. No separate Alamo product row is generated. | `sr5-core` pp. 428-429 (PDF 430-431) |
| Missile Sensor rating | Missile cost requires Sensor rating, but the reviewed weapon rules print no permitted missile Sensor-rating range. Preserve the omission and expose no initial missile purchase. Decision `gear.missile-sensor-range`. | `sr5-core` pp. 435-436 (PDF 437-438) |
| Arrow Rating range | Arrow and injection-arrow rows use Rating for Availability/cost and cap bow performance, but only the bow receives an explicit maximum of 10. Use the bow's Rating 1-10 range and creation cap of 6. Decision `gear.arrow-rating-range`. | `sr5-core` pp. 423-424 (PDF 425-426) |
| Super Squirt ammunition | The weapon uses 20-round clips of DMSO gel packs and a selected chemical payload, but the ammunition table supplies no gel-pack product, price, or Availability. Expose no separate gel-pack purchase. Decision `gear.super-squirt-ammunition`. | `sr5-core` pp. 429-434 (PDF 431-436) |

## Review Footer

### Reviewed Ranges

- Creation resources and gear limits: `sr5-core` pp. 94-101 (PDF 96-103).
- Combat projectile/launcher rules: `sr5-core` pp. 181-183 (PDF 183-185).
- Gear glossary, legality, mounts, and creation limit: `sr5-core` pp. 416-423
  (PDF 418-425).
- Every core weapon, accessory, ammunition, grenade, rocket/missile, explosive,
  clothing, armor, helmet, shield, and armor-modification table and its associated
  prose: `sr5-core` pp. 422-438 (PDF 424-440).

### Counts By Category

Counts include generated alternate profiles but do not double-count integral
accessory facts recorded on parent firearm rows.

| Category | Selectable | Parameterized | Included-component | Generated | Bookkeeping | Creation-unavailable | Excluded | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Melee weapons | 17 | 0 | 0 | 0 | 0 | 0 | 0 | 17 |
| Projectile/throwing weapons and projectiles | 6 | 3 | 0 | 0 | 0 | 0 | 0 | 9 |
| Firearms, special weapons, heavy weapons, launchers | 46 | 0 | 0 | 0 | 0 | 6 | 0 | 52 |
| Generated weapon profiles | 0 | 0 | 0 | 7 | 0 | 0 | 0 | 7 |
| Firearm accessories | 15 | 4 | 0 | 0 | 0 | 0 | 0 | 19 |
| Ammunition | 3 | 8 | 0 | 0 | 0 | 0 | 0 | 11 |
| Grenades/flash-pak | 6 | 1 | 0 | 0 | 0 | 0 | 0 | 7 |
| Rockets and missiles | 1 | 0 | 0 | 0 | 0 | 5 | 0 | 6 |
| Explosives and detonator | 2 | 1 | 0 | 0 | 0 | 1 | 0 | 4 |
| Clothing and clothing modifications | 3 | 1 | 0 | 0 | 0 | 0 | 0 | 4 |
| Armor, helmets, shields, full-body options | 10 | 0 | 0 | 0 | 0 | 3 | 0 | 13 |
| Armor modifications | 1 | 5 | 0 | 0 | 0 | 1 | 0 | 7 |
| **Total** | **110** | **23** | **0** | **7** | **0** | **16** | **0** | **156** |

Classification counts describe catalog entries, not units purchased. For
example, ammunition has eleven entries even though each purchase supplies ten
shots. Integral accessories are relationship facts on their parent rows, not
separately purchasable included-component entries.

### Counts By Weapon Classification

| Weapon classification | Product entries |
| --- | ---: |
| Blades | 8 |
| Clubs | 6 |
| Other/exotic melee | 3 |
| Bows/arrows | 3 |
| Crossbows/bolts | 5 |
| Throwing weapons | 1 |
| Tasers | 2 |
| Hold-outs | 3 |
| Light pistols | 6 |
| Heavy pistols | 6 |
| Machine pistols | 3 |
| Submachine guns | 6 |
| Assault rifles | 5 |
| Sniper/sport rifles | 5 |
| Shotguns | 3 |
| Special weapons | 4 |
| Machine guns | 3 |
| Cannons/launchers | 6 |
| Flash-pak electronic unit | 1 |
| **Weapon products** | **79** |

The 79 weapon products are 17 melee + 9 projectile/throwing + 52 firearm/heavy
products + the `flash-pak` electronic weapon-like device. The seven generated
profiles are not additional products.

### Explicit Exclusions

- Improvised melee-weapon profiles are open situational rules, not published
  products. Source: `sr5-core` pp. 184, 422 (PDF 186, 424).
- Cyber implant weapons belong to the augmentation ledger, not this file.
  Source: `sr5-core` pp. 422, 458 (PDF 424, 460).
- Weapon style examples, club-object examples, cased/caseless variants, grenade
  trigger choices, and chemical payload examples do not become duplicate closed
  products; they are parameters or operating modes of rows above.
- The `Cavalier Arms Alamo EBR` illustration caption is excluded as a conflicting
  label, not a second product.
- All non-core weapons, armor, modifications, and other equipment are excluded
  from this core-only ledger.
- Full body armor and every other numeric-Availability-over-12 core product remain
  transcribed as `creation-unavailable`; they are not silently dropped.

### Remaining Unknown Facts

None. Decisions `gear.defiance-ex-shocker-cost`, `gear.chemical-seal-table`,
`gear.helmet-availability`, `gear.smoke-area`,
`gear.launcher-arming-distance`, `gear.missile-sensor-range`,
`gear.arrow-rating-range`, and `gear.super-squirt-ammunition` resolve or
explicitly preserve every source conflict and omission affecting this ledger.

### Runtime Reconciliation Status

`Not implemented`. CHAR-802 has not materialized or reconciled an immutable
runtime weapons/armor catalog. Current approved-PDF review count: 156 catalog
entries, including 16 creation-unavailable entries and 7 generated profiles;
runtime count and semantic digest are unavailable.
