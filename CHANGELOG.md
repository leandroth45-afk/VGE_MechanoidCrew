# Changelog

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
