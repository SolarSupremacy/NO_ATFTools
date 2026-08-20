using System.Collections.Generic;
using System.Linq;
using System;
using HarmonyLib;
using UnityEngine;
using Debug = System.Diagnostics.Debug;
using Random = UnityEngine.Random;

namespace missileoverhaul.patchSubclass;


// ARHSeekerOverhaul: Drop-in replacement for ARHSeeker with better functionality.
public class ARHSeekerPatch_Backup : ARHSeeker
{
    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        lockPerseveranceRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("lockPerseverance");

    private static readonly AccessTools.FieldRef<ARHSeeker, bool>
        homeOnJamRef =
            AccessTools.FieldRefAccess<ARHSeeker, bool>("homeOnJam");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        homingLockDelayRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("homingLockDelay");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        maxDatalinkAngleRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("maxDatalinkAngle");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        minReacquireRangeRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("minReacquireRange");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        datalinkPositionalErrorRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("datalinkPositionalError");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        maxTrackingAngleRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("maxTrackingAngle");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        armDelayRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("armDelay");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        guidanceDelayRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("guidanceDelay");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        terminalRangeRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("terminalRange");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        maxLeadRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("maxLead");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        selfDestructAtSpeedRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("selfDestructAtSpeed");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        loftAmountRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("loftAmount");

    private static readonly AccessTools.FieldRef<ARHSeeker, RadarParams>
        radarParametersRef =
            AccessTools.FieldRefAccess<ARHSeeker, RadarParams>("radarParameters");

    private static readonly AccessTools.FieldRef<ARHSeeker, float>
        jamToleranceRef =
            AccessTools.FieldRefAccess<ARHSeeker, float>("jamTolerance");

    private static readonly AccessTools.FieldRef<ARHSeeker, JinkEvasion>
        jinkEvasionRef =
            AccessTools.FieldRefAccess<ARHSeeker, JinkEvasion>("jinkEvasion");
    

    private float lockPerseverance =>
        lockPerseveranceRef(this);

    private bool homeOnJam =>
        homeOnJamRef(this);

    private float homingLockDelay =>
        homingLockDelayRef(this);

    private float maxDatalinkAngle =>
        maxDatalinkAngleRef(this);

    private float minReacquireRange =>
        minReacquireRangeRef(this);

    private float datalinkPositionalError =>
        datalinkPositionalErrorRef(this);

    private float maxTrackingAngle =>
        maxTrackingAngleRef(this);

    private float armDelay =>
        armDelayRef(this);

    private float guidanceDelay =>
        guidanceDelayRef(this);

    private float terminalRange =>
        terminalRangeRef(this);

    private float maxLead =>
        maxLeadRef(this);

    private float selfDestructAtSpeed =>
        selfDestructAtSpeedRef(this);

    private float loftAmount =>
        loftAmountRef(this);

    private RadarParams radarParameters =>
        radarParametersRef(this);

    private float jamTolerance =>
        jamToleranceRef(this);

    private JinkEvasion jinkEvasion =>
        jinkEvasionRef(this);
    

    private GlobalPosition knownPos;
    private Vector3 knownVel;
    private Vector3 knownVelPrev;
    private Vector3 knownAccel;

    private float lastActiveTrackAttempt;
    private float lastDatalinkTrackAttempt;
    private float timeWithoutReturn;
    private float returnStrength;
    private float homingLockTime;
    private float jamAccumulation;
    private float topSpeed;
    private float targetDist;
    private float timeToTarget;

    private Vector3 positionalErrorVector;

    private bool armed;
    private bool guidance;
    private bool isJammed;
    private bool radarLockEstablished;
    private bool achievedLock;
    private bool multipleInbound;

    public override void Initialize(Unit target, GlobalPosition aimpoint)
    {
        Plugin.Log?.LogDebug($"ARHSeekerPatch.Initialize: target={target}, aimpoint={aimpoint}");
        this.missile.NetworkseekerMode = Missile.SeekerMode.passive;
        this.positionalErrorVector = UnityEngine.Random.insideUnitSphere * this.datalinkPositionalError;
        this.missile.onJam += new Action<Unit.JamEventArgs>(this.ARHSeeker_OnJam);
        this.lastActiveTrackAttempt = Time.timeSinceLevelLoad - 0.9f;
        this.topSpeed = this.missile.GetTopSpeed(0.0f, 0.0f);
        this.targetUnit = target;
        if (target is IRadarReturn)
            target.onAddRadarChaff += new Action<RadarChaff>(this.ARHSeeker_OnChaff);
        this.knownPos = this.missile.GlobalPosition() + this.missile.transform.forward * 100000f;
        if ((UnityEngine.Object) this.targetUnit != (UnityEngine.Object) null && (UnityEngine.Object) this.missile.NetworkHQ != (UnityEngine.Object) null)
        {
            this.missile.NetworkHQ.TryGetKnownPosition(this.targetUnit, out this.knownPos);
            this.knownPos += this.positionalErrorVector;
            if (this.proximityFuse)
                this.missile.SetProxyFuse(target.GetRandomPart().transform, target.rb);
        }
        this.missile.SetAimpoint(this.knownPos, Vector3.zero);
        this.StartSlowUpdate(1f, new Action(this.SlowChecks));
    }
    
    private void SlowChecks()
  {
    if (this.missile.disabled)
      return;
    if (((double) this.missile.timeSinceSpawn > 10.0 || !this.missile.EngineOn() && (double) this.missile.timeSinceSpawn > 2.0) && (this.missile.LosingGround() || this.missile.MissedTarget() || (double) this.missile.speed < (double) this.selfDestructAtSpeed || (UnityEngine.Object) this.targetUnit == (UnityEngine.Object) null))
      this.missile.Detonate(this.missile.rb.velocity, false, false);
    if ((UnityEngine.Object) this.missile.NetworkHQ != (UnityEngine.Object) null && (UnityEngine.Object) this.targetUnit != (UnityEngine.Object) null && !this.targetUnit.disabled && (UnityEngine.Object) this.targetUnit.NetworkHQ != (UnityEngine.Object) this.missile.NetworkHQ)
    {
      TrackingInfo trackingData = this.missile.NetworkHQ.GetTrackingData(this.targetUnit.persistentID);
      this.multipleInbound = this.targetUnit is Aircraft && trackingData.missileAttacks > (sbyte) 1;
    }
    if ((double) this.loftAmount <= 0.0)
      return;
    Vector3 vector3 = this.knownPos - this.missile.GlobalPosition();
    float a = Vector3.Dot(vector3.normalized, this.missile.rb.velocity);
    GlobalPosition knownPosition;
    if ((UnityEngine.Object) this.targetUnit != (UnityEngine.Object) null && this.missile.NetworkHQ.TryGetKnownPosition(this.targetUnit, out knownPosition))
      vector3 = knownPosition - this.missile.GlobalPosition();
    this.targetDist = vector3.magnitude;
    this.timeToTarget = this.targetDist / Mathf.Max(a, 10f);
  }

  public override string GetSeekerType() => "ARH";

  private void ARHSeeker_OnJam(Unit.JamEventArgs e)
  {
    if ((double) Vector3.Angle(e.jammingUnit.GlobalPosition() - this.missile.GlobalPosition(), this.missile.transform.forward) > (double) this.maxTrackingAngle)
      return;
    this.jamAccumulation += e.jamAmount;
    this.missile.RecordDamage(e.jammingUnit.persistentID, 0.01f);
    if ((double) this.jamAccumulation < (double) this.jamTolerance || !this.homeOnJam)
      return;
    this.missile.SetTarget(e.jammingUnit);
    this.targetUnit = e.jammingUnit;
    this.knownPos = e.jammingUnit.GlobalPosition();
    this.knownVel = e.jammingUnit.rb.velocity;
    this.radarLockEstablished = false;
  }

  private void ARHSeeker_OnChaff(RadarChaff source)
  {
    if (this.missile.targetID.NotValid || this.missile.seekerMode != Missile.SeekerMode.activeLock)
      return;
    float num1 = this.RangeCoef(FastMath.Distance(this.transform.position, this.targetUnit.transform.position));
    float num2 = Mathf.Clamp01(1f - Mathf.Abs(Vector3.Dot(FastMath.NormalizedDirection(this.transform.position, this.targetUnit.transform.position), FastMath.NormalizedDirection(source.transform.position, this.targetUnit.transform.position))));
    float num3 = (float) ((double) num1 * (double) num2 / (1.0 + (double) this.jamTolerance));
    this.jamAccumulation += num3;
    Plugin.Log?.LogDebug((object) $"Target chaff angle: {num2:F2}, range coeff: {num1:F2}, dazzle : {num3:F2} jam: {this.jamAccumulation:F2} success {(double) this.jamAccumulation > (double) this.jamTolerance}");
  }

  private float RangeCoef(float targetDistance)
  {
    float maxRange = this.radarParameters.maxRange;
    return Mathf.Clamp01(1f - targetDistance / maxRange);
  }

  private float GetRadarReturn()
  {
    if ((double) Time.timeSinceLevelLoad - (double) this.lastActiveTrackAttempt < 0.5)
      return this.returnStrength;
    this.lastActiveTrackAttempt = Time.timeSinceLevelLoad;
    if (!(this.targetUnit is IRadarReturn targetUnit) || this.isJammed && !this.homeOnJam || (double) this.returnStrength == 0.0 && (double) this.targetDist < 5000.0 && (double) targetUnit.GetECMIntensity() > 2.0)
      return 0.0f;
    GlobalPosition a = this.missile.GlobalPosition();
    GlobalPosition b = this.targetUnit.GlobalPosition();
    Vector3 vector3 = (b - a) with { y = 0.0f };
    float num1 = FastMath.Distance(a, b);
    float magnitude = vector3.magnitude;
    float num2 = Mathf.Sqrt(12742000f * a.y);
    float num3 = Mathf.Sqrt(12742000f * b.y);
    if ((double) num2 + (double) num3 < (double) num1 || !TargetCalc.LineOfSight(this.transform, this.targetUnit.transform, 10f))
      return -1f;
    if ((double) num1 > (double) this.radarParameters.maxRange || (double) this.returnStrength < (double) this.radarParameters.minSignal && (double) num1 < (double) this.minReacquireRange || (double) Vector3.Angle(this.transform.forward, this.targetUnit.transform.position - this.transform.position) > (double) this.maxTrackingAngle)
      return 0.0f;
    float num4 = 0.0f;
    if ((double) magnitude < (double) num2 && (double) b.y < (double) a.y * (1.0 - (double) magnitude / (double) num2))
    {
      float num5 = (float) ((double) num1 * (double) this.targetUnit.radarAlt / ((double) a.y - (double) b.y));
      num4 += Mathf.Min(num1, 1000f) / num5;
    }
    float clutter = num4 + (float) ((double) this.targetUnit.maxRadius * (double) this.targetUnit.maxRadius * 2.0 / ((double) this.targetUnit.radarAlt * (double) this.targetUnit.radarAlt));
    return targetUnit.GetRadarReturn(this.missile.transform.position, (Radar) null, (Unit) this.missile, num1, clutter, this.radarParameters, true);
  }

  public override void Seek()
  {
    if (this.missile.targetID.NotValid)
    {
      this.knownPos += this.knownVel * Time.fixedDeltaTime;
      this.missile.SetAimpoint(this.knownPos, Vector3.zero);
    }
    else
    {
      if (!this.armed && (double) this.missile.timeSinceSpawn > (double) this.armDelay)
      {
        this.armed = true;
        this.missile.Arm();
        this.missile.SetTangible(true);
      }
      if (!this.guidance && (double) this.missile.timeSinceSpawn > (double) this.guidanceDelay)
      {
        this.guidance = true;
        this.missile.DeployFins();
      }
      this.jamAccumulation -= Mathf.Max(this.jamAccumulation, 0.2f) * Mathf.Max(this.jamTolerance, 0.1f) * Time.deltaTime;
      this.jamAccumulation = Mathf.Clamp01(this.jamAccumulation);
      this.isJammed = (double) this.jamAccumulation > (double) this.jamTolerance;
      if ((UnityEngine.Object) this.targetUnit == (UnityEngine.Object) null)
      {
        this.missile.SetTarget((Unit) null);
        this.missile.SetAimpoint(this.missile.GlobalPosition() + this.missile.transform.forward * 10000f, Vector3.zero);
      }
      else if (!this.guidance)
      {
        this.missile.SetAimpoint(this.missile.GlobalPosition() + this.missile.transform.forward * 10000f, Vector3.zero);
      }
      else
      {
        if (!this.radarLockEstablished)
        {
          this.DatalinkMode();
          this.knownPos += this.knownVel * Time.fixedDeltaTime;
        }
        else
          this.TerminalMode();
        Vector3 platformVel = (double) this.missile.timeSinceSpawn < 3.0 ? this.missile.transform.forward * this.topSpeed : this.missile.rb.velocity;
        Vector3 leadVectorWithAccel = TargetCalc.GetLeadVectorWithAccel(this.knownPos, this.missile.GlobalPosition(), this.knownVel, platformVel, this.knownAccel, this.maxLead);
        if ((double) this.loftAmount > 0.0)
        {
          if ((double) this.missile.timeSinceSpawn < 3.0)
            this.timeToTarget = this.targetDist / this.topSpeed;
          float num = Mathf.Min((float) ((double) this.timeToTarget * (double) this.timeToTarget * 4.90500020980835) * this.loftAmount, this.targetDist * this.loftAmount);
          leadVectorWithAccel += num * Vector3.up;
          this.timeToTarget -= Time.fixedDeltaTime;
        }
        GlobalPosition aimPoint = this.knownPos + leadVectorWithAccel;
        float num1 = Mathf.InverseLerp(3f, 10f, this.missile.timeSinceSpawn);
        if ((double) this.jinkEvasion.amount > 0.0 && this.multipleInbound && (double) this.targetDist > (double) this.terminalRange)
          aimPoint += num1 * this.jinkEvasion.ApplyJink(this.transform.GlobalPosition(), this.knownPos, this.missile.speed, this.targetDist);
        aimPoint.y = Mathf.Max(aimPoint.y, 0.0f);
        this.missile.SetAimpoint(aimPoint, this.knownVel);
        if (!PlayerSettings.debugVis)
          return;
        GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(GameAssets.i.debugArrowGreen, this.missile.transform);
        gameObject.transform.rotation = Quaternion.LookRotation(aimPoint - this.missile.GlobalPosition());
        gameObject.transform.localScale = new Vector3(1f, 1f, (aimPoint - this.missile.GlobalPosition()).magnitude);
        UnityEngine.Object.Destroy((UnityEngine.Object) gameObject, 0.05f);
      }
    }
  }

  private void DatalinkMode()
  {
    if ((double) Time.timeSinceLevelLoad - (double) this.lastDatalinkTrackAttempt < 1.0)
      return;
    this.lastDatalinkTrackAttempt = Time.timeSinceLevelLoad;
    if ((double) FastMath.Distance(this.knownPos, this.missile.GlobalPosition()) < (double) this.terminalRange)
    {
      this.returnStrength = this.GetRadarReturn();
      Missile.SeekerMode seekerMode = (double) this.returnStrength > (double) this.radarParameters.minSignal ? Missile.SeekerMode.activeLock : Missile.SeekerMode.activeSearch;
      if (this.missile.seekerMode != seekerMode)
        this.missile.NetworkseekerMode = seekerMode;
    }
    if ((double) this.returnStrength > (double) this.radarParameters.minSignal)
    {
      this.knownPos = this.targetUnit.GlobalPosition();
      this.knownVel = (UnityEngine.Object) this.targetUnit.rb != (UnityEngine.Object) null ? this.targetUnit.rb.velocity : Vector3.zero;
      this.radarLockEstablished = true;
      if (this.achievedLock || !(this.targetUnit is Aircraft targetUnit))
        return;
      targetUnit.RecordDamage(this.missile.ownerID, 1f / 1000f);
      this.achievedLock = true;
    }
    else
    {
      if (this.missile.NetworkHQ.IsTargetBeingTracked(this.targetUnit))
        this.knownVel = (UnityEngine.Object) this.targetUnit.rb != (UnityEngine.Object) null ? this.targetUnit.rb.velocity : Vector3.zero;
      if (this.missile.NetworkHQ.IsTargetPositionAccurate(this.targetUnit, 2000f))
      {
        this.knownPos = this.missile.NetworkHQ.GetKnownPosition(this.targetUnit).Value;
        this.knownPos += this.positionalErrorVector;
      }
      if ((double) this.maxDatalinkAngle < 180.0 && (double) Vector3.Angle(this.missile.transform.forward, FastMath.Direction(this.missile.GlobalPosition(), this.knownPos)) > (double) this.maxDatalinkAngle)
        this.knownPos = this.missile.GlobalPosition() + this.missile.transform.forward * 10000f;
      if (FastMath.InRange(this.knownPos, this.targetUnit.GlobalPosition(), 2000f))
        return;
      this.missile.SetTarget((Unit) null);
      this.targetUnit = (Unit) null;
    }
  }

  private void TerminalMode()
  {
    this.returnStrength = this.GetRadarReturn();
    Missile.SeekerMode seekerMode = (double) this.returnStrength > (double) this.radarParameters.minSignal ? Missile.SeekerMode.activeLock : Missile.SeekerMode.activeSearch;
    if (this.missile.seekerMode != seekerMode)
      this.missile.NetworkseekerMode = seekerMode;
    if ((double) this.returnStrength < (double) this.radarParameters.minSignal)
    {
      if ((double) this.returnStrength == -1.0)
        this.missile.SetTarget((Unit) null);
      this.homingLockTime = 0.0f;
      this.timeWithoutReturn += Time.deltaTime;
      GlobalPosition knownPosition;
      if (this.missile.NetworkHQ.TryGetKnownPosition(this.targetUnit, out knownPosition))
        this.knownPos = knownPosition + this.positionalErrorVector;
      if ((double) Vector3.Angle(this.knownPos - this.missile.GlobalPosition(), this.missile.transform.forward) > (double) this.maxTrackingAngle)
        this.knownPos = this.missile.GlobalPosition() + this.missile.transform.forward * 1000f;
      else
        this.knownPos += this.knownVel * Time.fixedDeltaTime;
      if ((double) this.timeWithoutReturn <= (double) this.lockPerseverance)
        return;
      this.missile.SetTarget((Unit) null);
    }
    else
    {
      this.homingLockTime += Time.fixedDeltaTime;
      this.timeWithoutReturn = 0.0f;
      if ((double) this.homingLockTime > (double) this.homingLockDelay)
      {
        this.knownPos = this.targetUnit.GlobalPosition();
        this.knownVel = (UnityEngine.Object) this.targetUnit.rb != (UnityEngine.Object) null ? this.targetUnit.rb.velocity : Vector3.zero;
        this.knownAccel = (this.knownVel - this.knownVelPrev) / Time.fixedDeltaTime;
        this.knownVelPrev = this.knownVel;
      }
      this.missile.SetTarget(this.targetUnit);
    }
  }

  public RadarParams GetRadarParams() => this.radarParameters;
  
}