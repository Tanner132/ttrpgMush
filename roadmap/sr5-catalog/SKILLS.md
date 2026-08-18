# SR5 Skills Ledger

This file is the CHAR-801 row-level review of core active skills, skill groups,
specializations, Knowledge skills, and Language skills. It is a review input for
CHAR-802, not a runtime catalog. Only the pinned `sr5-core` PDF and approved
decisions in [`../SR5_RULE_DECISIONS.md`](../SR5_RULE_DECISIONS.md) are used.

## Field Conventions

- Every active skill has a natural creation rating range of 1-6. The one skill
  selected by Aptitude may reach 7; Aptitude costs 14 Karma and may be taken only
  once. Classification, cost, and maximum: `sr5-core` pp. 72, 88, 131 (PDF 74,
  90, 133); decision `skill.creation-maximum`.
- `none` means the source supplies no group or required parameter. `not
  applicable` means the concept does not apply.
- `General` creation eligibility means any character may buy the skill, subject
  to the rating and allocation rules below.
- `Magic` creation eligibility means the character must have a quality providing
  Magic; Magic must be at least 1 to use the skill. Magicians and mystic adepts
  may use all three magical groups. Adepts cannot use Sorcery, Conjuring, or
  Enchanting group skills. An aspected magician may use only the selected one of
  those groups. Ungrouped magical skills remain subject to their own listed
  prerequisite. `sr5-core` pp. 69, 89, 142 (PDF 71, 91, 144).
- `Assensing` eligibility additionally requires astral perception. An adept can
  learn Assensing only with the Astral Perception power. `sr5-core` pp. 69, 142
  (PDF 71, 144).
- `Resonance` eligibility means technomancer-only and requires the Resonance
  attribute. `sr5-core` pp. 89, 143 (PDF 91, 145).
- `Closed` specialization values are the complete printed list. `Open` values
  require an authored subject of the stated type; parenthetical book examples do
  not become catalog entries. `Hybrid` combines named closed values with an
  open-authored subject. `None printed` means the core supplies no specialization
  list for that skill; the project does not invent one.
- Closed specialization values are `selectable` child facts. Their stable ID is
  scoped to the parent as `<skill-id>/<lowercase-kebab-value>`; for example,
  `archery/non-standard-ammunition`. Open branches are `parameterized` child
  facts and store the bounded authored subject rather than generating a catalog
  option from each subject.

## Active Skills

All 75 rows are creation-selectable. The three `(Specific)` families are
`parameterized`; the other 72 are `selectable`. Each parameterized selection is
a separately rated skill profile keyed by its required authored subject. The
subject must identify one specific unusual weapon or vehicle; examples are
supporting text, not closed subject catalogs. `sr5-core` pp. 131, 147, 151 (PDF
133, 149, 153).

| ID | Display name | Class | Category | Linked attribute | Group | Default | Required parameter | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `archery` | Archery | selectable | Combat | Agility | none | Yes | none | General | `sr5-core` pp. 130, 151 (PDF 132, 153) |
| `automatics` | Automatics | selectable | Combat | Agility | Firearms | Yes | none | General | `sr5-core` pp. 130, 151 (PDF 132, 153) |
| `blades` | Blades | selectable | Combat | Agility | Close Combat | Yes | none | General | `sr5-core` pp. 130, 151 (PDF 132, 153) |
| `clubs` | Clubs | selectable | Combat | Agility | Close Combat | Yes | none | General | `sr5-core` pp. 131, 151 (PDF 133, 153) |
| `escape-artist` | Escape Artist | selectable | Physical | Agility | none | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `exotic-melee-weapon` | Exotic Melee Weapon (Specific) | parameterized | Combat | Agility | none | Yes | specific unusual melee weapon | General | `sr5-core` pp. 90, 151 (PDF 92, 153) |
| `exotic-ranged-weapon` | Exotic Ranged Weapon (Specific) | parameterized | Combat | Agility | none | No | specific unusual ranged weapon | General | `sr5-core` pp. 131, 151 (PDF 133, 153) |
| `gunnery` | Gunnery | selectable | Vehicle | Agility | none | Yes | none | General | `sr5-core` pp. 146, 151 (PDF 148, 153) |
| `gymnastics` | Gymnastics | selectable | Physical | Agility | Athletics | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `heavy-weapons` | Heavy Weapons | selectable | Combat | Agility | none | Yes | none | General | `sr5-core` pp. 132, 151 (PDF 134, 153) |
| `locksmith` | Locksmith | selectable | Technical | Agility | none | No | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `longarms` | Longarms | selectable | Combat | Agility | Firearms | Yes | none | General | `sr5-core` pp. 132, 151 (PDF 134, 153) |
| `palming` | Palming | selectable | Physical | Agility | Stealth | No | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `pistols` | Pistols | selectable | Combat | Agility | Firearms | Yes | none | General | `sr5-core` pp. 132, 151 (PDF 134, 153) |
| `sneaking` | Sneaking | selectable | Physical | Agility | Stealth | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `throwing-weapons` | Throwing Weapons | selectable | Combat | Agility | none | Yes | none | General | `sr5-core` pp. 90, 132, 151 (PDF 92, 134, 153) |
| `unarmed-combat` | Unarmed Combat | selectable | Combat | Agility | Close Combat | Yes | none | General | `sr5-core` pp. 132, 151 (PDF 134, 153) |
| `diving` | Diving | selectable | Physical | Body | none | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `free-fall` | Free-Fall | selectable | Physical | Body | none | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `pilot-aerospace` | Pilot Aerospace | selectable | Vehicle | Reaction | none | No | none | General | `sr5-core` pp. 146, 151 (PDF 148, 153) |
| `pilot-aircraft` | Pilot Aircraft | selectable | Vehicle | Reaction | none | No | none | General | `sr5-core` pp. 147, 151 (PDF 149, 153) |
| `pilot-walker` | Pilot Walker | selectable | Vehicle | Reaction | none | No | none | General | `sr5-core` pp. 147, 151 (PDF 149, 153) |
| `pilot-exotic-vehicle` | Pilot Exotic Vehicle (Specific) | parameterized | Vehicle | Reaction | none | No | specific exotic vehicle | General | `sr5-core` pp. 147, 151 (PDF 149, 153) |
| `pilot-ground-craft` | Pilot Ground Craft | selectable | Vehicle | Reaction | none | Yes | none | General | `sr5-core` pp. 147, 151 (PDF 149, 153) |
| `pilot-watercraft` | Pilot Watercraft | selectable | Vehicle | Reaction | none | Yes | none | General | `sr5-core` pp. 147, 151 (PDF 149, 153) |
| `running` | Running | selectable | Physical | Strength | Athletics | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `swimming` | Swimming | selectable | Physical | Strength | Athletics | Yes | none | General | `sr5-core` pp. 134, 151 (PDF 136, 153) |
| `animal-handling` | Animal Handling | selectable | Technical | Charisma | none | Yes | none | General | `sr5-core` pp. 143, 151 (PDF 145, 153) |
| `con` | Con | selectable | Social | Charisma | Acting | Yes | none | General | `sr5-core` pp. 138, 151 (PDF 140, 153) |
| `etiquette` | Etiquette | selectable | Social | Charisma | Influence | Yes | none | General | `sr5-core` pp. 138, 151 (PDF 140, 153) |
| `impersonation` | Impersonation | selectable | Social | Charisma | Acting | Yes | none | General | `sr5-core` pp. 138, 151 (PDF 140, 153) |
| `instruction` | Instruction | selectable | Social | Charisma | none | Yes | none | General | `sr5-core` pp. 138, 151 (PDF 140, 153) |
| `intimidation` | Intimidation | selectable | Social | Charisma | none | Yes | none | General | `sr5-core` pp. 139, 151 (PDF 141, 153) |
| `leadership` | Leadership | selectable | Social | Charisma | Influence | Yes | none | General | `sr5-core` pp. 139, 151 (PDF 141, 153) |
| `negotiation` | Negotiation | selectable | Social | Charisma | Influence | Yes | none | General | `sr5-core` pp. 139, 151 (PDF 141, 153) |
| `performance` | Performance | selectable | Social | Charisma | Acting | Yes | none | General | `sr5-core` pp. 139, 151 (PDF 141, 153) |
| `artisan` | Artisan | selectable | Technical | Intuition | none | No | none | General | `sr5-core` pp. 143, 151 (PDF 145, 153) |
| `assensing` | Assensing | selectable | Magical | Intuition | none | No | none | Assensing | `sr5-core` pp. 69, 142, 151 (PDF 71, 144, 153) |
| `disguise` | Disguise | selectable | Physical | Intuition | Stealth | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `navigation` | Navigation | selectable | Technical | Intuition | Outdoors | Yes | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `perception` | Perception | selectable | Physical | Intuition | none | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `tracking` | Tracking | selectable | Physical | Intuition | Outdoors | Yes | none | General | `sr5-core` pp. 134, 151 (PDF 136, 153) |
| `aeronautics-mechanic` | Aeronautics Mechanic | selectable | Technical | Logic | Engineering | No | none | General | `sr5-core` pp. 143, 151 (PDF 145, 153) |
| `arcana` | Arcana | selectable | Magical | Logic | none | No | none | Magic | `sr5-core` pp. 142, 151 (PDF 144, 153); `skill.catalog-defects` |
| `armorer` | Armorer | selectable | Technical | Logic | none | Yes | none | General | `sr5-core` pp. 143, 151 (PDF 145, 153) |
| `automotive-mechanic` | Automotive Mechanic | selectable | Technical | Logic | Engineering | No | none | General | `sr5-core` pp. 143-144, 151 (PDF 145-146, 153) |
| `biotechnology` | Biotechnology | selectable | Technical | Logic | none | No | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `chemistry` | Chemistry | selectable | Technical | Logic | none | No | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `computer` | Computer | selectable | Technical | Logic | Electronics | Yes | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `cybercombat` | Cybercombat | selectable | Technical | Logic | Cracking | Yes | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `cybertechnology` | Cybertechnology | selectable | Technical | Logic | Biotech | No | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `demolitions` | Demolitions | selectable | Technical | Logic | none | Yes | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `electronic-warfare` | Electronic Warfare | selectable | Technical | Logic | Cracking | No | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `first-aid` | First Aid | selectable | Technical | Logic | Biotech | Yes | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `forgery` | Forgery | selectable | Technical | Logic | none | Yes | none | General | `sr5-core` pp. 144, 151 (PDF 146, 153) |
| `hacking` | Hacking | selectable | Technical | Logic | Cracking | Yes | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `hardware` | Hardware | selectable | Technical | Logic | Electronics | No | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `industrial-mechanic` | Industrial Mechanic | selectable | Technical | Logic | Engineering | No | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `medicine` | Medicine | selectable | Technical | Logic | Biotech | No | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `nautical-mechanic` | Nautical Mechanic | selectable | Technical | Logic | Engineering | No | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `software` | Software | selectable | Technical | Logic | Electronics | No | none | General | `sr5-core` pp. 145, 151 (PDF 147, 153) |
| `astral-combat` | Astral Combat | selectable | Magical | Willpower | none | No | none | Magic | `sr5-core` pp. 142, 151 (PDF 144, 153) |
| `survival` | Survival | selectable | Physical | Willpower | Outdoors | Yes | none | General | `sr5-core` pp. 133, 151 (PDF 135, 153) |
| `alchemy` | Alchemy | selectable | Magical | Magic | Enchanting | No | none | Magic; Enchanting path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 142, 151 (PDF 71, 144, 153) |
| `artificing` | Artificing | selectable | Magical | Magic | Enchanting | No | none | Magic; Enchanting path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 142, 151, 153 (PDF 71, 144, 153, 155); `skill.catalog-defects` |
| `banishing` | Banishing | selectable | Magical | Magic | Conjuring | No | none | Magic; Conjuring path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 142, 151 (PDF 71, 144, 153) |
| `binding` | Binding | selectable | Magical | Magic | Conjuring | No | none | Magic; Conjuring path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 142, 151 (PDF 71, 144, 153) |
| `counterspelling` | Counterspelling | selectable | Magical | Magic | Sorcery | No | none | Magic; Sorcery path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 142, 151 (PDF 71, 144, 153) |
| `disenchanting` | Disenchanting | selectable | Magical | Magic | Enchanting | No | none | Magic; Enchanting path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 142, 151 (PDF 71, 144, 153) |
| `ritual-spellcasting` | Ritual Spellcasting | selectable | Magical | Magic | Sorcery | No | none | Magic; Sorcery path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 142, 151 (PDF 71, 144, 153) |
| `spellcasting` | Spellcasting | selectable | Magical | Magic | Sorcery | No | none | Magic; Sorcery path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 143, 151 (PDF 71, 145, 153) |
| `summoning` | Summoning | selectable | Magical | Magic | Conjuring | No | none | Magic; Conjuring path for aspected magician; unavailable to adept | `sr5-core` pp. 69, 143, 151 (PDF 71, 145, 153) |
| `compiling` | Compiling | selectable | Resonance | Resonance | Tasking | No | none | Resonance | `sr5-core` pp. 143, 151 (PDF 145, 153) |
| `decompiling` | Decompiling | selectable | Resonance | Resonance | Tasking | No | none | Resonance | `sr5-core` pp. 143, 151 (PDF 145, 153) |
| `registering` | Registering | selectable | Resonance | Resonance | Tasking | No | none | Resonance | `sr5-core` pp. 143, 151 (PDF 145, 153) |

### Active-Skill Specializations

All values below are child selections of the active skill, never independent
skills. Open subjects remain bounded authored text in the runtime model; no
example in parentheses is promoted to a closed value.

| Skill ID | Printed specialization domain | Source |
| --- | --- | --- |
| `archery` | Closed: Bow; Crossbow; Non-Standard Ammunition; Slingshot | `sr5-core` p. 130 (PDF 132) |
| `automatics` | Closed: Assault Rifles; Cyber-Implant; Machine Pistols; Submachine Guns | `sr5-core` p. 130 (PDF 132) |
| `blades` | Closed: Axes; Knives; Swords; Parrying | `sr5-core` p. 130 (PDF 132) |
| `clubs` | Closed: Batons; Hammers; Saps; Staves; Parrying | `sr5-core` p. 131 (PDF 133) |
| `escape-artist` | Hybrid: closed Contortionism; open restraint type | `sr5-core` p. 133 (PDF 135) |
| `exotic-melee-weapon` | None printed; the core has no detail block for this listed skill | `sr5-core` pp. 90, 130-132, 151 (PDF 92, 132-134, 153) |
| `exotic-ranged-weapon` | None | `sr5-core` p. 131 (PDF 133) |
| `gunnery` | Closed: Artillery; Ballistic; Energy; Guided Missile; Rocket | `sr5-core` p. 146 (PDF 148) |
| `gymnastics` | Closed: Balance; Climbing; Dance; Leaping; Parkour; Rolling | `sr5-core` p. 133 (PDF 135) |
| `heavy-weapons` | Closed: Assault Cannons; Grenade Launchers; Guided Missiles; Machine Guns; Rocket Launchers | `sr5-core` p. 132 (PDF 134) |
| `locksmith` | Open: lock type | `sr5-core` p. 145 (PDF 147) |
| `longarms` | Closed: Extended-Range Shots; Long-Range Shots; Shotguns; Sniper Rifles | `sr5-core` p. 132 (PDF 134) |
| `palming` | Closed: Legerdemain; Pickpocket; Pilfering | `sr5-core` p. 133 (PDF 135) |
| `pistols` | Closed: Holdouts; Revolvers; Semi-Automatics; Tasers | `sr5-core` p. 132 (PDF 134) |
| `sneaking` | Open: location type | `sr5-core` p. 133 (PDF 135) |
| `throwing-weapons` | Closed: Aerodynamic; Blades; Non-Aerodynamic | `sr5-core` p. 132 (PDF 134) |
| `unarmed-combat` | Hybrid: closed Blocking, Cyber Implants, Subduing Combat; open specific martial art | `sr5-core` p. 132 (PDF 134) |
| `diving` | Hybrid: closed Controlled Hyperventilation; open breathing apparatus or condition | `sr5-core` p. 133 (PDF 135) |
| `free-fall` | Closed: BASE Jumping; Break-Fall; Bungee; HALO; Low Altitude; Parachute; Static Line; Wingsuit; Zipline | `sr5-core` p. 133 (PDF 135) |
| `pilot-aerospace` | Closed: Deep Space; Launch Craft; Remote Operation; Semiballistic; Suborbital | `sr5-core` p. 146 (PDF 148) |
| `pilot-aircraft` | Closed: Fixed-Wing; Lighter-Than-Air; Remote Operation; Rotary Wing; Tilt Wing; Vectored Thrust | `sr5-core` p. 147 (PDF 149) |
| `pilot-walker` | Closed: Biped; Multiped; Quadruped; Remote | `sr5-core` p. 147 (PDF 149) |
| `pilot-exotic-vehicle` | Not applicable | `sr5-core` p. 147 (PDF 149) |
| `pilot-ground-craft` | Closed: Bike; Hovercraft; Remote Operation; Tracked; Wheeled | `sr5-core` p. 147 (PDF 149) |
| `pilot-watercraft` | Closed: Hydrofoil; Motorboat; Remote Operation; Sail; Ship; Submarine | `sr5-core` p. 147 (PDF 149) |
| `running` | Hybrid: closed Distance and Sprinting; open terrain | `sr5-core` p. 133 (PDF 135) |
| `swimming` | Closed: Dash; Long Distance | `sr5-core` p. 134 (PDF 136) |
| `animal-handling` | Hybrid: closed Herding, Riding, Training; open animal | `sr5-core` p. 143 (PDF 145) |
| `con` | Closed: Fast Talking; Seduction | `sr5-core` p. 138 (PDF 140) |
| `etiquette` | Open: culture or subculture | `sr5-core` p. 138 (PDF 140) |
| `impersonation` | Closed metahuman types: Dwarf; Elf; Human; Ork; Troll | `sr5-core` p. 138 (PDF 140) |
| `instruction` | Open: Active- or Knowledge-skill category | `sr5-core` p. 138 (PDF 140) |
| `intimidation` | Closed: Interrogation; Mental; Physical; Torture | `sr5-core` p. 139 (PDF 141) |
| `leadership` | Closed: Command; Direct; Inspire; Rally | `sr5-core` p. 139 (PDF 141) |
| `negotiation` | Closed: Bargaining; Contracts; Diplomacy | `sr5-core` p. 139 (PDF 141) |
| `performance` | Open: performance art | `sr5-core` p. 139 (PDF 141) |
| `artisan` | Open: discipline | `sr5-core` p. 143 (PDF 145) |
| `assensing` | Hybrid: closed Aura Reading and Astral Signatures; open aura type | `sr5-core` p. 142 (PDF 144) |
| `disguise` | Closed: Camouflage; Cosmetic; Theatrical; Trideo & Video | `sr5-core` p. 133 (PDF 135) |
| `navigation` | Closed: Augmented Reality Markers; Celestial; Compass; Maps; GPS | `sr5-core` p. 145 (PDF 147) |
| `perception` | Closed: Hearing; Scent; Searching; Taste; Touch; Visual | `sr5-core` p. 133 (PDF 135) |
| `tracking` | Open: terrain type | `sr5-core` p. 134 (PDF 136) |
| `aeronautics-mechanic` | Closed: Aerospace; Fixed Wing; LTA (blimp); Rotary Wing; Tilt Wing; Vector Thrust | `sr5-core` p. 143 (PDF 145) |
| `arcana` | Closed: Spell Design; Focus Design; Spirit Formula | `sr5-core` p. 142 (PDF 144) |
| `armorer` | Closed: Armor; Artillery; Explosives; Firearms; Melee Weapons; Heavy Weapons; Weapon Accessories | `sr5-core` p. 143 (PDF 145) |
| `automotive-mechanic` | Closed: Walker; Hover; Tracked; Wheeled | `sr5-core` p. 144 (PDF 146) |
| `biotechnology` | Closed: Bioinformatics; Bioware; Cloning; Gene Therapy; Vat Maintenance | `sr5-core` p. 144 (PDF 146) |
| `chemistry` | Closed: Analytical; Biochemistry; Inorganic; Organic; Physical | `sr5-core` p. 144 (PDF 146) |
| `computer` | Open: Matrix action | `sr5-core` p. 144 (PDF 146) |
| `cybercombat` | Open: target type | `sr5-core` p. 144 (PDF 146) |
| `cybertechnology` | Closed: Bodyware; Cyberlimbs; Headware; Repair | `sr5-core` p. 144 (PDF 146) |
| `demolitions` | Closed: Commercial Explosives; Defusing; Improvised Explosives; Plastic Explosives | `sr5-core` p. 144 (PDF 146) |
| `electronic-warfare` | Closed: Communications; Encryption; Jamming; Sensor Operations | `sr5-core` p. 144 (PDF 146) |
| `first-aid` | Open: treatment | `sr5-core` p. 144 (PDF 146) |
| `forgery` | Closed: Counterfeiting; Credstick Forgery; False ID; Image Doctoring; Paper Forgery | `sr5-core` p. 144 (PDF 146) |
| `hacking` | Closed: Devices; Files; Hosts; Personas | `sr5-core` p. 145 (PDF 147) |
| `hardware` | Open: hardware type | `sr5-core` p. 145 (PDF 147) |
| `industrial-mechanic` | Closed: Electrical Power Systems; Hydraulics; HVAC; Industrial Robotics; Structural; Welding | `sr5-core` p. 145 (PDF 147) |
| `medicine` | Closed: Cosmetic Surgery; Extended Care; Implant Surgery; Magical Health; Organ Culture; Trauma Surgery | `sr5-core` p. 145 (PDF 147) |
| `nautical-mechanic` | Closed: Motorboat; Sailboat; Ship; Submarine | `sr5-core` p. 145 (PDF 147) |
| `software` | Hybrid: closed Data Bombs; open complex form | `sr5-core` p. 145 (PDF 147) |
| `astral-combat` | Open: specific weapon-focus type or opponent | `sr5-core` p. 142 (PDF 144) |
| `survival` | Open: terrain | `sr5-core` p. 133 (PDF 135) |
| `alchemy` | Hybrid: closed triggers Command, Contact, Time; open spell type | `sr5-core` p. 142 (PDF 144) |
| `artificing` | Hybrid: closed Focus Analysis; open Crafting by focus type | `sr5-core` p. 142 (PDF 144) |
| `banishing` | Open: spirit type | `sr5-core` p. 142 (PDF 144) |
| `binding` | Open: spirit type | `sr5-core` p. 142 (PDF 144) |
| `counterspelling` | Open: spell type | `sr5-core` p. 142 (PDF 144) |
| `disenchanting` | Open: enchantment type | `sr5-core` p. 142 (PDF 144) |
| `ritual-spellcasting` | Open: ritual keyword | `sr5-core` p. 142 (PDF 144) |
| `spellcasting` | Open: spell type | `sr5-core` p. 143 (PDF 145) |
| `summoning` | Open: spirit type | `sr5-core` p. 143 (PDF 145) |
| `compiling` | Open: sprite type | `sr5-core` p. 143 (PDF 145) |
| `decompiling` | Open: sprite type | `sr5-core` p. 143 (PDF 145) |
| `registering` | Open: sprite type | `sr5-core` p. 143 (PDF 145) |

## Skill Groups

Each group is a `selectable` bundle. Buying one group rank gives every member the
same rank. Group rating range at creation is 1-6; Aptitude applies to one skill,
not an entire group. There are exactly 15 groups and 46 memberships: fourteen
groups contain three skills and Engineering contains four. `sr5-core` pp. 88,
129, 153 (PDF 90, 131, 155).

| ID | Display name | Exact members | Count | Source |
| --- | --- | --- | ---: | --- |
| `acting` | Acting | `con`, `impersonation`, `performance` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `athletics` | Athletics | `gymnastics`, `running`, `swimming` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `biotech` | Biotech | `cybertechnology`, `first-aid`, `medicine` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `close-combat` | Close Combat | `blades`, `clubs`, `unarmed-combat` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `conjuring` | Conjuring | `banishing`, `binding`, `summoning` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `cracking` | Cracking | `cybercombat`, `electronic-warfare`, `hacking` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `electronics` | Electronics | `computer`, `hardware`, `software` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `enchanting` | Enchanting | `alchemy`, `artificing`, `disenchanting` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155); `skill.catalog-defects` |
| `engineering` | Engineering | `aeronautics-mechanic`, `automotive-mechanic`, `industrial-mechanic`, `nautical-mechanic` | 4 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `firearms` | Firearms | `automatics`, `longarms`, `pistols` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `influence` | Influence | `etiquette`, `leadership`, `negotiation` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `outdoors` | Outdoors | `navigation`, `survival`, `tracking` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `sorcery` | Sorcery | `counterspelling`, `ritual-spellcasting`, `spellcasting` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `stealth` | Stealth | `disguise`, `palming`, `sneaking` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |
| `tasking` | Tasking | `compiling`, `decompiling`, `registering` | 3 | `sr5-core` pp. 90, 153 (PDF 92, 155) |

## Creation Allocation Rules

### Priority Skill Budgets

| Priority | Individual skill points | Skill-group points | Source |
| --- | ---: | ---: | --- |
| A | 46 | 10 | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| B | 36 | 5 | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| C | 28 | 2 | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| D | 22 | 0 | `sr5-core` pp. 65, 88 (PDF 67, 90) |
| E | 18 | 0 | `sr5-core` pp. 65, 88 (PDF 67, 90) |

- One individual point buys a new skill at 1 or raises one skill by 1. Individual
  points generally buy Active skills, but may also buy Knowledge or Language
  ratings and specializations. One group point buys or raises one whole group by
  1. Individual and group points are not interchangeable. All priority individual
  and group points must be spent. `sr5-core` pp. 88, 101 (PDF 90, 103); decision
  `allocation.unused-priority-points`.
- Step Five cannot duplicate a skill already represented by a purchased group,
  cannot combine individual ranks with group ranks, and cannot break a group.
  A group cannot receive a specialization. `sr5-core` pp. 88-89 (PDF 90-91).
- Step Seven may spend Karma under advancement costs: Active skills cost the
  cumulative sum of `new rating x 2`, Knowledge and Language skills cost the
  cumulative sum of `new rating x 1`, and groups cost the cumulative sum of `new
  rating x 5`. A new specialization costs 7 Karma. The creation maxima still
  apply. `sr5-core` pp. 98, 105-107 (PDF 100, 107-109).
- A specialization requires its parent skill at rating 1 or higher, gives +2 dice
  when applicable, and only one specialization may apply to a test. A new
  character may have at most one specialization per skill. In Step Five it costs
  1 individual skill point. It may instead be bought for 7 Karma in Step Seven.
  `sr5-core` pp. 88-89, 129-130, 107 (PDF 90-91, 131-132, 109).
- Buying a specialization for a member of a group in Step Seven breaks the group,
  as does raising one member independently. The resulting individual skills keep
  the former group rating. Step Five groups remain atomic. A broken group may be
  rebuilt once all member ratings match. This follows the detailed group rule on
  p. 129 over the conflicting p. 89 sentence. `sr5-core` pp. 89, 129 (PDF 91,
  131); decision `skill.group-break-and-rebuild`.

### Priority Grants And Collisions

| Path and priority | Skill grant | Eligible domain | Source |
| --- | --- | --- | --- |
| Magician or Mystic Adept A | two skills at Rating 5 | magical skills permitted to the path | `sr5-core` pp. 65, 68 (PDF 67, 70) |
| Magician or Mystic Adept B | two skills at Rating 4 | magical skills permitted to the path | `sr5-core` pp. 65, 68 (PDF 67, 70) |
| Adept B | one skill at Rating 4 | any eligible Active skill | `sr5-core` pp. 65, 68-69 (PDF 67, 70-71) |
| Adept C | one skill at Rating 2 | any eligible Active skill | `sr5-core` pp. 65, 68-69 (PDF 67, 70-71) |
| Aspected Magician B | one group at Rating 4 | selected Sorcery, Conjuring, or Enchanting group | `sr5-core` pp. 65, 68-69 (PDF 67, 70-71) |
| Aspected Magician C | one group at Rating 2 | selected Sorcery, Conjuring, or Enchanting group | `sr5-core` pp. 65, 68-69 (PDF 67, 70-71) |
| Technomancer A | two skills at Rating 5 | Resonance skills | `sr5-core` p. 65 (PDF 67); decision `technomancer.priority-grants` |
| Technomancer B | two skills at Rating 4 | Resonance skills | `sr5-core` p. 65 (PDF 67); decision `technomancer.priority-grants` |

The grants are already paid for, use no skill points or Karma, and may be raised
later. A granted skill may be raised by another valid allocation. Any duplicate
source combination that would discard grant, individual, or group value is
rejected, and the final natural rating from all sources must remain at 6, or 7
only for the one Aptitude skill. `sr5-core` pp. 68, 88 (PDF 70, 90); decisions
`skill.priority-grant-collision` and `skill.creation-maximum`.

## Knowledge Skills

Knowledge skills are `parameterized` open-authored profiles with required fields
`name`, one closed `category`, and numeric `rating`. A name must represent a
limited subject the character plausibly learned; a subject as broad as `Culture`
is invalid and must be narrowed. Printed examples are guidance, not a fixed
catalog. Knowledge skills do not normally contribute dice to Active-skill tests,
though an Active skill may sometimes substitute for Knowledge at a penalty and
Knowledge never substitutes for an Active skill. `sr5-core` pp. 130, 147-149
(PDF 132, 149-151).

| Category ID | Display name | Class | Linked attribute | Subject rule | Creation eligibility | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `academic` | Academic | parameterized | Logic | Authored formal-education subject, including sciences, technology, magical theory, humanities, cultures, art, or philosophy | rating 1-6 | `sr5-core` pp. 89-91, 148 (PDF 91-93, 150) |
| `interests` | Interests | parameterized | Intuition | Authored hobby, experience, or personal-interest subject; source states no category-content limit | rating 1-6 | `sr5-core` pp. 89-91, 148 (PDF 91-93, 150) |
| `professional` | Professional | parameterized | Logic | Authored trade, profession, or occupation subject | rating 1-6 | `sr5-core` pp. 89-91, 148 (PDF 91-93, 150) |
| `street` | Street | parameterized | Intuition | Authored urban/shadow subject about people, places, organizations, prices, or how street business is done | rating 1-6 | `sr5-core` pp. 89-91, 148 (PDF 91-93, 150) |

- A subject that could fit several categories receives the category most
  appropriate to this character's use of it; the category fixes the linked
  attribute. `sr5-core` p. 91 (PDF 93).
- A Knowledge specialization is an authored narrower subset of the parent
  subject, but not so narrow that it ceases to be useful. The source's Corporate
  Culture example illustrates the rule but creates no closed specialization.
  General creation specialization costs, prerequisite, bonus, and one-per-skill
  limit apply. `sr5-core` pp. 129, 148 (PDF 131, 150).
- Free Knowledge/Language points equal `(natural Intuition + natural Logic) x 2`;
  augmentations do not increase the attributes used for this calculation. One
  point buys one rating or one specialization. The same pool may be split across
  Knowledge and Language skills. Additional individual priority skill points and
  Step Seven Karma may also be used. `sr5-core` pp. 89, 95 (PDF 91, 97).
- Knowledge and Language ratings are each capped at 6 at creation. Drafts may
  leave free points unallocated, but finalization requires all of them to be
  spent; they never convert to another currency. `sr5-core` p. 91 (PDF 93);
  decision `knowledge.unused-free-points`.

## Languages

`language` is one `parameterized` open-authored family. Each selection requires
a specific language name and is a separate profile; the common-language list is
examples, not a catalog. Its linked attribute is Intuition, it defaults, has no
group, and a purchased language has creation rating 1-6. `sr5-core` pp. 88-91,
150 (PDF 90-93, 152).

| ID | Display name | Class | Required fields | Rating | Cost/grant | Source |
| --- | --- | --- | --- | --- | --- | --- |
| `language` | Language | parameterized | authored language name | 1-6 | 1 free Knowledge/Language point per rank, individual priority points, or Step Seven Karma | `sr5-core` pp. 89-91, 150 (PDF 91-93, 152) |
| `native-language` | Native Language | bookkeeping | authored language name | `N`, nonnumeric | one free; Bilingual grants a second | `sr5-core` pp. 89-91 (PDF 91-93) |

- Every character receives exactly one native language at no cost, recorded as
  `N` rather than a numeric rating. Bilingual costs 5 Karma, is available only at
  creation, and grants exactly one second free native language. It does not add
  points to the free Knowledge/Language pool. `sr5-core` pp. 72, 89-91 (PDF 74,
  91-93).
- Language specializations are closed `Read/Write` and `Speak`, plus open-authored
  `dialect` and `lingo`. A lingo is a specialization of a base language; the 2075
  lingo guide is examples, not a closed list. General specialization costs,
  prerequisite, bonus, and one-per-skill creation limit apply. `sr5-core` pp.
  129, 150 (PDF 131, 152).
- Native `N` is not a numeric skill rating. Native-language specializations are
  unavailable during initial creation. Decision `knowledge.native-specialization`.
- Ordinary communication generally requires no test. When a Language test is
  called for, use Language + Intuition; defaulting is allowed. When a Social skill
  is used in a foreign language, Social-skill dice are capped by that Language
  rating. `sr5-core` pp. 150-151 (PDF 152-153).

## Explicit Exclusions And Discrepancies

| Entry or discrepancy | Class | Resolution | Source |
| --- | --- | --- | --- |
| `herding` active skill | excluded | Summary-only defect. Herding is an Animal Handling specialization, not an active skill. | `sr5-core` pp. 90, 143, 151 (PDF 92, 145, 153); `skill.catalog-defects` |
| `lockpicking` active skill | excluded | Summary-only defect. No detail entry exists; Locksmith covers locks. | `sr5-core` pp. 90, 145, 151 (PDF 92, 147, 153); `skill.catalog-defects` |
| `arcane` active skill | excluded | Summary label is a typographic defect; canonical display is Arcana from the detail entry. | `sr5-core` pp. 142, 151 (PDF 144, 153); `skill.catalog-defects` |
| `enchanting` active skill | excluded | Summary label is the group/category; canonical member is Artificing. | `sr5-core` pp. 90, 142, 151, 153 (PDF 92, 144, 153, 155); `skill.catalog-defects` |
| Custom GM-devised active skills | excluded | The core permits GM invention but supplies no deterministic closed construction rules; not part of core character creation catalog. | `sr5-core` p. 147 (PDF 149) |
| Knowledge, language, and lingo examples as catalog entries | excluded | These are examples for open-authored subjects, not closed options. | `sr5-core` pp. 88-91, 147-150 (PDF 90-93, 149-152) |
| `Throwing Weapon` singular | source discrepancy | Use `Throwing Weapons`, matching the creation list and detailed heading; retain singular summary spelling only as provenance. | `sr5-core` pp. 90, 132, 151 (PDF 92, 134, 153) |
| Mechanic names pluralized in linked-attribute summary | source discrepancy | Use singular detailed names: Aeronautics Mechanic, Industrial Mechanic, and Nautical Mechanic. | `sr5-core` pp. 143, 145, 151 (PDF 145, 147, 153) |
| Exotic Melee Weapon detail omission | source discrepancy | Keep the canonical parameterized skill from both lists. Agility and defaulting are supplied by the linked-attribute list; no specialization is invented. | `sr5-core` pp. 90, 130-132, 151 (PDF 92, 132-134, 153) |
| Group reconstruction wording | source discrepancy | p. 89 says specialization breakage can never be reconstructed; p. 129 permits reconstruction at equal member ratings. Apply the approved equal-rating rebuild rule. | `sr5-core` pp. 89, 129 (PDF 91, 131); `skill.group-break-and-rebuild` |

## Review Footer

- Reviewed ranges: `sr5-core` pp. 65, 68-72, 88-92, 95, 98, 101,
  103-107, 128-153 (PDF 67, 70-74, 90-94, 97, 100, 103, 105-109,
  130-155).
- Approved-PDF entry counts by classification: `selectable` 87 (72 active
  skills and 15 groups); `parameterized` 8 (3 active-skill families, 4 Knowledge
  categories, and 1 Language family); `bookkeeping` 1 (Native Language);
  `included-component` 0; `generated` 0; `creation-unavailable` 0; `excluded` 6
  explicit rows/families. Specializations are child facts and are not counted as
  independent entries.
- Inventory checks: exactly 75 canonical active skills; exactly 15 groups;
  exactly 46 group memberships; exactly 4 Knowledge categories; exactly 1
  open-authored Language family plus Native Language bookkeeping.
- Explicit exclusions and source discrepancies: fully listed above. The four
  active-skill identity defects follow decision `skill.catalog-defects`; group
  rebuilding follows `skill.group-break-and-rebuild`; grant collision follows
  `skill.priority-grant-collision`; native specialization follows
  `knowledge.native-specialization`.
- Remaining unknown facts: None.
- Runtime reconciliation status: Not implemented. CHAR-802 must materialize and
  validate this inventory before a runtime count or semantic digest exists.
