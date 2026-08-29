using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheatOnYourDayOnes.World
{
    /// <summary>
    /// Assigns one coherent reaction to a nearby crowd for each assault. Future
    /// police spawning can subscribe to PoliceReportCompleted without changing NPC AI.
    /// </summary>
    public static class NPCWitnessCoordinator
    {
        private const float WitnessRadius = 15f;

        public static event Action<Vector3, Transform> PoliceReportCompleted;

        public static void ReportAssault(NPCWanderer victim, Transform suspect)
        {
            if (victim == null || suspect == null) return;

            float radiusSqr = WitnessRadius * WitnessRadius;
            List<NPCWanderer> witnesses = new();
            foreach (NPCWanderer npc in NPCWanderer.ActiveNpcs)
            {
                if (npc == null || npc == victim || !npc.CanReactAsWitness) continue;
                Vector3 offset = npc.transform.position - victim.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSqr) witnesses.Add(npc);
            }

            if (witnesses.Count == 0) return;

            // Shuffle so the same NPC is not always selected merely because it spawned first.
            for (int i = witnesses.Count - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                (witnesses[i], witnesses[swap]) = (witnesses[swap], witnesses[i]);
            }

            int callerCount = witnesses.Count == 1
                ? (UnityEngine.Random.value < .5f ? 1 : 0)
                : Mathf.Max(1, Mathf.RoundToInt(witnesses.Count / 3f));

            for (int i = 0; i < witnesses.Count; i++)
            {
                NPCWanderer witness = witnesses[i];
                bool shouldCall = i < callerCount;
                if (!shouldCall || !witness.TryBeginPoliceCall(suspect))
                    witness.FleeFromWitnessIncident(suspect);
            }
        }

        internal static void CompletePoliceReport(Vector3 incidentPosition, Transform suspect)
        {
            PoliceReportCompleted?.Invoke(incidentPosition, suspect);
        }
    }
}
