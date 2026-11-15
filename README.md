# Vanilla Gravship Expanded - Mechanoid Crew

A RimWorld mod that lets colony mechanoids work the gravship launch ritual from
[Vanilla Gravship Expanded - Chapter 1](https://steamcommunity.com/sharedfiles/filedetails/?id=3609835606).

## Features

- Mechs can fill the pilot, copilot, gravtech researcher and gravtech engineer roles.
- Mechs can join as regular crew and count towards the crew count bonus.
- Works with modded mechanoids.
- Only gravship launches are affected; every other ritual is vanilla.

## How mech skill works (current version)

Mechanoids have no skill tracker, so vanilla applies no skill scaling to them. This mod gives every
mech a **virtual skill level**, default **8** (adjustable), and feeds it into the skill curves of these stats:
`PilotingAbility`, `ResearchSpeed`, `VGE_GravshipResearch`, `VGE_GravshipMaintenance`.

The default level can be changed in game: Options > Mod settings > VGE Mechanoid Crew (0 to 20).

A specific mech can override it with a mod extension (this takes priority over the setting):

```xml
<modExtensions>
  <li Class="GravshipMechanoidsPatch.MechCrewSkills">
    <skills>
      <li><skill>Intellectual</skill><level>12</level></li>
    </skills>
  </li>
</modExtensions>
```

See `Common/Patches/MechSkills_Patch.xml` for a working example.

<<<<<<< HEAD
## Opting a mech out: `MechCrewIgnore` (optional)

**Nothing is opted out by default.** Every colony mech can fill the pilot, copilot, gravtech researcher and gravtech engineer roles
and join the crew. `MechCrewIgnore` is a tool you add yourself to a specific mech when you want this mod to leave it alone, for example:

- another mod already handles that mech's gravship roles and you want that mod to stay in charge;
- you do not want this mod to let that mech pilot or join the crew.

A mech with the extension is not added to the launch pool, is not allowed into roles or crew by this mod, and gets no virtual skill level.

It is **not a ban**: if another mod lets the mech take part, it still can. For a mech that only this mod would allow, opting out simply
puts it back to vanilla (humans only). The "Board colony mechanoids" fix applies to any mech that takes part, whoever allowed it.
=======
## Mods that already handle their own mechs

If another mod already gives its own mechs gravship roles, leave those mechs to that mod by adding the
`MechCrewIgnore` extension to their ThingDef (disabled example in `Common/Patches/MechIgnore_Example.xml`):
>>>>>>> e83b6964ac407f0346619e980093a6aa2ae39952

```xml
<modExtensions>
  <li Class="GravshipMechanoidsPatch.MechCrewIgnore" />
</modExtensions>
```

<<<<<<< HEAD
A disabled example is in `Common/Patches/MechIgnore_Example.xml`.

=======
>>>>>>> e83b6964ac407f0346619e980093a6aa2ae39952
### Silversol's Stormmarina

That mod already lets its own mechs (`Mech_Marina`, `Mech_MarinaX`, `Mech_Seiren`) pilot and copilot, and handles boarding for
`Mech_Marinecrew` and `Mech_Phantomsail`, which it deliberately does not let pilot. Both mods can run together. With this mod
enabled, every colony mech (including Marinecrew and Phantomsail) can take the roles. To keep the original design for those two, add
`MechCrewIgnore` to them (the example below only applies when Stormmarina is loaded):

```xml
<Operation Class="PatchOperationFindMod">
  <mods><li>Silversol's Stormmarina</li></mods>
  <match Class="PatchOperationAddModExtension">
    <xpath>/Defs/ThingDef[defName="Mech_Marinecrew" or defName="Mech_Phantomsail"]</xpath>
    <value><li Class="GravshipMechanoidsPatch.MechCrewIgnore" /></value>
  </match>
</Operation>
```

## Planned

- Use the piloting ability that some modded mechs define themselves.
- Mech attributes instead of one shared default level.

## Requirements

Odyssey, Biotech, Harmony, Vanilla Gravship Expanded - Chapter 1.

## Building

1. Install the [.NET SDK](https://dotnet.microsoft.com/download).
2. From the repository root: `dotnet build Source/GravshipMechanoidsPatch.csproj`

NuGet restores the RimWorld reference assemblies (`Krafs.Rimworld.Ref`) and Harmony, so no game files
are needed to compile. The DLL is written to `1.6/Assemblies/` and copied to `1.7/Assemblies/`.

## Bug reports

Please attach your `Player.log` (lines starting with `[VGE Mech Crew]`) and your mod list.

## License

Code: see `LICENSE`. `About/Preview.png` contains Vanilla Expanded artwork and is not covered by the code license.
