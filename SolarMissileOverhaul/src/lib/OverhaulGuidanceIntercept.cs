namespace missileoverhaul.lib;

public class OverhaulGuidanceIntercept : OverhaulGuidance
{
    public OverhaulGuidanceIntercept(Missile missile) : base(missile)
    {
        this.missile = missile;
    }

    protected override void Update()
    {
        if (!targetTrack.position.HasValue)
            return;
        SetAimpoint(targetTrack.GetPosition() + TargetLeadVector(), targetTrack.GetVelocity());
    }

}