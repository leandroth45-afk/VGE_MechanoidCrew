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
mech a **virtual skill level**, default **8**, and feeds it into the skill curves of these stats:
`PilotingAbility`, `ResearchSpeed`, `VGE_GravshipResearch`, `VGE_GravshipMaintenance`.

A specific mech can override it with a mod extension:

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

## Mods that already handle their own mechs

If another mod already gives its own mechs gravship roles, leave those mechs to that mod by adding the
`MechCrewIgnore` extension to their ThingDef (disabled example in `Common/Patches/MechIgnore_Example.xml`):

```xml
<modExtensions>
  <li Class="GravshipMechanoidsPatch.MechCrewIgnore" />
</modExtensions>
```

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
- In-game settings for the default level.

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
