using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
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
        public const int FactoryDefaultSkillLevel = 8;
        public const int MinSkillLevel = 0;
        public const int MaxSkillLevel = 20;

        // Live value: editable in Options > Mod settings > VGE Mechanoid Crew
        public static int DefaultSkillLevel
        {
            get
            {
                int level = MechCrewMod.Settings != null
                    ? MechCrewMod.Settings.defaultSkillLevel
                    : FactoryDefaultSkillLevel;
                return UnityEngine.Mathf.Clamp(level, MinSkillLevel, MaxSkillLevel);
            }
        }

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
        public int level = -1; // -1 = use the default level from the mod settings
    }

    public class MechCrewSkills : DefModExtension
    {
        public List<MechCrewSkillEntry> skills = new List<MechCrewSkillEntry>();
    }

    // =====================================================================
    // Mod settings (Options > Mod settings > VGE Mechanoid Crew)
    // =====================================================================
    public class MechCrewModSettings : ModSettings
    {
        public int defaultSkillLevel = MechCrewSettings.FactoryDefaultSkillLevel;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref defaultSkillLevel, "defaultSkillLevel", MechCrewSettings.FactoryDefaultSkillLevel);
        }
    }

    public class MechCrewMod : Mod
    {
        public static MechCrewModSettings Settings;

        public MechCrewMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MechCrewModSettings>();
        }

        public override string SettingsCategory()
        {
            return "VGE Mechanoid Crew";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Default mech skill level: " + Settings.defaultSkillLevel
                + "  (factory default " + MechCrewSettings.FactoryDefaultSkillLevel + ")");
            Settings.defaultSkillLevel = UnityEngine.Mathf.RoundToInt(
                listing.Slider(Settings.defaultSkillLevel, MechCrewSettings.MinSkillLevel, MechCrewSettings.MaxSkillLevel));

            listing.Gap(8f);
            listing.Label("Mechanoids have no skills, so for Piloting Ability, Research Speed, Gravship Research and "
                + "Gravship Maintenance they act as this skill level (0-20, same scale as colonists). "
                + "A mech that has its own MechCrewSkills extension uses those levels instead. "
                + "Stat values may take a few moments to refresh after changing this.");

            listing.Gap(8f);
            if (listing.ButtonText("Reset to factory default (" + MechCrewSettings.FactoryDefaultSkillLevel + ")"))
                Settings.defaultSkillLevel = MechCrewSettings.FactoryDefaultSkillLevel;

            listing.End();
        }
    }

    // Optional opt-out marker. NOT applied to any mech by default: out of the box every colony mech can take
    // the roles and join the crew. Add this extension to a mech's ThingDef only if you want THIS mod to leave
    // that mech alone (no pool injection, no role/crew override, no virtual skills), e.g. because another mod
    // already handles it. It is not a ban: another mod can still let the mech take part, and the
    // "Board colony mechanoids" fix applies to any mech that takes part.
    // XML: <modExtensions><li Class="GravshipMechanoidsPatch.MechCrewIgnore" /></modExtensions>
    public class MechCrewIgnore : DefModExtension
    {
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
                        return ext.skills[i].level >= 0 ? ext.skills[i].level : MechCrewSettings.DefaultSkillLevel;
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

        // A colony mech this mod is responsible for (i.e. not opted out with MechCrewIgnore)
        public static bool Handles(Pawn p)
        {
            return p != null && p.IsColonyMech && !p.def.HasModExtension<MechCrewIgnore>();
        }

        // Adds the colony mechs this mod handles to the candidate pool of a gravship launch.
        // Safe to call more than once: mechs already in the pool are never added twice.
        public static int AddMechsToPool(RitualRoleAssignments assignments, Precept_Ritual ritual, Map map)
        {
            if (assignments == null || map == null || !IsGravshipRitual(ritual))
                return 0;

            List<Pawn> pool = assignments.AllCandidatePawns;
            if (pool == null)
                return 0;

            int added = 0;
            List<Pawn> colonyMechs = map.mapPawns.SpawnedColonyMechs;
            for (int i = 0; i < colonyMechs.Count; i++)
            {
                Pawn mech = colonyMechs[i];
                if (!Handles(mech) || mech.Downed || mech.GetLord() != null || pool.Contains(mech))
                    continue;

                pool.Add(mech);
                added++;
            }
            return added;
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
            int added = MechCrew.AddMechsToPool(__result, ritual, map);
            if (added > 0 && Prefs.DevMode)
                Log.Message("[VGE Mech Crew] Added " + added + " colony mech(s) to the gravship launch pool (dialog creation).");
        }
    }

    // The gravship launch dialog (Dialog_BeginGravshipLaunch, a Dialog_BeginRitual subclass) may build its
    // pool by a different route, so inject again once the window is open. This is the same hook the
    // Stormmarina mod uses for its own mechs; duplicates are skipped.
    [HarmonyPatch(typeof(Dialog_BeginRitual), "PostOpen")]
    public static class Patch_DialogPostOpen_AddMechs
    {
        public static void Postfix(object __instance)
        {
            if (!(__instance is Dialog_BeginRitual dialog))
                return;

            Traverse t = Traverse.Create(dialog);
            Precept_Ritual ritual = t.Field("ritual").GetValue<Precept_Ritual>();
            Map map = t.Field("map").GetValue<Map>();
            RitualRoleAssignments assignments = t.Field("assignments").GetValue<RitualRoleAssignments>();

            int added = MechCrew.AddMechsToPool(assignments, ritual, map);
            if (added > 0 && Prefs.DevMode)
                Log.Message("[VGE Mech Crew] Added " + added + " colony mech(s) to the gravship launch pool (window open).");
        }
    }

    // The pilot console validates the chosen navigator and rejects pawns without skills (every mech).
    // Parameters are read through __args so a renamed parameter in a newer game version cannot break the patch.
    [HarmonyPatch(typeof(CompPilotConsole), "ValidateNavigator")]
    public static class Patch_ValidateNavigator
    {
        public static void Postfix(object[] __args, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !MechCrewSettings.AllowedRoleIds.Contains("pilot"))
                return;
            if (__args == null || __args.Length == 0 || !(__args[0] is LocalTargetInfo target))
                return;

            Pawn pawn = target.Thing as Pawn;
            if (!MechCrew.Handles(pawn) || pawn.Downed)
                return;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                return;

            __result = AcceptanceReport.WasAccepted;
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
            if (__result || !MechCrew.Handles(p))
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
            if (pawn == null || pawn.skills != null || !pawn.RaceProps.IsMechanoid || pawn.def.HasModExtension<MechCrewIgnore>())
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
            if (__result || !MechCrewSettings.MechsCountAsCrew || !MechCrew.Handles(pawn))
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
            if (__result == null || role != null || !MechCrewSettings.MechsCountAsCrew || !MechCrew.Handles(p))
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
            if (__result || !MechCrewSettings.MechsCountAsCrew || !MechCrew.Handles(p))
                return;
            if (assignments == null || !MechCrew.IsGravshipRitual(assignments.Ritual))
                return;

            RitualRole role = assignments.RoleForPawn(p);
            if (role != null && !role.countsAsParticipant)
                return;

            __result = true;
        }
    }

    // =====================================================================
    // 6. The launch itself.
    //    RitualBehaviorWorker.TryExecuteOn starts with CanStartRitualNow(), which builds its OWN
    //    pool (colonists, prisoners, animals - no mechs) and refuses to start when a required role
    //    has no candidate in it. When that happens TryExecuteOn returns silently, no lord is created,
    //    and the launch is cancelled right after the dialog's Start button.
    //    Fix: if the vanilla check refuses, count mechs as candidates for the required roles, then
    //    ask vanilla again with those roles marked as covered. Only if vanilla then agrees do we
    //    clear the refusal, so every other reason (wrong layer, already running, ...) still blocks.
    // =====================================================================
    [HarmonyPatch(typeof(RitualBehaviorWorker), nameof(RitualBehaviorWorker.CanStartRitualNow))]
    public static class Patch_CanStartRitualNow
    {
        static bool rechecking;

        public static void Postfix(RitualBehaviorWorker __instance, TargetInfo target, Precept_Ritual ritual,
            Pawn selectedPawn, Dictionary<string, Pawn> forcedForRole, ref string __result)
        {
            if (__result == null || rechecking || !target.IsValid || !MechCrew.IsGravshipRitual(ritual))
                return;

            if (Prefs.DevMode)
                Log.Message("[VGE Mech Crew] Vanilla launch check refused: " + __result);

            List<RitualRole> roles = ritual.behavior.def.roles;
            if (roles.NullOrEmpty())
                return;

            List<Pawn> pool = target.Map.mapPawns.FreeColonistsAndPrisonersSpawned.ToList();
            List<Pawn> colonyMechs = target.Map.mapPawns.SpawnedColonyMechs;
            for (int i = 0; i < colonyMechs.Count; i++)
            {
                Pawn mech = colonyMechs[i];
                if (MechCrew.Handles(mech) && !mech.Downed && mech.GetLord() == null)
                    pool.Add(mech);
            }

            var forced = forcedForRole != null
                ? new Dictionary<string, Pawn>(forcedForRole)
                : new Dictionary<string, Pawn>();

            bool coveredAny = false;
            foreach (RitualRole role in roles)
            {
                if (!role.required || role.substitutable || forced.ContainsKey(role.id))
                    continue;

                int needed = role.mergeId == null
                    ? 1
                    : roles.Count(r => r.mergeId == role.mergeId);

                int found = 0;
                Pawn firstCandidate = null;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (role.AppliesToPawn(pool[i], out string _, target, null, null, ritual, skipReason: true))
                    {
                        found++;
                        if (firstCandidate == null)
                            firstCandidate = pool[i];
                    }
                }

                if (found >= needed && firstCandidate != null)
                {
                    forced[role.id] = firstCandidate; // vanilla only checks ContainsKey(role.id)
                    coveredAny = true;
                }
            }

            if (!coveredAny)
                return;

            rechecking = true;
            try
            {
                string again = __instance.CanStartRitualNow(target, ritual, selectedPawn, forced);
                if (again == null)
                    __result = null;
                else if (Prefs.DevMode)
                    Log.Message("[VGE Mech Crew] Still refused after counting mechs: " + again);
            }
            finally
            {
                rechecking = false;
            }
        }
    }

    // =====================================================================
    // 7. "Board colony mechanoids" option of the gravship launch.
    //    With the option on, vanilla queues every colony mech to board the ship. A mech that is ALSO a
    //    participant of the launch ritual (a role or crew) is then told to board by
    //    JobGiver_BoardOrLeaveGravship while it belongs to the ritual lord, and that cancels the whole
    //    ritual the moment it starts.
    //    Fix (same idea as the Stormmarina mod, but for any mech): when a mech takes part in the ritual,
    //    switch the option off for the duration of TryExecuteOn, restore it afterwards, board the mechs
    //    that are NOT taking part ourselves, and keep participating mechs off the boarding list.
    //    Runs last so it sees (and respects) what other mods already did. Everything is reflection-based:
    //    if a game version lacks these members the patch simply does nothing.
    // =====================================================================
    [HarmonyPatch(typeof(RitualBehaviorWorker_GravshipLaunch), nameof(RitualBehaviorWorker_GravshipLaunch.TryExecuteOn))]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_GravshipLaunch_MechBoarding
    {
        static readonly FieldInfo BoardColonyMechsField =
            AccessTools.Field(typeof(RitualBehaviorWorker_GravshipLaunch), "boardColonyMechs");
        static readonly FieldInfo PawnsToBoardField =
            AccessTools.Field(typeof(Building_GravEngine), "pawnsToBoard");

        // RitualRoleAssignments.Participants returns a shared buffer that is cleared on every access: copy at once.
        static List<Pawn> MechParticipants(RitualRoleAssignments assignments)
        {
            var result = new List<Pawn>();
            if (assignments == null)
                return result;

            List<Pawn> participants = assignments.Participants;
            for (int i = 0; i < participants.Count; i++)
                if (participants[i] != null && participants[i].IsColonyMech)
                    result.Add(participants[i]);
            return result;
        }

        static ICollection<Pawn> PawnsToBoardOf(TargetInfo target, bool createIfMissing)
        {
            if (PawnsToBoardField == null || !target.IsValid)
                return null;

            ThingWithComps thing = target.Thing as ThingWithComps;
            if (thing == null)
                return null;

            foreach (ThingComp comp in thing.AllComps)
            {
                for (Type t = comp.GetType(); t != null; t = t.BaseType)
                {
                    if (t.Name != "CompGravshipFacility")
                        continue;

                    Building_GravEngine engine = AccessTools.Field(t, "engine")?.GetValue(comp) as Building_GravEngine;
                    if (engine == null)
                        return null;

                    var collection = PawnsToBoardField.GetValue(engine) as ICollection<Pawn>;
                    if (collection == null && createIfMissing)
                    {
                        collection = Activator.CreateInstance(PawnsToBoardField.FieldType) as ICollection<Pawn>;
                        if (collection != null)
                            PawnsToBoardField.SetValue(engine, collection);
                    }
                    return collection;
                }
            }
            return null;
        }

        static void Restore(RitualBehaviorWorker_GravshipLaunch worker)
        {
            BoardColonyMechsField?.SetValue(worker, true);
        }

        public static void Prefix(RitualBehaviorWorker_GravshipLaunch __instance, RitualRoleAssignments assignments, out bool __state)
        {
            __state = false;
            if (BoardColonyMechsField == null || MechParticipants(assignments).Count == 0)
                return;

            // only switch it off if it is currently on (option off, or another mod already switched it off: leave it)
            if (!(BoardColonyMechsField.GetValue(__instance) is bool isOn) || !isOn)
                return;

            BoardColonyMechsField.SetValue(__instance, false);
            __state = true;
        }

        public static void Postfix(RitualBehaviorWorker_GravshipLaunch __instance, TargetInfo target,
            Precept_Ritual ritual, RitualRoleAssignments assignments, bool __state)
        {
            if (__state)
                Restore(__instance);

            List<Pawn> participants = MechParticipants(assignments);
            if (participants.Count == 0 || !target.IsValid || target.Map == null)
                return;

            // nothing to board if the ritual never actually started
            bool started = target.Map.lordManager.lords.Any(l => l.LordJob is LordJob_Ritual job && job.Ritual == ritual);
            if (!started)
                return;

            ICollection<Pawn> toBoard = PawnsToBoardOf(target, createIfMissing: __state);
            if (toBoard == null)
                return;

            if (__state)
            {
                // what vanilla would have done for the option, minus the mechs that take part in the ritual
                var spawned = new List<Pawn>(target.Map.mapPawns.AllPawnsSpawned);
                for (int i = 0; i < spawned.Count; i++)
                {
                    Pawn mech = spawned[i];
                    if (mech.Downed || mech.Dead || !mech.IsColonyMech || participants.Contains(mech) || toBoard.Contains(mech))
                        continue;

                    toBoard.Add(mech);
                    mech.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                }
            }

            // ritual participants must never be on the boarding list (also covers other mods adding them)
            for (int i = 0; i < participants.Count; i++)
                toBoard.Remove(participants[i]);
        }

        public static Exception Finalizer(RitualBehaviorWorker_GravshipLaunch __instance, bool __state, Exception __exception)
        {
            if (__state)
                Restore(__instance);
            return __exception;
        }
    }
}
