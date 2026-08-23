namespace missileoverhaul.lib;

public class OverhaulGuidancePursuit : OverhaulGuidance
{
    public OverhaulGuidancePursuit(Missile missile) : base(missile)
    {
        this.missile = missile;
    }

    protected override void Update()
    {
        if (!targetTrack.position.HasValue)
            return;
        SetAimpoint(targetTrack.GetPosition(), targetTrack.GetVelocity());
    }

}