using System.Collections.Generic;
using UnityEngine;

namespace missileoverhaul.lib;





public interface ITargetSensor
{
    bool IsOperational { get; }

    void TickSensor(float deltaTime);

    IEnumerable<TargetObservation> GetObservations();
}


public interface ITrackManager
{
    TargetTrack PrimaryTrack { get; }

    void SubmitObservation(TargetObservation observation);

    void Tick(float deltaTime);

    bool TryGetTrack(object target, out TargetTrack track);
}


public interface IGuidanceLaw
{
    GlobalPosition Calculate(
        NavigationState missile,
        GuidanceObjective objective);
}


public struct GuidanceObjective
{
    public GuidanceObjectiveType type;

    public Track target;

    public Vector3 position;
    public Vector3 velocity;

    public Vector3 direction;
}


public enum GuidanceObjectiveType
{
    None,
    TargetTrack,
    Position,
    Waypoint,
    Direction,
    Beam,
    ImpactPoint
}