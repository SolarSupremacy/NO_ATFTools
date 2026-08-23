using UnityEngine;

namespace missileoverhaul.lib;

public class OverhaulUtil
{
    // if (!PlayerSettings.debugVis) return;
    public void DebugVisual(Transform from, Vector3 vector, Color color)
    {
        GameObject debugGameObject = Object.Instantiate(GameAssets.i.debugArrowGreen, from);
        debugGameObject.GetComponent<MeshRenderer>().material.color = color;
        debugGameObject.transform.rotation = Quaternion.LookRotation(vector);
        debugGameObject.transform.localScale = new Vector3(1f, 1f, (vector).magnitude);
        Object.Destroy(debugGameObject, 1f/60f);
    }
}


public struct Track(
    GameObject? reference,
    GlobalPosition? position,
    Vector3? velocity,
    Vector3? acceleration,
    float? time)
{
    public GameObject? reference = reference;
    public GlobalPosition? position = position;
    public Vector3? velocity = velocity;
    public Vector3? acceleration = acceleration;
    public float? time = time;

    public bool HasReference()
    {
        return reference is not null;
    }

    public GlobalPosition GetPosition(GlobalPosition def = default)
    {
        return position ?? def;
    }

    public Vector3 GetVelocity(Vector3 def = default)
    {
        return velocity ?? def;
    }

    public Vector3 GetAcceleration(Vector3 def = default)
    {
        return acceleration ?? def;
    }

    public float GetTime(float def = 0f)
    {
        return time ?? def;
    }

    public Track Predict(float deltaTime = 0.0f)
    {
        if (deltaTime <= 0.0f) return this;
        Vector3 deltaPosition = GetVelocity() * deltaTime + GetAcceleration() * deltaTime * deltaTime * 0.5f;
        Vector3 deltaVelocity = GetAcceleration() * deltaTime;
        return new Track(
            reference,
            GetPosition() + deltaPosition,
            GetVelocity() + deltaVelocity,
            acceleration * 0.9f,
            time + deltaTime
        );
    }

    public GlobalPosition ProjectPosition(float deltaTime = 0.0f)
    {
        if (deltaTime <= 0.0f) return GetPosition();
        return GetPosition() + GetVelocity() * deltaTime + GetAcceleration() * deltaTime * deltaTime * 0.5f;
    }
}


// Target Criteria

public interface ITargetCriterion
{
    bool Matches(Missile missile, Unit candidate);
}


public sealed class TargetCriterionType<T> : ITargetCriterion
    where T : Unit
{
    public bool Matches(Missile missile, Unit candidate)
    {
        return candidate is T;
    }
}


public sealed class TargetCriterionEnemy : ITargetCriterion
{
    public bool Matches(Missile missile, Unit candidate)
    {
        if (candidate.NetworkpersistentID.NotValid)
            return true;
        return candidate.NetworkpersistentID != missile.NetworkpersistentID;
    }
}


// idfk

public struct TargetObservation
{
    public object target;

    public Vector3? position;
    public Vector3? velocity;
    public Vector3? acceleration;

    public Vector3? direction;
    public float? distance;

    public float confidence;
    public float timestamp;
}

public struct NavigationState
{
    public GlobalPosition position;
    public Vector3 velocity;
    public Vector3 acceleration;
    public Quaternion rotation;
    public Vector3 angularVelocity;
}

public struct GuidanceCommand
{
    public Vector3 desiredAcceleration;
    public Vector3 desiredDirection;
    public float desiredSpeed;
}

