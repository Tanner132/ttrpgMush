# Run & Gun Weapon Accessories Ledger (CHAR-817)

This is the CHAR-817 source ledger for Run & Gun's Weapon Accessories
section. It is a review input for the runtime catalog change it accompanies,
not a substitute for the approved book. It extends
[`RUN_GUN_WEAPONS.md`](RUN_GUN_WEAPONS.md) (CHAR-816), which excluded this
material pending a schema change.

CHAR-816 deferred Weapon Accessories because its 6-slot mounting system (Top,
Underbarrel, Barrel, Side, Internal, Stock) appeared to need a new gameplay
mechanic. On review, the project already had a full mount/attachment system
(`WeaponAccessoryDefinition`, `WeaponMount`, `GearAttachmentEvaluator`) built
for `sr5-core`'s own accessories — it only needed a backward-compatible
extension, not a new subsystem:

- Three new `WeaponMount` enum values: `Side`, `Internal`, `Stock`.
- An `AdditionalMounts` list on `WeaponAccessoryDefinition`, generalizing the
  old two-way `TopOrUnderbarrel` combinator into an N-way candidate set
  (`GearAttachmentEvaluator.MountCandidates`). A one-candidate accessory
  still auto-assigns its mount; a multi-candidate accessory still requires an
  explicit `AttachmentSelection.Mount` choice, exactly as `TopOrUnderbarrel`
  always worked — just generalized past two options (e.g. Guncam has five).
- A `RestrictedToWeaponCategoryIds` allow-list on
  `WeaponAccessoryDefinition`, checked before mount evaluation, for
  accessories printed for only a subset of weapon categories (e.g. Bayonet,
  Foregrip).
- Two new `MountsByWeaponCategory` entries for Run & Gun's new weapon
  categories: `laser-weapons` (given the broadest of the ranges its member
  models span — SMG through sniper-rifle) and `flamethrowers` (Internal
  only, per "cannot mount any accessories except biometric safety systems,"
  `run-gun` p. 49, PDF 51; `Mount.None` accessories like Sling and Tracker
  are unaffected since they occupy no physical slot).

Old pinned catalog versions deserialize unaffected: every new field is
`= null`-defaulted and additive.

## Source

Only `run-gun` is used, already pinned in
[`../SR5_RULESET_MANIFEST.md`](../SR5_RULESET_MANIFEST.md). Citations use the
same two-page printed/PDF offset verified for CHAR-815/816.

## Scope

Included: every accessory printed in the "Weapon Accessories" section
(`run-gun` pp. 50-53, PDF 52-55) that has a purchasable price-table entry —
33 new catalog entries, published alongside the 17 already-existing
`sr5-core` accessories for 50 total.

Excluded (see Explicit Exclusions And Discrepancies below): Underbarrel
Weight, Weapon Commlink, and the install-test procedures (Matrix Search
thresholds, Armorer + Logic extended tests, tool-kit vs. tool-shop
requirements — GM/mechanical procedure, not a purchasable option, consistent
with how this project treats every other test procedure).

## Cost-Modeling Convention

Every entry publishes `Mount`, `Availability`, and `Cost` as printed.
Parameterized items (Extreme Environment Modification) use `RatingRange` +
`PerRating` cost, matching the existing convention for e.g. armor
modifications. Sub-items that are themselves optional add-ons to a parent
accessory (the three Advanced Safety System options; the three Safe Target
System follow-ons) are published as independent `Mount: None` gear-less
accessories rather than nested under their parent, since the catalog has no
parent/child accessory relationship — a player selects them alongside the
parent accessory on the same host.

### Multi-slot accessories (AdditionalMounts)

| Accessory | Candidate mounts |
| --- | --- |
| Bayonet | Top, Underbarrel (restricted to rifle-and-larger categories) |
| Flashlight (all three variants) | Top, Underbarrel, Side |
| Guncam | Top, Underbarrel, Barrel, Side, Internal |
| Improved Range Finder | Top, Underbarrel, Barrel, Side, Internal, Stock (all six slots) |
| Safe Target System | Top, Underbarrel, Barrel, Side, Internal |
| Slide Mount | Top, Underbarrel, Side |

## New Weapon Accessories

| ID | Display name | Mount | Restricted categories | Availability / legality | Cost | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `accessory-run-gun-advanced-safety-system` | Advanced Safety System | Internal | -- | 4 / Legal | 600¥ | `run-gun` p. 50 (PDF 52) |
| `accessory-run-gun-immobilization` | Immobilization (Safety System) | Internal | -- | 6 / Legal | 100¥ | `run-gun` p. 50 (PDF 52) |
| `accessory-run-gun-self-destruct` | Self Destruct (Safety System) | Internal | -- | 6 / Legal | 200¥ | `run-gun` p. 50 (PDF 52) |
| `accessory-run-gun-explosive-self-destruct` | Explosive Self Destruct (Safety System) | Internal | -- | 11F / Forbidden | 400¥ | `run-gun` p. 50 (PDF 52) |
| `accessory-run-gun-electro-shocker` | Electro Shocker (Safety System) | Internal | -- | 6R / Restricted | 350¥ | `run-gun` p. 50 (PDF 52) |
| `accessory-run-gun-bayonet` | Bayonet | Top, Underbarrel | assault-rifles, sniper-rifles, shotguns, special-weapons, machine-guns, cannons-launchers | 4R / Restricted | 50¥ | `run-gun` p. 50 (PDF 52) |
| `accessory-run-gun-concealed-quick-draw-holster` | Concealed Quick-Draw Holster | None | -- | 6 / Legal | 275¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-extreme-environment-modification` | Extreme Environment Modification (Rating 1-2) | None | -- | 8 / Legal | 1,500¥/rating | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-flashlight-standard` | Flashlight, Standard | Top, Underbarrel, Side | -- | 2 / Legal | 50¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-flashlight-low-light` | Flashlight, Low Light | Top, Underbarrel, Side | -- | 4 / Legal | 200¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-flashlight-infrared` | Flashlight, Infrared | Top, Underbarrel, Side | -- | 6 / Legal | 400¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-folding-stock` | Folding Stock | Stock | -- | 2 / Legal | 30¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-foregrip` | Foregrip | Barrel | submachine-guns, assault-rifles, sniper-rifles, shotguns, special-weapons, machine-guns, cannons-launchers | 2 / Legal | 100¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-gecko-grip` | Gecko Grip | None (simplified) | -- | 6 / Legal | 100¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-guncam` | Guncam | Top, Underbarrel, Barrel, Side, Internal | -- | 4 / Legal | 350¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-hip-pad-bracing-system` | Hip Pad Bracing System | Stock | -- | 4 / Legal | 250¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-improved-range-finder` | Improved Range Finder | All six mounts (simplified) | -- | 6 / Legal | 2,000¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-peak-discharge-power-clip` | Peak-Discharge Battery Pack, Power Clip | None | -- | 14F / Forbidden | 400¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-peak-discharge-satchel-power-pack` | Peak-Discharge Battery Pack, Satchel Power Pack | None | -- | 16F / Forbidden | 900¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-peak-discharge-power-backpack` | Peak-Discharge Battery Pack, Power Backpack | None | -- | 20F / Forbidden | 2,500¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-safe-target-system` | Safe Target System | Top, Underbarrel, Barrel, Side, Internal | -- | 6 / Legal | 750¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-safe-target-additional-profiles` | Safe Target System, Additional RFID/GPS Profiles (10) | None | -- | 6 / Legal | 25¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-safe-target-image-recognition` | Safe Target System, Image Recognition Capabilities | None | -- | 8 / Legal | 300¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-safe-target-extra-image-profiles` | Safe Target System, Extra Image Profiles (10) | None | -- | 8 / Legal | 25¥ | `run-gun` p. 51 (PDF 53) |
| `accessory-run-gun-slide-mount` | Slide Mount | Top, Underbarrel, Side | -- | 4 / Legal | 500¥ | `run-gun` p. 52 (PDF 54) |
| `accessory-run-gun-sling` | Sling | None | -- | none / Legal | 15¥ | `run-gun` p. 52 (PDF 54) |
| `accessory-run-gun-tracker` | Tracker | None | -- | 4 / Legal | 150¥ | `run-gun` p. 53 (PDF 55) |
| `accessory-run-gun-underbarrel-bola-launcher` | Underbarrel Bola Launcher | Underbarrel | -- | 8R / Restricted | 350¥ | `run-gun` p. 53 (PDF 55) |
| `accessory-run-gun-underbarrel-chainsaw` | Underbarrel Chainsaw | Underbarrel | -- | 10R / Restricted | 2,500¥ | `run-gun` p. 53 (PDF 55) |
| `accessory-run-gun-underbarrel-flamethrower` | Underbarrel Flamethrower | Underbarrel | -- | 18F / Forbidden | 2,400¥ | `run-gun` p. 53 (PDF 55) |
| `accessory-run-gun-underbarrel-grapple-gun` | Underbarrel Grapple Gun | Underbarrel | -- | 8R / Restricted | 600¥ | `run-gun` p. 53 (PDF 55) |
| `accessory-run-gun-underbarrel-grenade-launcher` | Underbarrel Grenade Launcher | Underbarrel | -- | 10F / Forbidden | 3,500¥ | `run-gun` p. 53 (PDF 55) |
| `accessory-run-gun-weapon-personality` | Weapon Personality | None | -- | 8 / Legal | 250¥ | `run-gun` p. 53 (PDF 55) |

## Explicit Exclusions And Discrepancies

| Item | Treatment | Source |
| --- | --- | --- |
| Underbarrel Weight | Excluded — the printed price table has no Availability or Cost value for this row at all (confirmed by re-extracting the exact table row in raw-text mode; a genuine book gap, not an extraction artifact). No value to invent under this project's "publish exactly as printed" convention. | `run-gun` p. 53 (PDF 55) |
| Weapon Commlink | Excluded — priced as "as commlink + 200¥," where "commlink" is an unspecified, highly variable product with no single resolvable base price (unlike Underbarrel Chainsaw/Flamethrower below, which each reference exactly one already-cataloged product). Unmodelable under the current `CostDefinition` schema, analogous to the recorded Autosoft-pricing exclusion in `SR5_RULE_DECISIONS.md`. | `run-gun` p. 53 (PDF 55) |
| Underbarrel Chainsaw priced "as chainsaw + 500¥" | Resolved by cross-reference: the only chainsaw in the catalog is Ash Arms Combat Chainsaw (`ash-arms-combat-chainsaw`, 2,000¥, CHAR-816) → 2,500¥, 10R (chainsaw's 6R + printed "+4" availability step). | `run-gun` p. 53 (PDF 55) |
| Underbarrel Flamethrower priced "as flamethrower +2 avail / +200¥" | Resolved by cross-reference: the only flamethrower in the catalog is Shiawase Blazer (`shiawase-blazer`, 16F/2,200¥, CHAR-816) → 18F/2,400¥. | `run-gun` p. 53 (PDF 55) |
| Advanced Safety System's four sub-options (Immobilization, Self Destruct, Explosive Self Destruct, Electro Shocker) | Published as four independent `Mount: None` accessories rather than nested choices under a parent, since the catalog has no accessory-of-accessory relationship; a player selects the base Advanced Safety System plus any sub-options they want on the same host. | `run-gun` p. 50 (PDF 52) |
| Safe Target System's three follow-on options (Additional Profiles, Image Recognition, Extra Image Profiles) | Same treatment as Advanced Safety System's sub-options above. | `run-gun` p. 51 (PDF 53) |
| Gecko Grip | Printed without a mount slot restriction; modeled as `Mount: None` (simplified — the book's own text does not tie it to a specific physical slot). | `run-gun` p. 51 (PDF 53) |
| Improved Range Finder | Printed as usable in any mount; modeled with all six `WeaponMount` values as its candidate set (simplified representation of "any slot"). | `run-gun` p. 51 (PDF 53) |

## Review Footer

- Reviewed weapon accessory rules: `run-gun` pp. 50-53 (PDF 52-55).
- Approved-PDF accessory products in scope: 35 named price-table rows, of
  which 33 are modeled (2 excluded — see table above).
- Reconciliation: 33 new catalog `weaponAccessories` entries plus the 17
  already-existing `sr5-core` entries account for all 50 published entries
  with no unexplained inventory difference, as of catalog version
  `sr5-core` `1.7.0`.
