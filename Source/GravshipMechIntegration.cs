using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace GravshipMechanoidsPatch
{
    // =====================================================================
    // Tunables
    // =====================================================================
    public static class MechCrewSettings
    {
        // Ritual role ids (from the VGE RitualBehaviorDefs) that colony mechs may fill.
        // Remove "pilot" from this set if you want the pilot to stay human-only.
        public static readonly HashSet<string> AllowedRoleIds = new HashSet<string>
        {
            "pilot", "copilot", "gravtechResearcher", "gravtechEngineer"
        };

        // Mechs can also stand in as ordinary crew (spectators) and count towards the
        // "crew count" quality bonus, exactly like a colonist would.
        public const bool MechsCountAsCrew = true;

        // Mechs have no skill tracker. For the stats below they act as if they had these
        // skill levels, so the vanilla skill curves of each stat decide how good they are.
        // A mech can override this with the MechCrewSkills mod extension (see MechSkills_Patch.xml).
        public const int DefaultSkillLevel = 8;

        public static readonly HashSet<string> SkillMirroredStats = new HashSet<string>
        {
            "PilotingAbility", "ResearchSpeed", "VGE_GravshipResearch", "VGE_GravshipMaintenance"
        };
    }

    // XML: <modExtensions><li Class="GravshipMechanoidsPatch.MechCrewSkills"><skills>
    //        <li><skill>Intellectual</skill><level>12</level></li></skills></li></modExtensions>
    public class MechCrewSkillEntry
    {
        public SkillDef skill;
        public int level = MechCrewSettings.DefaultSkillLevel;
    }

    public class MechCrewSkills : DefModExtension
    {
        public List<MechCrewSkillEntry> skills = new List<MechCrewSkillEntry>();
    }

    internal static class MechSkills
    {
        static readonly FieldInfo BaseValueField = AccessTools.Field(typeof(SkillNeed_BaseBonus), "baseValue");
        static readonly FieldInfo BonusPerLevelField = AccessTools.Field(typeof(SkillNeed_BaseBonus), "bonusPerLevel");

        public static int LevelFor(Pawn mech, SkillDef skill)
        {
            MechCrewSkills ext = mech.def.GetModExtension<MechCrewSkills>();
            if (ext?.skills != null)
            {
                for (int i = 0; i < ext.skills.Count; i++)
                    if (ext.skills[i].skill == skill)
                        return ext.skills[i].level;
            }
            return MechCrewSettings.DefaultSkillLevel;
        }

        // Same maths as vanilla SkillNeed.ValueFor, but with the mech's virtual skill level.
        public static float ValueAt(SkillNeed need, Pawn mech)
        {
            if (need?.skill == null)
                return 1f;

            int level = LevelFor(mech, need.skill);

            var direct = need as SkillNeed_Direct;
            if (direct != null)
            {
                if (direct.valuesPerLevel.Count > level) return direct.valuesPerLevel[level];
                if (direct.valuesPerLevel.Count > 0) return direct.valuesPerLevel[direct.valuesPerLevel.Count - 1];
                return 1f;
            }

            var bonus = need as SkillNeed_BaseBonus;
            if (bonus != null && BaseValueField != null && BonusPerLevelField != null)
            {
                float baseValue = (float)BaseValueField.GetValue(bonus);
                float perLevel = (float)BonusPerLevelField.GetValue(bonus);
                return baseValue + perLevel * level;
            }

            return 1f;
        }
    }

    internal static class MechCrew
    {
        // Only gravship launches are touched; every other ritual keeps vanilla behaviour.
        public static bool IsGravshipRitual(Precept_Ritual ritual)
        {
            return ritual != null && ritual.behavior is RitualBehaviorWorker_GravshipLaunch;
        }

        // RitualRoleAssignments.ritualTarget is private
        public static TargetInfo TargetOf(RitualRoleAssignments assignments)
        {
            return Traverse.Create(assignments).Field("ritualTarget").GetValue<TargetInfo>();
        }
    }

    // =====================================================================
    // Startup: patch each class on its own so ONE failure can never silently
    // take the rest of the mod down, and every failure is logged by name.
    // =====================================================================
    [StaticConstructorOnStartup]
    public static class ModInit
    {
        static ModInit()
        {
            var harmony = new Harmony("com.vge.mechanoidcrew");
            int ok = 0, failed = 0;

            foreach (Type type in typeof(ModInit).Assembly.GetTypes())
            {
                if (!type.IsDefined(typeof(HarmonyPatch), false))
                    continue;

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Error("[VGE Mech Crew] Could not apply " + type.Name + ":\n" + ex);
                }
            }

            Log.Message("[VGE Mech Crew] " + ok + " patch group(s) applied, " + failed + " failed.");
        }
    }

    // =====================================================================
    // 1. Put colony mechs into the candidate pool.
    //    Vanilla builds the pool from map.mapPawns.FreeColonistsAndPrisonersSpawned
    //    (humanlikes only), so mechs never even reach the role checks without this.
    // =====================================================================
    [HarmonyPatch(typeof(Dialog_BeginRitual), nameof(Dialog_BeginRitual.CreateRitualRoleAssignments))]
    public static class Patch_CreateRitualRoleAssignments
    {
        public static void Postfix(RitualRoleAssignments __result, Precept_Ritual ritual, Map map)
        {
            if (__result == null || map == null || !MechCrew.IsGravshipRitual(ritual))
                return;

            List<Pawn> pool = __result.AllCandidatePawns;
            if (pool == null)
                return;

            int added = 0;
            List<Pawn> colonyMechs = map.mapPawns.SpawnedColonyMechs;
            for (int i = 0; i < colonyMechs.Count; i++)
            {
                Pawn mech = colonyMechs[i];
                if (mech.Downed || mech.GetLord() != null || pool.Contains(mech))
                    continue;

                pool.Add(mech);
                added++;
            }

            if (added > 0 && Prefs.DevMode)
                Log.Message("[VGE Mech Crew] Added " + added + " colony mech(s) to the gravship launch pool.");
        }
    }

    // =====================================================================
    // 2. Let mechs pass the "must be humanlike" check on the gravship roles.
    //    Vanilla returns false at the Humanlike check, before it would touch
    //    pawn.skills (null on mechs), so there is no null-reference risk.
    // =====================================================================
    [HarmonyPatch(typeof(RitualRoleColonist), nameof(RitualRoleColonist.AppliesToPawn))]
    public static class Patch_RitualRoleColonist_AppliesToPawn
    {
        public static void Postfix(RitualRoleColonist __instance, Pawn p, ref string reason,
            LordJob_Ritual ritual, RitualRoleAssignments assignments, Precept_Ritual precept,
            ref bool __result)
        {
            if (__result || p == null || !p.IsColonyMech)
                return;

            if (!MechCrewSettings.AllowedRoleIds.Contains(__instance.id))
                return;

            Precept_Ritual context = ritual?.Ritual ?? assignments?.Ritual ?? precept;
            if (!MechCrew.IsGravshipRitual(context))
                return;

            reason = null;
            __result = true;
        }
    }

    // =====================================================================
    // 3. Mirror skills for mechs.
    //    Vanilla only applies skillNeedOffsets/skillNeedFactors when pawn.skills != null,
    //    so a mech gets the flat noSkillOffset and no skill scaling at all. Here the mech
    //    is treated as having a virtual skill level and the stat's own skill curves are used.
    //    Applied in GetValueUnfinalized so the stat's parts / post-process curve still run after.
    // =====================================================================
    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValueUnfinalized))]
    public static class Patch_StatWorker_GetValueUnfinalized
    {
        public static void Postfix(StatRequest req, StatDef ___stat, ref float __result)
        {
            if (___stat == null || !req.HasThing)
                return;

            Pawn pawn = req.Thing as Pawn;
            if (pawn == null || pawn.skills != null || !pawn.RaceProps.IsMechanoid)
                return;

            if (!MechCrewSettings.SkillMirroredStats.Contains(___stat.defName))
                return;

            // vanilla added noSkillOffset because there is no skill tracker; swap it for the real curves
            __result -= ___stat.noSkillOffset;

            if (___stat.skillNeedOffsets != null)
                for (int i = 0; i < ___stat.skillNeedOffsets.Count; i++)
                    __result += MechSkills.ValueAt(___stat.skillNeedOffsets[i], pawn);

            if (___stat.skillNeedFactors != null)
                for (int i = 0; i < ___stat.skillNeedFactors.Count; i++)
                    __result *= MechSkills.ValueAt(___stat.skillNeedFactors[i], pawn);
        }
    }

    // =====================================================================
    // 4. Mechs as regular crew (spectators).
    // =====================================================================
    [HarmonyPatch(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.CanEverSpectate))]
    public static class Patch_CanEverSpectate
    {
        public static void Postfix(RitualRoleAssignments __instance, Pawn pawn, ref bool __result)
        {
            if (__result || !MechCrewSettings.MechsCountAsCrew || pawn == null || !pawn.IsColonyMech)
                return;

            Precept_Ritual r = __instance.Ritual;
            if (!MechCrew.IsGravshipRitual(r))
                return;

            // same checks vanilla runs before its humanlike test
            if (r.ritualOnlyForIdeoMembers && pawn.Ideo != r.ideo && !r.def.allowSpectatorsFromOtherIdeos)
                return;
            if (r.behavior.def.spectatorFilter != null && !r.behavior.def.spectatorFilter.Allowed(pawn))
                return;
            if (!r.behavior.PawnCanFillRole(pawn, null, out string _, MechCrew.TargetOf(__instance)))
                return;

            __result = GatheringsUtility.ShouldPawnKeepAttendingRitual(pawn, r, r.behavior.def.spectatorsIgnoreBleeding);
        }
    }

    // The three PawnNotAssignableReason overloads all end up in the static one, so patch that.
    [HarmonyPatch]
    public static class Patch_PawnNotAssignableReason
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(RitualRoleAssignments), nameof(RitualRoleAssignments.PawnNotAssignableReason),
                new[]
                {
                    typeof(Pawn), typeof(RitualRole), typeof(Precept_Ritual),
                    typeof(RitualRoleAssignments), typeof(TargetInfo), typeof(bool).MakeByRefType()
                });
        }

        public static void Postfix(Pawn p, RitualRole role, Precept_Ritual ritual, TargetInfo ritualTarget, ref string __result)
        {
            if (__result == null || role != null || !MechCrewSettings.MechsCountAsCrew || p == null || !p.IsColonyMech)
                return;
            if (!MechCrew.IsGravshipRitual(ritual))
                return;

            // only clear the "spectators must be humanlike" message, never other reasons (downed, bleeding, ...)
            string humanlikeMessage = "MessageRitualRoleMustBeHumanlike".Translate("Spectators".Translate());
            if (__result != humanlikeMessage)
                return;

            __result = null;

            // vanilla skipped these checks when it returned the humanlike message; run them now
            if (!ritual.def.allowSpectatorsFromOtherIdeos && ritual.ritualOnlyForIdeoMembers && p.Ideo != ritual.ideo)
            {
                __result = humanlikeMessage;
                return;
            }
            if (!ritual.behavior.PawnCanFillRole(p, null, out string why, ritualTarget))
                __result = why;
        }
    }

    // =====================================================================
    // 5. Crew-count quality bonus: vanilla Counts() returns p.RaceProps.Humanlike.
    // =====================================================================
    [HarmonyPatch(typeof(RitualOutcomeComp_ParticipantCount), "Counts")]
    public static class Patch_ParticipantCount_Counts
    {
        public static void Postfix(RitualRoleAssignments assignments, Pawn p, ref bool __result)
        {
            if (__result || !MechCrewSettings.MechsCountAsCrew || p == null || !p.IsColonyMech)
                return;
            if (assignments == null || !MechCrew.IsGravshipRitual(assignments.Ritual))
                return;

            RitualRole role = assignments.RoleForPawn(p);
            if (role != null && !role.countsAsParticipant)
                return;

            __result = true;
        }
    }
}
