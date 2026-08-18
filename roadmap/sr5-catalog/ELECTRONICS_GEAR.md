# SR5 Core Electronics And General Gear Ledger

This is the row-level CHAR-801 review ledger for core electronics, software,
sensors, security, survival, medical gear, toxins, drugs, BTLs, and associated
general gear. It is a review input, not a runtime catalog. Only `sr5-core` and
approved decisions in `../SR5_RULE_DECISIONS.md` were used. Run Faster supplies
no rows in this file.

## Shared Purchase Rules

- `Availability` is the printed numeric value plus `R` (Restricted) or `F`
  (Forbidden); `none` means the table prints a dash/no value. Unless a row says
  otherwise, quantity is one item or one service term, Rating and Capacity are
  not applicable, there is no host/subject parameter, and there are no included
  or generated components.
- `selectable` and `parameterized` rows are creation choices only where their
  numeric Availability is at most 12 and a selected Rating is at most 6.
  Restricted merchandise additionally needs an appropriate license; Forbidden
  merchandise cannot be licensed. `creation-unavailable` rows remain in the
  reviewed core inventory but cannot be bought at creation. Sources:
  `sr5-core` pp. 94, 416-419 (PDF 96, 418-421). Decisions:
  `gear.legality-at-creation`, `gear.rating-cap-force`.
- Costs below are base costs. Apply the approved dwarf `+10%` or troll `+50%`
  gear modifier to applicable purchases. Source: `sr5-core` pp. 65-66, 94
  (PDF 67-68, 96). Decisions: `metatype.dwarf-costs`, `metatype.troll-costs`.
- Device Matrix Condition Monitor is `8 + ceil(Device Rating / 2)` and Matrix
  damage resistance is Device Rating + Firewall. A commlink's Data Processing
  and Firewall equal Device Rating. A commlink/deck PAN supports Device Rating x
  3 slaved devices. Source: `sr5-core` pp. 227-235 (PDF 229-237).
- A `subject` is required, trimmed, non-empty bounded plain text naming the
  applicable skill, area, product type, model, weapon, information domain, or
  license activity. The source supplies the closed type but not a closed list of
  authored subjects.

## Matrix Devices

Every commlink includes AR browsing; phone/radio text and voice; media player;
micro trid projector; touchscreen; video/still camera; image/text/RFID scanner;
GPS; chip and credstick readers; retractable earbuds; voice dialing;
speech/text conversion; and a resistant case. Those are `included-component`
functions, not duplicate purchases. Source: `sr5-core` p. 438 (PDF 440).

### Commlinks And Sim Modules

| ID | Display name | Classification | Device Rating | Qty/unit | Cost | Availability | Included/generated components; effect; creation eligibility | Source |
| --- | --- | --- | ---: | --- | ---: | --- | --- | --- |
| `meta-link` | Meta Link | `selectable` | 1 | one | 100 nuyen | 2 | DP/F 1; common commlink components; eligible. | `sr5-core` p. 439 (PDF 441) |
| `sony-emperor` | Sony Emperor | `selectable` | 2 | one | 700 nuyen | 4 | DP/F 2; common commlink components; eligible. | `sr5-core` p. 439 (PDF 441) |
| `renraku-sensei` | Renraku Sensei | `selectable` | 3 | one | 1,000 nuyen | 6 | DP/F 3; common commlink components; eligible. | `sr5-core` p. 439 (PDF 441) |
| `erika-elite` | Erika Elite | `selectable` | 4 | one | 2,500 nuyen | 8 | DP/F 4; common commlink components; eligible. | `sr5-core` p. 439 (PDF 441) |
| `hermes-ikon` | Hermes Ikon | `selectable` | 5 | one | 3,000 nuyen | 10 | DP/F 5; common commlink components; eligible. | `sr5-core` p. 439 (PDF 441) |
| `transys-avalon` | Transys Avalon | `selectable` | 6 | one | 5,000 nuyen | 12 | DP/F 6; common commlink components; eligible. | `sr5-core` p. 439 (PDF 441) |
| `fairlight-caliban` | Fairlight Caliban | `creation-unavailable` | 7 | one | 8,000 nuyen | 14 | DP/F 7; unavailable by Rating and Availability ceilings. | `sr5-core` p. 439 (PDF 441); `gear.rating-cap-force` |
| `sim-module` | Sim Module | `selectable` | none | one upgrade | +100 nuyen | none | Host: commlink or compatible device; requires DNI (trodes, datajack, or implanted commlink); enables simsense, AR neural signals, and cold-sim VR. | `sr5-core` pp. 438-439 (PDF 440-441) |
| `sim-module-hot-sim` | Sim Module, Hot-Sim Modification | `selectable` | none | one upgrade | +250 nuyen | +4F | Host: sim module; enables hot-sim's +2 Matrix-action dice and 4D6 VR Initiative, makes biofeedback Physical, and carries addiction risk. | `sr5-core` pp. 230, 438-439 (PDF 232, 440-441) |

### Cyberdecks

Each deck includes a sim module, universal data connector, retractable cable,
storage, and configurable Attack/Sleaze/Data Processing/Firewall. Assign the four
array values one-to-one at boot; a Free Action may swap two attributes or one
running/stored program. All Street Gear decks include an illegal hot-sim module.
Program capacity is simultaneous running programs, not storage. Sources:
`sr5-core` pp. 227-228, 439 (PDF 229-230, 441).

| ID | Display name | Classification | DR | Attribute array | Programs | Qty/unit | Cost | Availability | Creation eligibility | Source |
| --- | --- | --- | ---: | --- | ---: | --- | ---: | --- | --- | --- |
| `erika-mcd-1` | Erika MCD-1 | `selectable` | 1 | 4/3/2/1 | 1 | one | 49,500 nuyen | 3R | Eligible; included hot-sim and deck components. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `microdeck-summit` | Microdeck Summit | `selectable` | 1 | 4/3/3/1 | 1 | one | 58,000 nuyen | 3R | Eligible; included hot-sim and deck components. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `microtronica-azteca-200` | Microtronica Azteca 200 | `selectable` | 2 | 5/4/3/2 | 2 | one | 110,250 nuyen | 6R | Eligible; included hot-sim and deck components. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `hermes-chariot` | Hermes Chariot | `selectable` | 2 | 5/4/4/2 | 2 | one | 123,000 nuyen | 6R | Eligible; included hot-sim and deck components. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `novatech-navigator` | Novatech Navigator | `selectable` | 3 | 6/5/4/3 | 3 | one | 205,750 nuyen | 9R (Matrix table: 6R) | Eligible under either printed value; discrepancy retained below. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `renraku-tsurugi` | Renraku Tsurugi | `selectable` | 3 | 6/5/5/3 | 3 | one | 214,125 nuyen | 9R | Eligible; included hot-sim and deck components. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `sony-ciy-720` | Sony CIY-720 | `selectable` | 4 | 7/6/5/4 | 4 | one | 345,000 nuyen | 12R | Eligible; included hot-sim and deck components. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `shiawase-cyber-5` | Shiawase Cyber-5 | `creation-unavailable` | 5 | 8/7/6/5 | 5 | one | 549,375 nuyen | 15R | Availability exceeds 12. | `sr5-core` pp. 227, 439 (PDF 229, 441) |
| `fairlight-excalibur` | Fairlight Excalibur | `creation-unavailable` | 6 | 9/8/7/6 | 6 | one | 823,250 nuyen | 18R | Availability exceeds 12. | `sr5-core` pp. 227, 439 (PDF 229, 441) |

### Rigger Command Consoles

An RCC has fixed DP/Firewall, commlink baseline functions, and DR x 3 slaved-drone
capacity. At boot allocate DR points between Noise Reduction and Sharing; Sharing
is the count of RCC autosofts shared with every slaved drone that runs none of its
own. One Simple Action commands any subset; the RCC also permits direct jumps
between slaved drones. RCC programs are platform-specific and cannot be used on a
deck. Sources: `sr5-core` pp. 266-269 (PDF 268-271).

| ID | Display name | Classification | DR | DP/F | Qty/unit | Cost | Availability | Creation eligibility |
| --- | --- | --- | ---: | --- | --- | ---: | --- | --- |
| `scratch-built-junk` | Scratch-Built Junk | `selectable` | 1 | 3/2 | one | 1,400 nuyen | 2R | Eligible. |
| `radio-shack-remote-controller` | Radio Shack Remote Controller | `selectable` | 2 | 3/3 | one | 8,000 nuyen | 6R | Eligible. |
| `essy-motors-dronemaster` | Essy Motors DroneMaster | `selectable` | 3 | 4/4 | one | 16,000 nuyen | 6R | Eligible. |
| `compuforce-taskmaster` | CompuForce TaskMaster | `selectable` | 4 | 5/4 | one | 32,000 nuyen | 8R | Eligible. |
| `maersk-spider` | Maersk Spider | `selectable` | 4 | 4/5 | one | 34,000 nuyen | 8R | Eligible. |
| `maser-industrial-electronics` | Maser Industrial Electronics | `selectable` | 5 | 3/4 | one | 64,000 nuyen | 8R | Eligible. |
| `vulcan-liegelord` | Vulcan Liegelord | `selectable` | 5 | 5/6 | one | 66,000 nuyen | 10R | Eligible. |
| `proteus-poseidon` | Proteus Poseidon | `selectable` | 5 | 5/6 | one | 68,000 nuyen | 12R | Eligible. |
| `lone-star-remote-commander` | Lone Star Remote Commander | `creation-unavailable` | 6 | 6/5 | one | 75,000 nuyen | 14R | Availability exceeds 12. |
| `mct-drone-web` | MCT Drone Web | `creation-unavailable` | 6 | 7/6 | one | 95,000 nuyen | 16R | Availability exceeds 12. |
| `triox-ubermensch` | Triox UberMensch | `creation-unavailable` | 6 | 8/7 | one | 140,000 nuyen | 18R | Availability exceeds 12. |

Source for every RCC row: `sr5-core` p. 267 (PDF 269).

## Programs And Software

One copy of a cyberprogram occupies one running slot and has no Rating. Duplicate
program types cannot run concurrently. Common programs cost 80 nuyen with no
Availability; hacking programs cost 250 nuyen and print `6R` in Street Gear but
`4R` in the Matrix table. Both are creation-eligible. Deck-only unless the effect
column explicitly lists RCC. Sources: `sr5-core` pp. 243-246, 442
(PDF 245-248, 444).

### Common Cyberprograms

| ID | Display name | Classification | Host | Effect | Source |
| --- | --- | --- | --- | --- | --- |
| `browse` | Browse | `selectable` | deck | Halves Matrix Search time. | `sr5-core` p. 245 (PDF 247) |
| `configurator` | Configurator | `selectable` | deck | Stores one complete alternate attribute/program configuration; while running, reconfigure directly to it. | `sr5-core` p. 245 (PDF 247) |
| `edit` | Edit | `selectable` | deck | +2 Data Processing limit for Edit tests. | `sr5-core` p. 245 (PDF 247) |
| `encryption` | Encryption | `selectable` | deck or RCC-specific copy | +1 Firewall. | `sr5-core` pp. 245, 269 (PDF 247, 271) |
| `signal-scrub` | Signal Scrub | `selectable` | deck or RCC-specific copy | Rating 2 Noise Reduction. | `sr5-core` pp. 245, 269 (PDF 247, 271) |
| `toolbox` | Toolbox | `selectable` | deck or RCC-specific copy | +1 Data Processing. | `sr5-core` pp. 245, 269 (PDF 247, 271) |
| `virtual-machine` | Virtual Machine | `selectable` | deck or RCC-specific copy | +2 program slots; persona takes +1 unresisted Matrix damage whenever it takes Matrix damage. | `sr5-core` pp. 245, 269 (PDF 247, 271) |

### Hacking Cyberprograms

| ID | Display name | Classification | Host | Effect | Source |
| --- | --- | --- | --- | --- | --- |
| `armor-program` | Armor | `selectable` | deck or RCC-specific copy | +2 dice to resist Matrix damage. | `sr5-core` pp. 245, 269 (PDF 247, 271) |
| `baby-monitor` | Baby Monitor | `selectable` | deck | Always displays current Overwatch Score. | `sr5-core` p. 245 (PDF 247) |
| `biofeedback` | Biofeedback | `selectable` | deck | Matrix damage inflicted on biological persona also inflicts equal Stun in cold-sim or Physical in hot-sim; includes failed Attack retaliation; target resists Willpower + Firewall. | `sr5-core` p. 245 (PDF 247) |
| `biofeedback-filter` | Biofeedback Filter | `selectable` | deck or RCC-specific copy | +2 dice to resist biofeedback damage. | `sr5-core` pp. 245, 269 (PDF 247, 271) |
| `blackout` | Blackout | `selectable` | deck | As Biofeedback, but always Stun. | `sr5-core` p. 245 (PDF 247) |
| `decryption` | Decryption | `selectable` | deck | +1 Attack. | `sr5-core` p. 245 (PDF 247) |
| `defuse` | Defuse | `selectable` | deck | +4 dice to resist Data Bomb damage. | `sr5-core` p. 245 (PDF 247) |
| `demolition` | Demolition | `selectable` | deck | +1 Rating to a Data Bomb set while running. | `sr5-core` p. 245 (PDF 247) |
| `exploit` | Exploit | `selectable` | deck | +2 Sleaze for Hack on the Fly. | `sr5-core` p. 245 (PDF 247) |
| `fork` | Fork | `selectable` | deck | One Matrix action targets two icons with one roll; combine target modifiers and resolve defenses separately. | `sr5-core` pp. 245-246 (PDF 247-248) |
| `guard` | Guard | `selectable` | deck or RCC-specific copy | Reduces extra Matrix damage from attacker marks by 1 DV per mark. | `sr5-core` pp. 246, 269 (PDF 248, 271) |
| `hammer` | Hammer | `selectable` | deck | +2 DV when an action causes Matrix damage; not failed-Attack retaliation. | `sr5-core` p. 246 (PDF 248) |
| `lockdown` | Lockdown | `selectable` | deck | Damaged persona is link-locked until this stops or it Jacks Out. | `sr5-core` p. 246 (PDF 248) |
| `mugger` | Mugger | `selectable` | deck | Mark bonus damage increases by 1 DV per mark. | `sr5-core` p. 246 (PDF 248) |
| `shell` | Shell | `selectable` | deck or RCC-specific copy | +1 die against Matrix and biofeedback damage; stacks with similar programs. | `sr5-core` pp. 246, 269 (PDF 248, 271) |
| `sneak-program` | Sneak | `selectable` | deck or RCC-specific copy | +2 dice to defend against Trace Icon; convergence does not reveal physical location. | `sr5-core` pp. 246, 269 (PDF 248, 271) |
| `stealth-program` | Stealth | `selectable` | deck | +1 Sleaze. | `sr5-core` p. 246 (PDF 248) |
| `track` | Track | `selectable` | deck | Either +2 Data Processing for Trace Icon or negates target Sneak's +2 defense, not both. | `sr5-core` p. 246 (PDF 248) |
| `wrapper` | Wrapper | `selectable` | deck or RCC-specific copy | Change Icon may ignore normal iconography; Matrix Perception can reveal truth. | `sr5-core` pp. 246, 269 (PDF 248, 271) |

### Agents And Autosofts

| ID | Display name | Classification | Rating / parameter | Qty/unit | Cost | Availability | Host, effect, and creation eligibility | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `agent` | Agent | `parameterized` | Rating 1-6 | one program | R1-3: Rating x 1,000; R4-6: Rating x 2,000 nuyen | Rating x 3 | One deck slot; own persona; uses host Matrix attributes and Rating for Mental attributes and Computer/Hacking/Cybercombat; may use host programs. Ratings 1-4 eligible; 5-6 exceed Availability 12. | `sr5-core` p. 246 (PDF 248) |
| `clearsight-autosoft` | Clearsight | `parameterized` | Rating 1-6 | one; no subject | Rating x 500 nuyen | Rating x 2 | Drone Perception skill; drone slot or shared from RCC. | `sr5-core` pp. 269-270, 461 (PDF 271-272, 463) |
| `electronic-warfare-autosoft` | Electronic Warfare | `parameterized` | Rating 1-6 | one; no subject | Rating x 500 nuyen | Rating x 2 | Drone Electronic Warfare skill; drone slot or shared from RCC. | `sr5-core` pp. 269, 461 (PDF 271, 463) |
| `evasion-autosoft` | [Model] Evasion | `parameterized` | Rating 1-6; required vehicle/drone model | one per model | Rating x 500 nuyen | Rating x 2 | Teaches that model's autopilot to evade sensor lock; other models cannot use it. | `sr5-core` pp. 269, 461 (PDF 271, 463) |
| `maneuvering-autosoft` | [Model] Maneuvering | `parameterized` | Rating 1-6; required model | one per model | Rating x 500 nuyen | Rating x 2 | Pilot skill for exactly that vehicle/drone model. | `sr5-core` pp. 269, 461 (PDF 271, 463) |
| `stealth-autosoft` | [Model] Stealth | `parameterized` | Rating 1-6; required model | one per model | Rating x 500 nuyen | Rating x 2 | Stealth skill for exactly that model; autonomous drone test is Pilot + Stealth [Handling]. | `sr5-core` pp. 269-270, 461 (PDF 271-272, 463) |
| `targeting-autosoft` | [Weapon] Targeting | `parameterized` | Rating 1-6; required weapon model | one per weapon model | Rating x 500 nuyen | Rating x 2 | Gunnery skill for exactly that weapon model. | `sr5-core` pp. 270, 461 (PDF 272, 463) |

A drone has `ceil(Device Rating / 2)` local autosoft/cyberprogram slots. A drone
running any local program receives no shared RCC autosofts. Sources: `sr5-core`
pp. 269-270 (PDF 271-272).

### Electronic Accessories

| ID | Display name | Classification | DR | Qty/unit | Cost | Availability | Host/capacity; effect | Source |
| --- | --- | --- | ---: | --- | ---: | --- | --- | --- |
| `ar-gloves` | AR Gloves | `selectable` | 3 | one pair | 150 nuyen | none | Commlink/deck accessory; manual AR interaction and tactile force feedback. | `sr5-core` p. 439 (PDF 441) |
| `biometric-reader` | Biometric Reader | `selectable` | 3 | one | 200 nuyen | 4 | Reads fingerprints, retina, voice, tongue, etc., not DNA; may biometric-lock electronics. | `sr5-core` p. 439 (PDF 441) |
| `electronic-paper` | Electronic Paper | `selectable` | 1 | one sheet | 5 nuyen | none | Authored size from note to poster; foldable touchscreen displaying media; wireless write/erase. | `sr5-core` p. 439 (PDF 441) |
| `printer` | Printer | `selectable` | 3 | one | 25 nuyen | none | Full-color hardcopy printer with paper supply. | `sr5-core` p. 439 (PDF 441) |
| `satellite-link` | Satellite Link | `selectable` | 4 | one | 500 nuyen | 6 | Portable dish; Matrix access without local network; distance Noise limited to -5. | `sr5-core` p. 439 (PDF 441) |
| `simrig-electronic` | Simrig | `selectable` | 3 | one | 1,000 nuyen | 12 | Requires working DNI sim module; records wearer's sensory and emotive experience. | `sr5-core` p. 439 (PDF 441) |
| `subvocal-microphone` | Subvocal Microphone | `selectable` | 3 | one | 50 nuyen | 4 | Adhesive throat mic; -4 dice to Perception attempts to overhear subvocal speech. | `sr5-core` p. 439 (PDF 441) |
| `trid-projector` | Trid Projector | `selectable` | 3 | one | 200 nuyen | none | Projects trideo in adjacent 5 m cube. | `sr5-core` p. 439 (PDF 441) |
| `trodes` | Trodes | `selectable` | 3 | one net/cap | 70 nuyen | none | Grants DNI; costs Capacity 2 when installed in headgear. | `sr5-core` p. 439 (PDF 441) |

### RFID And Data Items

| ID | Display name | Classification | DR | Qty/unit | Cost | Availability | Capacity/host; effect | Source |
| --- | --- | --- | ---: | --- | ---: | --- | --- | --- |
| `standard-rfid-tag` | Standard RFID Tags | `selectable` | 1 | 10 tags | 1 nuyen | none | Holds file(s); traceable, editable/erasable; owner may be `nobody`. | `sr5-core` p. 440 (PDF 442) |
| `datachip` | Datachip | `selectable` | 1 | one chip | 5 nuyen | none | Large data storage; no wireless; universal data connector required. | `sr5-core` p. 440 (PDF 442) |
| `security-rfid-tag` | Security Tags | `selectable` | 3 | 10 tags | 5 nuyen | 3 | EMP-hardened against tag eraser; implanted removal is Extended Medicine + Logic [Mental] (10, 1 minute). | `sr5-core` p. 440 (PDF 442) |
| `sensor-rfid-tag` | Sensor Tags | `selectable` | 2 | 10 tags | 40 nuyen | 5 | Hosts one separately purchased single sensor, max Rating 2; stores 24 hours then stops/overwrites; owner can monitor live wirelessly. | `sr5-core` pp. 440, 446 (PDF 442, 448) |
| `stealth-rfid-tag` | Stealth Tags | `selectable` | 3 | 10 tags | 10 nuyen | 7R | Always silent; Sleaze 3; -2 Concealability; may be implanted as security tag. | `sr5-core` p. 440 (PDF 442) |

### Communications And Countermeasures

| ID | Display name | Classification | Rating | Qty/unit | Cost | Availability | Subject/range/effect | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `bug-scanner` | Bug Scanner | `parameterized` | 1-6 | one | Rating x 100 nuyen | Rating R | 20 m; Electronic Warfare + Logic [Rating] versus silent device Logic + Sleaze; any net hit finds/pinpoints; wireless may substitute Rating for skill. | `sr5-core` pp. 440-441 (PDF 442-443) |
| `data-tap` | Data Tap | `selectable` | none | one | 300 nuyen | 6R | Clamp to cable; connected devices gain direct connections to both endpoints; wireless Free Action self-destruct severs cable without damage. | `sr5-core` pp. 440-441 (PDF 442-443) |
| `headjammer` | Headjammer | `parameterized` | 1-6 | one | Rating x 150 nuyen | Rating R | Subject/wearer and augmentations only; noise as jammer. Unauthorized removal: Hardware + Logic or Locksmith + Agility (8, 1 Complex Action) Extended; self-removal Escape Artist + Agility (4). | `sr5-core` p. 441 (PDF 443) |
| `area-jammer` | Jammer, Area | `parameterized` | 1-6 | one | Rating x 200 nuyen | Rating x 3 F | Spherical noise Rating, -1 per 5 m; wireless exempts designated devices/personas. Ratings 1-4 eligible; 5-6 exceed Availability 12. | `sr5-core` p. 441 (PDF 443) |
| `directional-jammer` | Jammer, Directional | `parameterized` | 1-6 | one | Rating x 200 nuyen | Rating x 2 F | 30-degree cone, noise Rating, -1 per 20 m; wireless exemptions. All ratings creation-eligible, Forbidden. | `sr5-core` p. 441 (PDF 443) |
| `micro-transceiver` | Micro-Transceiver | `selectable` | none | one set | 100 nuyen | 2 | Included hard-to-spot earbud and subvocal mic; chosen voice links within 1 km, worldwide wireless. | `sr5-core` p. 441 (PDF 443) |
| `tag-eraser` | Tag Eraser | `selectable` | none | one | 450 nuyen | 6R | At <=5 mm deals 10 Matrix damage to unshielded electronics; one charge; mains recharge 10 seconds, wireless induction 1 hour. | `sr5-core` p. 441 (PDF 443) |
| `white-noise-generator` | White Noise Generator | `parameterized` | 1-6 | one | Rating x 50 nuyen | Rating | Within Rating m, overhearing Perception -Rating; highest only; no video/wireless protection; wireless triples radius. | `sr5-core` p. 441 (PDF 443) |

### Information And Skill Software

| ID | Display name | Classification | Rating / subject | Qty/unit | Cost | Availability | Host/prerequisite/effect | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `datasoft` | Datasoft | `parameterized` | required information/Knowledge subject | one file | 120 nuyen | 4 | Commlink/dataterminal/deck; appropriate subject gives +1 Mental limit on related Knowledge tests. | `sr5-core` pp. 441-442 (PDF 443-444) |
| `mapsoft` | Mapsoft | `parameterized` | required geographic area | one file | 100 nuyen | 4 | Same hosts; route/location/map data; wireless self-updates and reveals location; GM may grant +1 Navigation limit in area. | `sr5-core` pp. 441-442 (PDF 443-444) |
| `shopsoft` | Shopsoft | `parameterized` | required product type | one file | 150 nuyen | 4 | Same hosts; +1 Social limit for Availability and Negotiation tests buying/selling that product type. | `sr5-core` pp. 441-442 (PDF 443-444) |
| `tutorsoft` | Tutorsoft | `parameterized` | Rating 1-6; required non-Magic/non-Resonance skill | one file | Rating x 400 nuyen | Rating | Makes Instruction tests with Rating x 2 dice; cannot teach Magic/Resonance skills. | `sr5-core` pp. 441-442 (PDF 443-444) |
| `activesoft` | Activesoft | `parameterized` | Rating 1-6; required physical Active skill | one skillsoft | Rating x 5,000 nuyen | 8 | Requires skillwires and skilljack limits; substitutes Rating for eligible non-Magic/non-Resonance physical skill; no Edge. | `sr5-core` p. 442 (PDF 444) |
| `knowsoft` | Knowsoft | `parameterized` | Rating 1-6; required Knowledge subject | one skillsoft | Rating x 2,000 nuyen | 4 | Requires skilljack; substitutes Rating for Knowledge skill; no Edge. | `sr5-core` p. 442 (PDF 444) |
| `linguasoft` | Linguasoft | `parameterized` | Rating 1-6; required language | one skillsoft | Rating x 1,000 nuyen | 2 | Requires skilljack; substitutes Rating for language skill; no Edge and does not create native language. | `sr5-core` p. 442 (PDF 444) |

## Credit, Identity, And Tools

Detailed SIN ownership, verification, burn state, fake-license attachment, and
identity finalization belong in `VEHICLES_RESOURCES.md`. These rows are the
merchandise and carrying-capacity facts needed by this ledger. Decision:
`identity.fake-license-link`.

| ID | Display name | Classification | Rating / parameter | Qty/unit | Cost | Availability | Capacity/effect/creation eligibility | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `standard-credstick` | Certified Credstick, Standard | `selectable` | none | one | 5 nuyen | none | Max balance 5,000 nuyen; bearer instrument; no wireless; balance is `bookkeeping`. | `sr5-core` p. 442 (PDF 444) |
| `silver-credstick` | Certified Credstick, Silver | `selectable` | none | one | 20 nuyen | none | Max balance 20,000 nuyen; same bearer rules. | `sr5-core` p. 442 (PDF 444) |
| `gold-credstick` | Certified Credstick, Gold | `selectable` | none | one | 100 nuyen | 5 | Max balance 100,000 nuyen; same bearer rules. | `sr5-core` p. 442 (PDF 444) |
| `platinum-credstick` | Certified Credstick, Platinum | `selectable` | none | one | 500 nuyen | 10 | Max balance 500,000 nuyen; same bearer rules. | `sr5-core` p. 442 (PDF 444) |
| `ebony-credstick` | Certified Credstick, Ebony | `creation-unavailable` | none | one | 1,000 nuyen | 20 | Max balance 1,000,000 nuyen; Availability exceeds 12. | `sr5-core` p. 442 (PDF 444) |
| `credit-account` | Credit Account | `bookkeeping` | required SIN/account identity | one account | included with Low+ lifestyle | not applicable | Online, passcode/biometric, traceable; registered to a SIN unless underworld banking; not a separate shop purchase when lifestyle includes it. | `sr5-core` p. 442 (PDF 444) |
| `fake-sin` | Fake SIN | `parameterized` | Rating 1-6; required authored identity | one identity | Rating x 2,500 nuyen | Rating x 3 F | Digital identity; ratings 1-4 eligible, 5-6 exceed Availability 12. Generates identity record; no physical item. Detailed profiles/checking in `VEHICLES_RESOURCES.md`. | `sr5-core` pp. 367-368, 442-443 (PDF 369-370, 444-445) |
| `fake-license` | Fake License | `parameterized` | Rating 1-6; required parent fake-SIN ID and one R item/activity subject | one license | Rating x 200 nuyen | Rating x 3 F | Attached to exactly one fake SIN; Forbidden subjects invalid. Ratings 1-4 eligible, 5-6 exceed Availability 12. | `sr5-core` pp. 367, 443 (PDF 369, 445); `identity.fake-license-link` |
| `tool-kit` | Kit | `parameterized` | required skill | one portable kit | 500 nuyen | none | Basic tools for standard repairs/tasks for one skill. | `sr5-core` p. 443 (PDF 445) |
| `tool-shop` | Shop | `parameterized` | required skill | one van-transportable shop | 5,000 nuyen | 8 | Advanced tools and standard spare parts for one skill. | `sr5-core` p. 443 (PDF 445) |
| `tool-facility` | Facility | `parameterized` | required skill | one immobile facility | 50,000 nuyen | 12 | Building-scale advanced construction/modification tools and standard spare parts for one skill. | `sr5-core` p. 443 (PDF 445) |

## Optical, Imaging, And Audio

Electronic device Capacity hosts enhancements; enhancement cost and Availability
are additive (`+`) to the host. Optical glass devices can establish spell LOS
from cover at -3 spellcasting dice but cannot take electronic enhancements.
Sources: `sr5-core` pp. 443-445 (PDF 445-447).

### Optical And Imaging Devices

| ID | Display name | Classification | Capacity / parameter | Qty/unit | Cost | Availability | Included/effect | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `electronic-binoculars` | Binoculars | `parameterized` | Capacity 1-3 | one | Capacity x 50 nuyen | none | Electronic; includes vision magnification; hosts vision enhancements. | `sr5-core` pp. 443-444 (PDF 445-446) |
| `optical-binoculars` | Binoculars, Optical | `selectable` | no Capacity | one | 50 nuyen | none | Includes optical magnification; valid optical LOS; no enhancements. | `sr5-core` pp. 443-444 (PDF 445-446) |
| `camera` | Camera | `parameterized` | Capacity 1-6 | one | Capacity x 100 nuyen | none | Records still/video/trideo with sound; hosts vision and audio enhancements. | `sr5-core` p. 443 (PDF 445) |
| `micro-camera` | Micro-Camera | `selectable` | Capacity 1 | one | 100 nuyen | none | Camera functions; hosts one Capacity point. | `sr5-core` p. 443 (PDF 445) |
| `contacts` | Contacts | `parameterized` | Capacity 1-3 | one pair | Capacity x 200 nuyen | 6 | Wireless-only display host worn on eyes. | `sr5-core` p. 443 (PDF 445) |
| `glasses` | Glasses | `parameterized` | Capacity 1-4 | one | Capacity x 100 nuyen | none | Worn display/vision enhancement host. | `sr5-core` p. 443 (PDF 445) |
| `goggles` | Goggles | `parameterized` | Capacity 1-6 | one | Capacity x 50 nuyen | none | Strapped display/vision enhancement host. | `sr5-core` pp. 443-444 (PDF 445-446) |
| `endoscope` | Endoscope | `selectable` | optical; authored length >=1 m | one | 250 nuyen | 8 | Fiber-optic/myomeric ends; sees around corners/under doors; valid optical LOS; no enhancements. | `sr5-core` p. 444 (PDF 446) |
| `imaging-scope` | Imaging Scope | `selectable` | Capacity 3 | one | 300 nuyen | 2 | Electronic weapon-mounted vision/display host. | `sr5-core` pp. 431, 443-444 (PDF 433, 445-446) |
| `periscope` | Periscope | `selectable` | optical | one | 50 nuyen | 3 | Look, shoot, or cast around corners; valid optical LOS; no enhancements. | `sr5-core` p. 444 (PDF 446) |
| `mage-sight-goggles` | Mage Sight Goggles | `selectable` | optical; length 10/20/30 m | one | 3,000 nuyen | 12R | Fiber optic in remotely shaped myomeric rope; valid optical LOS; no enhancements. | `sr5-core` p. 444 (PDF 446) |
| `monocle` | Monocle | `parameterized` | Capacity 1-4 | one | 3,000 nuyen | 12R | Flip-down electronic display/vision enhancement host. | `sr5-core` pp. 443-444 (PDF 445-446) |

### Vision Enhancements

| ID | Display name | Classification | Rating / Capacity | Qty/unit | Cost | Availability | Effect | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `low-light-vision-enhancement` | Low-Light Vision | `selectable` | [1] | one install | +500 nuyen | +4 | Normal vision down to starlight, not total darkness. | `sr5-core` p. 444 (PDF 446) |
| `flare-compensation-enhancement` | Flare Compensation | `selectable` | [1] | one install | +250 nuyen | +1 | Mitigates glare and flashing-light vision modifiers. | `sr5-core` p. 444 (PDF 446) |
| `image-link-enhancement` | Image Link | `selectable` | [1] | one install | +25 nuyen | none | Displays visual data/AR in field of vision. | `sr5-core` p. 444 (PDF 446) |
| `smartlink-enhancement` | Smartlink | `selectable` | [1] | one install | +2,000 nuyen | +4R | Receives smartgun range/ammo/status data and enables external smartlink benefit; implanted smartlinks are stronger. | `sr5-core` pp. 433, 444 (PDF 435, 446) |
| `thermographic-vision-enhancement` | Thermographic Vision | `selectable` | [1] | one install | +500 nuyen | +6 | Infrared heat-pattern vision, including living targets in darkness. | `sr5-core` p. 444 (PDF 446) |
| `vision-enhancement` | Vision Enhancement | `parameterized` | Rating 1-3; [Rating] | one install | +(Rating x 500) nuyen | +Rating x 2 | +Rating visual Perception limit; wireless also +Rating dice. | `sr5-core` p. 444 (PDF 446) |
| `vision-magnification-enhancement` | Vision Magnification | `selectable` | [1] | one install | +250 nuyen | +2 | Digital zoom to 50x; applies ranged-combat magnification rules. | `sr5-core` pp. 177, 444 (PDF 179, 446) |

### Audio Devices And Enhancements

| ID | Display name | Classification | Capacity / parameter | Qty/unit | Cost | Availability | Effect/host restriction | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `directional-microphone` | Directional Microphone | `parameterized` | Capacity 1-6 | one | Capacity x 50 nuyen | 4 | Effective listening position up to 100 m closer along clear line. | `sr5-core` p. 445 (PDF 447) |
| `earbuds` | Ear Buds | `parameterized` | Capacity 1-3 | one pair | Capacity x 50 nuyen | none | Concealable audio output/enhancement host. | `sr5-core` p. 445 (PDF 447) |
| `headphones` | Headphones | `parameterized` | Capacity 1-6 | one | Capacity x 50 nuyen | none | Audio output/enhancement host. | `sr5-core` p. 445 (PDF 447) |
| `laser-microphone` | Laser Microphone | `parameterized` | Capacity 1-6 | one | Capacity x 100 nuyen | 6R | Reads surface vibration at max 100 m; cannot host spatial recognizer. | `sr5-core` p. 445 (PDF 447) |
| `omnidirectional-microphone` | Omni-Directional Microphone | `parameterized` | Capacity 1-6; form standard or micro | one | Capacity x 50 nuyen | none | Pickup/recorder; micro form requires Capacity 1 and max range 5 m. | `sr5-core` p. 445 (PDF 447) |
| `audio-enhancement` | Audio Enhancement | `parameterized` | Rating 1-3; [Rating] | one install | +(Rating x 500) nuyen | +Rating x 2 | +Rating audio Perception limit; wireless also +Rating dice. | `sr5-core` p. 445 (PDF 447) |
| `select-sound-filter` | Select Sound Filter | `parameterized` | Rating 1-3; [Rating] | one install | +(Rating x 250) nuyen | +Rating x 3 | Tracks Rating sound groups; actively isolates one and may record/trigger on others. | `sr5-core` p. 445 (PDF 447) |
| `spatial-recognizer` | Spatial Recognizer | `selectable` | [2] | one install | +1,000 nuyen | +4 | +2 Perception limit locating sound source; wireless also +2 dice; not in laser mic. | `sr5-core` p. 445 (PDF 447) |

## Sensors

A sensor package requires a compatible housing. Sensor array supports up to eight
functions despite costing [6] host Capacity; a single sensor supports exactly one.
For array Perception, Electronic Warfare may replace Perception and Sensor Rating
may be the limit. Ratings 7-8 exist in the source but are unavailable at creation
under `gear.rating-cap-force`. Source: `sr5-core` pp. 445-446 (PDF 447-448).

### Housings And Packages

| ID | Display name | Classification | Capacity / Rating | Qty/unit | Cost | Availability | Host/function parameter and eligibility | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `handheld-sensor-housing` | Handheld Housing | `parameterized` | Capacity 1-3 | one | Capacity x 100 nuyen | none | Hosts packages totaling Capacity; max sensor Rating 3. | `sr5-core` p. 446 (PDF 448) |
| `wall-mounted-sensor-housing` | Wall-Mounted Housing | `parameterized` | Capacity 1-6 | one | Capacity x 250 nuyen | none | Hosts packages totaling Capacity; max sensor Rating 4. | `sr5-core` p. 446 (PDF 448) |
| `sensor-array` | Sensor Array | `parameterized` | Rating 2-8; [6] | one | Rating x 1,000 nuyen | 7 | Requires housing supporting Rating and [6]; choose <=8 distinct function IDs. Ratings 2-6 eligible; 7-8 creation-unavailable variants. | `sr5-core` pp. 445-446 (PDF 447-448) |
| `single-sensor` | Single Sensor | `parameterized` | Rating 2-8; [1] | one | Rating x 100 nuyen | 5 | Requires housing supporting Rating and [1]; choose exactly one function. Ratings 2-6 eligible; 7-8 creation-unavailable variants. | `sr5-core` pp. 445-446 (PDF 447-448) |

Other host maxima are RFID/audio/visual/headware 2; small-or-smaller drone 3;
medium drone 4; large drone/cyberlimb 5; motorcycle 6; larger vehicle 7; and
building/airport 8. Source: `sr5-core` p. 446 (PDF 448).

### Sensor Functions

Each row is an `included-component` function selected inside a purchased sensor
package, has the package Rating, no separate cost/Availability/quantity, and is
not independently purchasable.

| ID | Display name | Range | Function/effect | Source |
| --- | --- | --- | --- | --- |
| `sensor-function.atmosphere` | Atmosphere Sensor | not limited | Current local atmospheric/weather analysis. | `sr5-core` p. 446 (PDF 448) |
| `sensor-function.camera` | Camera | not limited | Camera function; Capacity equal to Rating for enhancement hosting. | `sr5-core` pp. 443, 446 (PDF 445, 448) |
| `sensor-function.cyberware-scanner` | Cyberware Scanner | 15 m | Roll Rating; thresholds: standard ware/weapons 1, alphaware/other 2, betaware 3, deltaware 5+; +1/+2/+3 threshold for 2+/4+/6+ items; threshold gives type/location, extra hits detail. | `sr5-core` pp. 366, 446 (PDF 368, 448) |
| `sensor-function.directional-microphone` | Directional Microphone | not limited | As audio device; Capacity equal to Rating. | `sr5-core` pp. 445-446 (PDF 447-448) |
| `sensor-function.geiger-counter` | Geiger Counter | not limited | Measures surrounding radioactivity. | `sr5-core` p. 446 (PDF 448) |
| `sensor-function.laser-microphone` | Laser Microphone | 100 m | As audio device; Capacity equal to Rating; no spatial recognizer. | `sr5-core` pp. 445-446 (PDF 447-448) |
| `sensor-function.laser-range-finder` | Laser Range Finder | 1,000 m | Calculates exact distance to reflected target. | `sr5-core` p. 446 (PDF 448) |
| `sensor-function.mad-scanner` | MAD Scanner | 5 m | Rating dice; one hit detects ferrous metal/weapons; not wood/stone/plastic. | `sr5-core` pp. 366, 446 (PDF 368, 448) |
| `sensor-function.motion-sensor` | Motion Sensor | 25 m | Ultrasound/infrared detection of motion and drastic ambient-temperature change. | `sr5-core` p. 446 (PDF 448) |
| `sensor-function.olfactory-scanner` | Olfactory Scanner | not limited | Rating dice versus threshold 3 to identify a scent (2 with tailored pheromones); distinguishes metahuman/animal and gender, not individual. | `sr5-core` pp. 365-366, 446 (PDF 367-368, 448) |
| `sensor-function.omnidirectional-microphone` | Omni-Directional Microphone | not limited | As audio device; Capacity equal to Rating. | `sr5-core` pp. 445-446 (PDF 447-448) |
| `sensor-function.radio-signal-scanner` | Radio Signal Scanner | 20 m | Functions as bug scanner using package Rating. | `sr5-core` pp. 440, 446 (PDF 442, 448) |
| `sensor-function.ultrasound` | Ultrasound | 50 m | Active topographic sonar sees textures/distances/invisible forms but not color/brightness and not through glass; passive mode only receives outside ultrasound. | `sr5-core` p. 446 (PDF 448) |
| `sensor-function.vision-magnification` | Vision Magnification | not limited | Digital zoom up to 50x with clear LOS; ranged-combat magnification rules. | `sr5-core` pp. 178, 446 (PDF 180, 448) |

## Security, Restraints, And Breaking And Entering

| ID | Display name | Classification | Rating / parameter | Qty/unit | Cost | Availability | Capacity/effect/creation eligibility | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `key-combination-lock` | Key/Combination Lock | `parameterized` | Rating 1-6 | one | Rating x 10 nuyen | Rating | Locksmith + Agility (Rating, 1 CT) Extended; autopicker adds dice or substitutes for skill. | `sr5-core` pp. 363, 447 (PDF 365, 449) |
| `maglock` | Maglock | `parameterized` | Rating 1-6 at creation | one | Rating x 100 nuyen | Rating | Case: Locksmith + Agility (Rating x 2, 1 CT) Extended or Barrier Rating; supports access add-ons/network. | `sr5-core` pp. 363, 447 (PDF 365, 449) |
| `maglock-keypad-card-reader` | Keypad or Card Reader | `parameterized` | required host maglock; subtype keypad/card | one add-on | +50 nuyen | none | Keypad rewiring repeats Rating x 2 Extended after case; card uses key/passkey/copy rules. | `sr5-core` pp. 363-364, 447 (PDF 365-366, 449) |
| `maglock-anti-tamper` | Anti-Tamper Circuits | `parameterized` | Rating 1-4; host maglock | one add-on | +(Rating x 250) nuyen | +Rating | Extra Locksmith + Agility (Rating) test; failure alarms. | `sr5-core` pp. 363, 447 (PDF 365, 449) |
| `maglock-biometric-reader` | Biometric Reader | `parameterized` | host maglock; biometric subject | one add-on | +200 nuyen | +4 | Validates selected biometric type; spoofing uses applicable duplicate/molder. | `sr5-core` pp. 364, 447 (PDF 366, 449) |
| `metal-restraints` | Metal Restraints | `selectable` | Armor 16, Structure 2 | one set | 20 nuyen | none | Mechanical or wireless lock. | `sr5-core` p. 447 (PDF 449) |
| `plasteel-restraints` | Plasteel Restraints | `selectable` | Armor 20, Structure 2 | one set | 50 nuyen | 6R | Flash-fused; subject must be cut free. | `sr5-core` p. 447 (PDF 449) |
| `plastic-restraints` | Plastic Restraints | `selectable` | Armor 6, Structure 1 | 10 straps | 5 nuyen | none | Disposable lightweight restraints. | `sr5-core` p. 447 (PDF 449) |
| `containment-manacles` | Containment Manacles | `selectable` | Armor 16, Structure 2 | one set | 250 nuyen | 6R | Wrists/ankles; limits movement to shuffle and blocks extending cyber-implant weapon. | `sr5-core` p. 447 (PDF 449) |
| `autopicker` | Autopicker | `parameterized` | Rating 1-6 | one | Rating x 500 nuyen | 8R | +Rating lockpicking limit; wireless +Rating dice; may substitute Rating for Locksmith on key locks. | `sr5-core` pp. 363, 447 (PDF 365, 449) |
| `cellular-glove-molder` | Cellular Glove Molder | `parameterized` | Rating 1-4 | one | Rating x 500 nuyen | 12F | Captures finger/palm print and makes wearable biometric sleeve. | `sr5-core` p. 447 (PDF 449) |
| `chisel-crowbar` | Chisel/Crowbar | `selectable` | none | one | 20 nuyen | none | Doubles effective Strength forcing a door/container. | `sr5-core` p. 447 (PDF 449) |
| `keycard-copier` | Keycard Copier | `parameterized` | Rating 1-6 | one | Rating x 600 nuyen | 8F | Copies stolen keycard; Hardware kit, 10 minutes, Hardware + Logic (2) creates card; forged card rolls Rating x 2 vs maglock x 2. | `sr5-core` pp. 447-448 (PDF 449-450) |
| `lockpick-set` | Lockpick Set | `selectable` | none | one set | 250 nuyen | 4R | Required manual mechanical lock-picking tools. | `sr5-core` pp. 363, 448 (PDF 365, 450) |
| `maglock-passkey` | Maglock Passkey | `parameterized` | Rating 1-4 | one | Rating x 2,000 nuyen | Rating x 3 F | Cardreader bypass without opening case; wireless +1 effective Rating. | `sr5-core` pp. 363-364, 448 (PDF 365-366, 450) |
| `miniwelder` | Miniwelder | `selectable` | DV 25 vs barriers | one | 250 nuyen | 2 | Cuts/welds metal for 30 minutes per fuel supply; unsuitable weapon. | `sr5-core` p. 448 (PDF 450) |
| `miniwelder-fuel-canister` | Miniwelder Fuel Canister | `selectable` | none | one canister | 80 nuyen | 2 | Replacement 30-minute power/fuel supply for miniwelder. | `sr5-core` p. 448 (PDF 450) |
| `monofilament-chainsaw` | Monofilament Chainsaw | `selectable` | Accuracy 3; Reach 1; 8P; AP -6 | one | 500 nuyen | 8 | Exotic Melee Weapon; doubles DV against barriers. | `sr5-core` p. 448 (PDF 450) |
| `sequencer` | Sequencer | `parameterized` | Rating 1-6 | one | Rating x 250 nuyen | Rating x 3 F | Requires opened keypad-maglock case; opposed Rating vs maglock Rating; wireless +1 Rating. Ratings 1-4 eligible. | `sr5-core` pp. 363, 448 (PDF 365, 450) |

## Industrial, Survival, And Grappling Gear

| ID | Display name | Classification | Rating / quantity | Cost | Availability | Included/effect/creation eligibility | Source |
| --- | --- | --- | --- | ---: | --- | --- | --- |
| `glue-solvent` | Glue Solvent | `selectable` | one spray; about 1 m2 | 90 nuyen | 2 | Dissolves fast-drying aerosol superglue. | `sr5-core` p. 448 (PDF 450) |
| `glue-sprayer` | Glue Sprayer | `selectable` | one spray; about 1 m2 | 150 nuyen | 2 | Hardens in 1 CT; opposed Body + Strength, both Rating 5, to force. | `sr5-core` p. 448 (PDF 450) |
| `thermite-burning-bar` | Thermite Burning Bar | `creation-unavailable` | one bar; 30P Fire | 500 nuyen | 16R | Melts iron/steel/plasteel; setup prevents normal weapon use; wireless activation; Availability exceeds 12. | `sr5-core` p. 448 (PDF 450) |
| `chemsuit` | Chemsuit | `parameterized` | Rating 1-6; one suit | Rating x 150 nuyen | Rating x 2 | Chemical Protection Rating; not sealed; does not stack with armor chemical protection (use higher). | `sr5-core` pp. 437, 448-449 (PDF 439, 450-451) |
| `climbing-gear` | Climbing Gear | `selectable` | one kit | 200 nuyen | none | Backpack, 400 kg rope, harness, gloves, carabiners, crampons for assisted climbing. | `sr5-core` pp. 134, 448 (PDF 136, 450) |
| `diving-gear` | Diving Gear | `selectable` | one set; 2 hours air | 2,000 nuyen | 6 | Suit/mask/snorkel/regulator/tank/vest; inhalation immunity while active; Cold Resistance 1. | `sr5-core` p. 448 (PDF 450) |
| `flashlight` | Flashlight | `parameterized` | normal, low-light, or infrared | 25 nuyen | none | Reduces matching visibility modifiers; may use top/underbarrel weapon mount. | `sr5-core` p. 449 (PDF 451) |
| `gas-mask` | Gas Mask | `selectable` | one; 1 hour air | 200 nuyen | none | Inhalation-toxin immunity; cannot combine respirator; larger tanks attach; wireless air analysis. | `sr5-core` pp. 408, 449 (PDF 410, 451) |
| `gas-mask-air-supply` | Gas Mask Replacement Air Supply | `selectable` | one; 1 hour air | 40 nuyen | none | Replacement clean-air supply for gas mask. | `sr5-core` p. 449 (PDF 451) |
| `gecko-tape-gloves` | Gecko Tape Gloves | `selectable` | one glove/kneepad/sole set | 250 nuyen | 12 | Assisted climbing; ineffective wet; wireless neutralizes adhesion. | `sr5-core` pp. 134, 449 (PDF 136, 451) |
| `hazmat-suit` | Hazmat Suit | `selectable` | one suit; 4 hours air | 3,000 nuyen | 8 | Chemical seal grants contact/inhalation immunity while intact; many include a Geiger counter, but source does not state universal inclusion; wireless environment analysis. | `sr5-core` pp. 408, 437, 449 (PDF 410, 439, 451) |
| `light-stick` | Light Stick | `selectable` | one; 3 hours | 25 nuyen | none | Soft chemical light in 10 m radius. | `sr5-core` p. 449 (PDF 451) |
| `magnesium-torch` | Magnesium Torch | `selectable` | one; 5 minutes | 5 nuyen | none | Bright torchlight. | `sr5-core` p. 449 (PDF 451) |
| `micro-flare-launcher` | Micro Flare Launcher | `selectable` | Acc 3; 5P Fire; AP -5; SS; ammo 1 | 175 nuyen | none | Exotic Ranged Weapon; 200 m altitude, city-block illumination, negates poor/low light. | `sr5-core` pp. 449-450 (PDF 451-452) |
| `micro-flares` | Micro Flares | `selectable` | one flare | 25 nuyen | none | Ammunition for micro flare launcher. | `sr5-core` p. 449 (PDF 451) |
| `rappelling-gloves` | Rappelling Gloves | `selectable` | one pair | 50 nuyen | none | +2 dice to hold grapple line; required to handle microwire safely. | `sr5-core` p. 449 (PDF 451) |
| `respirator` | Respirator | `parameterized` | Rating 1-6; one | Rating x 50 nuyen | none | +Rating Toxin Resistance against inhalation vector; cannot combine gas mask. | `sr5-core` pp. 408, 449 (PDF 410, 451) |
| `survival-kit` | Survival Kit | `selectable` | one kit | 200 nuyen | 4 | Includes knife, lighter, matches, compass, thermal blanket, several days' ration bars, water purifier, and survival miscellany. | `sr5-core` p. 449 (PDF 451) |
| `grapple-gun` | Grapple Gun | `selectable` | Acc 3; 7S; AP -2; SS; ammo 1 | 500 nuyen | 8R | Exotic Ranged Weapon; Light Crossbow ranges; included internal winch; rope/hook purchased separately. | `sr5-core` pp. 449-450 (PDF 451-452) |
| `catalyst-stick` | Catalyst Stick | `selectable` | one reusable stick | 120 nuyen | 8F | Turns stealth rope to near-traceless dust in seconds. | `sr5-core` pp. 449-450 (PDF 451-452) |
| `microwire` | Microwire | `selectable` | per 100 m; supports 100 kg | 50 nuyen | 4 | Nearly invisible; without rappelling gloves inflicts 8P AP -8 when grabbed/slid. | `sr5-core` pp. 449-450 (PDF 451-452) |
| `myomeric-rope` | Myomeric Rope | `selectable` | per 10 m; max controlled length 30 m | 200 nuyen | 10 | Remote shape/movement at 2 m per CT. | `sr5-core` pp. 449-450 (PDF 451-452) |
| `standard-rope` | Standard Rope | `selectable` | per 100 m; supports 400 kg | 50 nuyen | none | Standard grapple/climbing line. | `sr5-core` pp. 449-450 (PDF 451-452) |
| `stealth-rope` | Stealth Rope | `selectable` | per 100 m; supports 400 kg | 85 nuyen | 8F | Catalyst-compatible low-trace rope. | `sr5-core` pp. 449-450 (PDF 451-452) |

## Biotech, Medical Services, And Patches

| ID | Display name | Classification | Rating / term | Qty/unit | Cost | Availability | Included/effect/creation eligibility | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `biomonitor` | Biomonitor | `selectable` | none | one | 300 nuyen | 3 | Measures vital signs and analyzes blood/sweat/skin; wireless sharing and ambulance auto-alert. | `sr5-core` p. 450 (PDF 452) |
| `disposable-syringe` | Disposable Syringe | `selectable` | none | one use | 10 nuyen | 3 | Delivers injection-vector dose; unwilling subject generally immobilized/grappled. | `sr5-core` pp. 408, 450 (PDF 410, 452) |
| `medkit` | Medkit | `parameterized` | Rating 1-6 | one | Rating x 250 nuyen | Rating | +Rating First Aid limit; R1-3 pocket, R4-6 case; Rating uses before supplies; wireless +Rating dice or autonomous Rating x 2 [Rating]. | `sr5-core` p. 450 (PDF 452) |
| `medkit-supplies` | Medkit Supplies | `selectable` | none | one restock | 100 nuyen | none | Restocks a medkit after its Rating uses. | `sr5-core` p. 450 (PDF 452) |
| `docwagon-basic` | DocWagon Contract, Basic | `selectable` | 1 year | one contract | 5,000 nuyen/year | none | Included tissue filing and biomonitor RFID implant/wristband; <10 minute armed-team guarantee where service permitted; resuscitation 5,000 and HTR 5,000 plus 20,000 per employee death. | `sr5-core` p. 450 (PDF 452) |
| `docwagon-gold` | DocWagon Contract, Gold | `selectable` | 1 year | one contract | 25,000 nuyen/year | none | Basic inclusions; 1 free resuscitation/year, HTR 50% off, extended care 10% off; death compensation remains. | `sr5-core` p. 450 (PDF 452) |
| `docwagon-platinum` | DocWagon Contract, Platinum | `selectable` | 1 year | one contract | 50,000 nuyen/year | none | Basic inclusions; 4 free resuscitations/year, no HTR charge, extended care 50% off; death compensation remains. | `sr5-core` p. 450 (PDF 452) |
| `docwagon-super-platinum` | DocWagon Contract, Super-Platinum | `selectable` | 1 year | one contract | 100,000 nuyen/year | none | Basic inclusions; 5 free resuscitations/year; no HTR or employee death compensation. | `sr5-core` p. 450 (PDF 452) |
| `antidote-patch` | Antidote Patch | `parameterized` | Rating 1-6 | one patch | Rating x 50 nuyen | Rating | +Rating to toxin resistance tests made within 20 minutes after application; must beat toxin timing. | `sr5-core` p. 451 (PDF 453) |
| `chem-patch` | Chem Patch | `selectable` | none | one patch | 200 nuyen | 6 | Required one separately purchased chemical/toxin dose loaded before later dermal administration. | `sr5-core` p. 451 (PDF 453) |
| `stim-patch` | Stim Patch | `parameterized` | Rating 1-6 | one patch | Rating x 25 nuyen | Rating x 2 | Temporarily removes Rating Stun for Rating x 10 minutes; then Rating + 1 unresisted Stun; cannot rest; Addiction 2, threshold 1. | `sr5-core` p. 451 (PDF 453) |
| `tranq-patch` | Tranq Patch | `parameterized` | Rating 1-10 | one patch | Rating x 20 nuyen | Rating x 2 | Inflicts Rating Stun resisted only by Body. Ratings 1-6 eligible; 7-10 exceed Rating cap (and 7+ Availability cap). | `sr5-core` p. 451 (PDF 453); `gear.rating-cap-force` |
| `trauma-patch` | Trauma Patch | `selectable` | none | one patch | 500 nuyen | 6 | Dying patient immediately stabilizes using Body instead of First Aid/Medicine; wireless automatically stabilizes. | `sr5-core` p. 451 (PDF 453) |

An unwilling slap-patch subject requires a successful no-damage melee attack and
exposed skin. Source: `sr5-core` p. 451 (PDF 453).

## Toxins

Each row is one dose. At Speed, resist with Body + Willpower + protection; each
hit reduces Power by 1, and Power 0 prevents all effects. Each simultaneous extra
dose adds +1 Power. Contact/inhalation protection and seals apply as listed in
the source. Disorientation is -2 all actions for 10 minutes. Nausea doubles wound
modifiers for 10 minutes and, if remaining Power exceeds Willpower, incapacitates
for 3 CT. Paralysis is 1 hour immobility if remaining Power exceeds Reaction,
otherwise -2 dice for 1 hour. Sources: `sr5-core` pp. 408-409
(PDF 410-411).

| ID | Display name | Classification | Vector; Speed; Power; Penetration | Qty/unit | Cost | Availability | Effect/duration/creation eligibility | Source |
| --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| `cs-tear-gas` | CS/Tear Gas | `selectable` | Contact/Inhalation; 1 CT; 8; 0 | one dose | 20 nuyen | 4R | Disorientation, Nausea, Stun; wash skin to end nausea early; inert in air after 2 minutes. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `gamma-scopolamine` | Gamma-Scopolamine | `creation-unavailable` | Injection; Immediate; 12; 0 | one dose | 200 nuyen | 14F | Paralysis about 1 hour, then 1 hour truth-serum residue reducing Willpower by 3, minimum 1; Availability exceeds 12. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `narcoject` | Narcoject | `selectable` | Injection; Immediate; 15; 0 | one dose | 50 nuyen | 8R | Stun damage; no side effects. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `nausea-gas` | Nausea Gas | `selectable` | Inhalation; 3 CT; 9; 0 | one dose | 25 nuyen | 6R | Disorientation and Nausea; inert after 2 minutes in air. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `neuro-stun-viii` | Neuro-Stun VIII | `selectable` | Contact/Inhalation; 1 CT; 15; 0 | one dose | 60 nuyen | 12R | Disorientation and Stun; colorless/odorless; inert after 10 minutes in air. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `neuro-stun-ix` | Neuro-Stun IX | `creation-unavailable` | Contact/Inhalation; 1 CT; 15; 0 | one dose | 60 nuyen | 13R | Disorientation and Stun; inert after 1 minute; Availability exceeds 12. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `neuro-stun-x` | Neuro-Stun X | `creation-unavailable` | Contact/Inhalation; 1 CT; 15; -2 | one dose | 100 nuyen | 14R | Disorientation and Stun; inert after 1 minute; Availability exceeds 12. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `pepper-punch` | Pepper Punch | `selectable` | Contact/Inhalation; 1 CT; 11; 0 | one dose | 5 nuyen | none | Nausea and Stun; may carry RFID/dye tag. | `sr5-core` pp. 409-410 (PDF 411-412) |
| `seven-7` | Seven-7 | `creation-unavailable` | Contact/Inhalation; 1 CT; 12; -2 | one dose | 1,000 nuyen | 20F | Physical damage, Disorientation, Nausea; inert after 10 minutes; Availability exceeds 12. | `sr5-core` pp. 409-410 (PDF 411-412) |

## Drugs And BTLs

Each row is one dose/use. Addiction type controls Logic + Willpower
(Psychological) and/or Body + Willpower (Physiological). The Addiction Rating and
threshold below drive the use-window test; failure creates/worsens Addiction but
does not award creation Karma. Sources: `sr5-core` pp. 411-414
(PDF 413-416).

| ID | Display name | Classification | Vector; Speed; Duration | Cost | Availability | Addiction (type; rating/threshold) | Effect and crash | Source |
| --- | --- | --- | --- | ---: | --- | --- | --- | --- |
| `bliss` | Bliss | `selectable` | Inhalation/Injection; 1 CT; max(1, 6-Body) hours | 15 nuyen | 3F | Both; 5/3 | Reaction -1; all thresholds +1; all limits -1; High Pain Tolerance 3. | `sr5-core` pp. 411, 414 (PDF 413, 416) |
| `cram` | Cram | `selectable` | Ingestion/Inhalation; 10 minutes; max(1, 12-Body) hours | 10 nuyen | 2R | Psychological; 4/3 | Reaction +1; Initiative Dice +1D6; crash 6 unresisted Stun. | `sr5-core` pp. 411, 414 (PDF 413, 416) |
| `deepweed` | Deepweed | `selectable` | Ingestion/Inhalation; Immediate; max(1, 6-Body) hours | 400 nuyen | 8F | Physiological; not listed in Addiction Table | Willpower +1, Mental limit +1, Physical limit -1; forces magical user to astrally perceive; afterward -1 all dice and limits for equal duration. | `sr5-core` p. 411 (PDF 413) |
| `jazz` | Jazz | `selectable` | Inhalation; Immediate; 10 x 1D6 minutes | 75 nuyen | 2R | Both; 8/3 | Reaction +1, Physical limit +1, Initiative Dice +2D6; crash Disorientation for equal duration. | `sr5-core` pp. 411, 414 (PDF 413, 416) |
| `kamikaze` | Kamikaze | `selectable` | Inhalation; Immediate; 10 x 1D6 minutes | 100 nuyen | 4R | Physiological; 9/3 | Body +1, Agility +1, Strength +2, Willpower +1, Physical limit +2, Initiative Dice +2D6, HPT 3; crash Reaction -1, Willpower -1, all limits -2 for equal duration and 6 unresisted Stun. | `sr5-core` pp. 412, 414 (PDF 414, 416) |
| `long-haul` | Long Haul | `selectable` | Injection; 10 minutes; 4 days | 50 nuyen | none | Psychological; 2/1 | No sleep/fatigue modifiers, then sleep 8D6 hours (Disorientation if kept awake); second dose adds 1D6/2 days, then 10 unresisted Stun and mandatory crash; no further extension. | `sr5-core` pp. 412, 414 (PDF 414, 416) |
| `nitro` | Nitro | `selectable` | Inhalation; 1 CT; 10 x 1D6 minutes | 50 nuyen | 2R | Both; 9/3 | Strength +2, Willpower +2, Perception +2, Physical limit +2, HPT 6; crash all limits -2 and 9 unresisted Stun for equal duration. | `sr5-core` pp. 412, 414 (PDF 414, 416) |
| `novacoke` | Novacoke | `selectable` | Inhalation/Injection; 1 CT; max(1, 10-Body) hours | 10 nuyen | 2R | Both; 7/2 | Reaction +1, Charisma +1, Perception +1, Social limit +1, HPT 1; crash Charisma/Willpower become 1 and all limits -1 for equal duration. | `sr5-core` pp. 412, 414 (PDF 414, 416) |
| `psyche` | Psyche | `selectable` | Ingestion; 10 minutes; max(1, 12-Body) hours | 200 nuyen | none | Psychological; 6/2 | Intuition +1, Logic +1, Mental limit +1; Awakened sustained-spell penalty becomes -1 each. | `sr5-core` pp. 412, 414 (PDF 414, 416) |
| `zen` | Zen | `selectable` | Inhalation; 5 minutes; 10 x 1D6 minutes | 5 nuyen | 4R | Psychological; 3/1 | Reaction -2, Willpower +1, -1 dice to physical actions. | `sr5-core` pp. 412, 414 (PDF 414, 416) |
| `dreamchip` | Dreamchip | `selectable` | BTL; Immediate; typically 10 x 1D6 minutes | 20 nuyen | 4F | Psychological; 6/1 | High-intensity doctored simsense narrative; one-use/self-erasing. Dreamdeck format needs hot-sim simsense deck/module; direct-input format needs skilljack/datajack; downloadable format needs modified hot-sim commlink. | `sr5-core` pp. 413-414 (PDF 415-416) |
| `moodchip` | Moodchip | `selectable` | BTL; Immediate; typically 10 x 1D6 minutes | 50 nuyen | 4F | Psychological; 6/2 | Intense selected emotion; usually opposite-emotion crash 1-2 hours; often RAS disabled; same formats/one-use rules. | `sr5-core` pp. 413-414 (PDF 415-416) |
| `personafix` | Personafix | `selectable` | BTL; Immediate; typically 10 x 1D6 minutes | 200 nuyen | 4F | Psychological; 7/2 | Installs personality/behavior pattern while active; often RAS disabled; same formats/one-use rules. | `sr5-core` pp. 413-414 (PDF 415-416) |
| `tripchip` | Tripchip | `selectable` | BTL; Immediate; typically 10 x 1D6 minutes | 100 nuyen | 4F | Psychological; 8/3 | Intense/altered sensory output, often synaesthetic with RAS disabled; same formats/one-use rules. | `sr5-core` pp. 413-414 (PDF 415-416) |

All BTL formats auto-erase after one use; Hardware + Logic (10, 1 hour) Extended
bypasses that behavior. The selected format is a required purchase parameter but
does not change table cost. Source: `sr5-core` p. 413 (PDF 415).

## Explicit Exclusions And Discrepancies

| Entry/family | Classification | Reason | Source |
| --- | --- | --- | --- |
| Run Faster electronics/general gear | `excluded` | Only Sum-to-Ten allocation and the approved formula-grant clarification are in scope; no Run Faster catalog row is admitted. | `run-faster` pp. 62-63 (PDF 64-65) |
| Generic device examples and sample host ratings | `excluded` | Support examples, not creation merchandise with prices. | `sr5-core` pp. 234, 247 (PDF 236, 249) |
| IC programs and hosts | `excluded` | Host-generated security profiles, not purchasable character-creation software in the reviewed tables. | `sr5-core` pp. 246-249 (PDF 248-251) |
| Pilot programs | `excluded` | Included device-specific vehicle/drone components; no separate core purchase row or price. | `sr5-core` p. 269 (PDF 271) |
| Real SIN and legal license | `excluded` | Issued identity/permission, not priced creation merchandise; SINner quality and fake merchandise are cataloged elsewhere. | `sr5-core` pp. 367, 443 (PDF 369, 445) |
| Custom toxins/drugs/BTLs and legacy substances | `excluded` | Narrative mentions/examples without complete purchasable core product rows. | `sr5-core` pp. 408-414 (PDF 410-416) |
| Automated defenses, landscaping, barriers, gas/marking systems | `excluded` | GM security-site support without player merchandise rows/costs. | `sr5-core` pp. 362-366 (PDF 364-368) |
| Hazmat Geiger counter | `excluded` as universal component | Text says many suits include one, not all; no deterministic included component is generated. | `sr5-core` p. 449 (PDF 451) |

Retained source discrepancies:

- `novatech-navigator` prints Availability `6R` in the Matrix deck table and
  `9R` in Street Gear. The ledger records `9R (Matrix table: 6R)`; both satisfy
  creation eligibility, so no eligibility interpretation is introduced.
  Source: `sr5-core` pp. 227, 439 (PDF 229, 441).
- Hacking cyberprograms print Availability `4R` in the Matrix program table and
  `6R` in Street Gear. The ledger records the shared purchase rule as `6R`
  (Matrix table: `4R`); both satisfy creation eligibility. Source: `sr5-core`
  pp. 246, 442 (PDF 248, 444).
- Deepweed is explicitly Physiological in its drug profile but is absent from
  the Addiction Table. The row retains `not listed` rather than inventing an
  Addiction Rating or threshold. Source: `sr5-core` pp. 411, 414
  (PDF 413, 416).
- The example build calls a handheld Capacity 2 housing with two Rating 3
  functions a `scanner` and prices the complete assembly at 800 nuyen, consistent
  with one 200-nuyen housing plus two 300-nuyen single sensors. It creates no
  separate scanner product. Source: `sr5-core` p. 96 (PDF 98).

## Review Footer

### Reviewed Pages

- Creation and legality: `sr5-core` pp. 94, 96, 100-101, 416-419
  (PDF 96, 98, 102-103, 418-421).
- Matrix devices/programs: `sr5-core` pp. 227-248, 266-270
  (PDF 229-250, 268-272).
- Security and identification: `sr5-core` pp. 362-368
  (PDF 364-370).
- Toxins, drugs, and BTLs: `sr5-core` pp. 408-414
  (PDF 410-416).
- Electronics/general gear tables and operative descriptions: `sr5-core`
  pp. 438-451 (PDF 440-453).
- Autosoft prices: `sr5-core` p. 461 (PDF 463).
- `run-faster` pp. 62-63 (PDF 64-65) reviewed only to confirm its catalogs are
  excluded under the approved source contract.

### Approved-PDF Counts

The count unit is one stable-ID row above. Parameterized rows count once even
when some ratings are creation-unavailable variants; included functions count
once and are never duplicate shop choices.

| Classification | Count |
| --- | ---: |
| `selectable` | 138 |
| `parameterized` | 59 |
| `included-component` | 14 |
| `bookkeeping` | 1 |
| `creation-unavailable` | 12 |
| **Total stable-ID rows** | **224** |
| Explicit excluded families | 8 |

Inventory cross-checks: 7 commlinks, 2 sim-module rows, 9 cyberdecks, 11 RCCs,
25 cyberprograms (7 common and 18 hacking), 1 agent family, 6 autosofts, 9
electronic accessories, 5 RFID/data rows, 8 communications devices, 7
information/skillsoft families, 5 credsticks, 1 credit-account record, 2 fake
identity merchandise families, 3 tool tiers, 12 optical/imaging devices, 7
vision enhancements, 5 audio devices, 3 audio enhancements, 4 sensor
housing/package rows, 14 sensor functions, 5 security devices/add-ons, 4
restraints, 10 B&E items, 3 industrial chemicals, 15 survival items, 6 grappling
items, 4 biotech items, 4 DocWagon contracts, 5 patches, 9 toxins, 10 drugs, and
4 BTLs. These category counts total 224.

### Remaining Unknown Facts

None. The three retained source-table omissions/conflicts are represented
verbatim and do not change creation eligibility. Open subjects and physical
configuration choices are bounded parameters, not missing catalog facts.

### Runtime Reconciliation Status

`Not implemented`. CHAR-802 must materialize this reviewed inventory, preserve
included/generated relationships, validate references and rating-specific
eligibility, and reconcile exact IDs/counts before catalog version `1.0.0` is
published.
