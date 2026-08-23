using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace missileoverhaul.lib;

public static class OverhaulMissile
{
    /*
     * Effectively gives every Missile instance an additional field:
     *
     *     MissileGuidance guidance;
     *
     * without actually modifying the compiled Missile class.
     */
    private static readonly ConditionalWeakTable<Missile, OverhaulState> States = new();

    extension(Missile missile)
    {
        public OverhaulState GetOverhaulState()
        {
            return States.GetValue(
                missile,
                static m => new OverhaulState(m)
            );
        }

        public OverhaulGuidance GetGuidance()
        {
            return missile.GetOverhaulState().guidance;
        }

        public void SetGuidance(OverhaulGuidance guidance)
        {
            OverhaulState state = missile.GetOverhaulState();

            if (state.guidance != null)
                state.guidance.Dispose();

            state.guidance = guidance;
        }
        
        public List<OverhaulSeeker> GetSeekers()
        {
            return missile.GetOverhaulState().seekers;
        }

        public OverhaulSeeker? GetSeeker(int index = 0)
        {
            if (index < 0 || index >= missile.GetOverhaulState().seekers.Count)
                return null;
            return missile.GetOverhaulState().seekers[index];
        }

        public void AddSeeker(OverhaulSeeker seeker)
        {
            OverhaulState state = missile.GetOverhaulState();

            state.seekers.Add(seeker);
        }
        
        public void RemoveSeeker(OverhaulSeeker seeker)
        {
            OverhaulState state = missile.GetOverhaulState();

            state.seekers.Remove(seeker);
        }
    }

    public static void Remove(Missile missile)
    {
        if (States.TryGetValue(missile, out OverhaulState state))
        {
            if (state.guidance != null)
                state.guidance.Dispose();
            
            foreach (OverhaulSeeker seeker in state.seekers)
                seeker.Dispose();
        }

        States.Remove(missile);
    }
}

public sealed class OverhaulState
{
    public readonly Missile missile;

    
    public OverhaulGuidance guidance;
    public OverhaulTargeting targeting;
    public List<OverhaulSeeker> seekers = [];

    public OverhaulState(Missile missile)
    {
        this.missile = missile;
    }
}


