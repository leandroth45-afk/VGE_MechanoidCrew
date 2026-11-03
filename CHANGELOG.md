# Changelog

## 1.0.1

- Fixed the gravship launch being cancelled right after pressing Start when some moded mechs were assigned to the roles. The vanilla start check built its own pawn list without mechs and silently aborted the launch when a required role had no human candidate. The check now counts colony mechs for the required roles, and every other launch restriction still applies.
- New `MechCrewIgnore` mod extension: a mech with it is left completely alone by this mod (pool, roles, crew, stats). Meant for mechs from mods that already handle gravship roles themselves (see `Common/Patches/MechIgnore_Example.xml`).
- Mechs are now also added to the candidate list when the launch window opens (the hook the gravship launch dialog needs in current game versions), in addition to when it is created. Duplicates are skipped.
- The pilot console's navigator check now accepts colony mechs for the pilot role.
- With dev mode on, the log now shows why it refused a launch (`[VGE Mech Crew] Vanilla launch check refused: ...`).

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
