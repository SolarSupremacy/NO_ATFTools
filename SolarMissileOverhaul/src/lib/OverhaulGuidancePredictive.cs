namespace missileoverhaul.lib;

public class OverhaulGuidancePredictive : OverhaulGuidance
{
    public OverhaulGuidancePredictive(Missile missile) : base(missile)
    {
        this.missile = missile;
    }

    protected override void Update()
    {
        if (!targetTrack.position.HasValue)
            return;
        SetAimpoint(targetTrack.GetPosition() + TargetLeadVectorWithAccel(), targetTrack.GetVelocity());
    }

}