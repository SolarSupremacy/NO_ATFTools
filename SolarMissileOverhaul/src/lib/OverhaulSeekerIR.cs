using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace missileoverhaul.lib;

public class OverhaulSeekerIR : OverhaulSeeker
{
    private IRSource? lockedIRSource;
    private PersistentID? lockedUnitID;
    private Unit lockedUnit;

    public OverhaulSeekerIR(Missile missile)
    {
        this.missile = missile;
        seekerType = SeekerType.IR;
    }

    protected override void Initialize()
    {

    }

    protected override void Update()
    {
        // If no lock, return.
        if (lockedIRSource == null) return;

        if (lockedUnitID.HasValue)
        {
            // Locked onto IRSource and Unit.
            guidance.UpdateTarget(new Track(lockedUnit, lockedUnit.GlobalPosition(), lockedUnit.rb.velocity, null, UnityTime));
        }
        else
        {
            // Locked onto IRSource only.
            guidance.UpdateTarget(new Track(null, lockedIRSource.transform.GlobalPosition(), null, null, UnityTime));
        }
    }

    protected override void UpdateSlow()
    {
        if (lockedIRSource != null)
        {
            // If locked onto IRSource, check if lock is maintained.
            if (!IsInSeeker(lockedIRSource.transform.GlobalPosition()))
            {
                lockedIRSource = null;
                lockedUnitID = null;
            }
        }
        else
        {
            // If not locked, try to seek a new target.
            KeyValuePair<IRSource, Unit>? seekResult = Seek();
            if (seekResult.HasValue)
            {
                lockedIRSource = seekResult.Value.Key;
                lockedUnitID = seekResult.Value.Value.persistentID;
                lockedUnit = seekResult.Value.Value;
            }
        }
    }

    protected KeyValuePair<IRSource, Unit>? Seek()
    {
        foreach (Unit unit in UnitRegistry.allUnits
                     .Where(unit => unit.HasIRSignature())
                     .Where(unit => IsInSeeker(unit.GlobalPosition())))
        {
            return new KeyValuePair<IRSource, Unit>(unit.GetIRSource(), unit);
        }

        return null;
    }

    protected List<KeyValuePair<IRSource, Unit>> GetIRSourcesInSeeker()
    {
        return
        [
            .. from unit in UnitRegistry.allUnits.Where(unit => unit.HasIRSignature())
            let source = unit.GetIRSource()
            where IsInSeeker(source.transform.GlobalPosition())
            select new KeyValuePair<IRSource, Unit>(source, unit)
        ];
    }
}


/// <summary>
/// One actual sensor observation.
///
/// This is deliberately separate from Unit because an IR contact
/// can also be something such as a flare.
/// </summary>
public readonly struct IRContact
{
    public readonly Transform Transform;
    public readonly Unit Unit;
    public readonly IRSource Source;

    public readonly bool IsCountermeasure;

    /// <summary>
    /// Intrinsic IR intensity before seeker/environment attenuation.
    /// </summary>
    public readonly float Intensity;

    public IRContact(
        Transform transform,
        Unit unit,
        IRSource source,
        bool isCountermeasure,
        float intensity)
    {
        Transform = transform;
        Unit = unit;
        Source = source;
        IsCountermeasure = isCountermeasure;
        Intensity = intensity;
    }
}