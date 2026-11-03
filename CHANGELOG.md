# Changelog

## 1.0.1

- Fixed the gravship launch being cancelled the moment it started when mechs took part in it while the vanilla launch option "Board colony mechanoids" was on (the boarding job giver cancelled the ritual, and the Vanilla Gravship Expanded launch patch then threw `ArgumentNullException: key`). While mechs take part in the ritual the option is now switched off for the launch call and restored afterwards; mechs that are not part of the ritual are still boarded, and participating mechs are kept off the boarding list. Works for any mech and with other mods that handle their own mechs.
- The launch start check now also counts colony mechs for required roles, so a launch with no qualifying human is no longer silently refused.
- New mod settings page (Options > Mod settings > VGE Mechanoid Crew): the default mech skill level (factory default 8) can now be changed from 0 to 20 in game. Per-mech `MechCrewSkills` levels still take priority, and a `MechCrewSkills` entry without a `<level>` now follows the setting.
- New optional `MechCrewIgnore` mod extension, **not applied to any mech by default**: out of the box every colony mech can fill the pilot, copilot, gravtech researcher and gravtech engineer roles and join the crew. Add it to a mech's ThingDef only if you want this mod to leave that specific mech alone (for example because another mod already handles it, or because you do not want this mod to let it take part). It opts the mech out of this mod only, it is not a ban: another mod can still let the mech take part. The "Board colony mechanoids" fix is not affected by it. A disabled example is in `Common/Patches/MechIgnore_Example.xml`.
- Mechs are now also added to the candidate list when the launch window opens (the hook the gravship launch dialog needs in current game versions), in addition to when it is created. Duplicates are skipped.
- The pilot console's navigator check now accepts colony mechs for the pilot role.
- With dev mode on, the log now shows why vanilla refused a launch (`[VGE Mech Crew] Vanilla launch check refused: ...`).

## 1.0.0 - Initial release

- Colony mechanoids can now fill the gravship launch ritual roles from Vanilla Gravship Expanded - Chapter 1: pilot, copilot, gravtech researcher and gravtech engineer.
- Mechanoids can join the launch as regular crew and count towards the crew count quality bonus, like a colonist.
- Works with modded mechanoids: any mech belonging to your colony is eligible.
- Mechs appear in the normal launch dialog. Downed mechs and mechs busy in another lord job are left out.
- Mechs have no skills, so they now act as skill level 8 for Piloting Ability, Research Speed, Gravship Research and Gravship Maintenance. Each stat's own skill curves decide the result.
- New `MechCrewSkills` mod extension lets a specific mech define its own virtual skill levels (sample patch for the Constructoid included).
- Only gravship launches are affected; other rituals behave as in vanilla.
- Startup log line (`[VGE Mech Crew] ... patch group(s) applied`) and per-patch error logging to make problems easy to diagnose.
- Tested on RimWorld 1.7. Built against the 1.6 assemblies.
