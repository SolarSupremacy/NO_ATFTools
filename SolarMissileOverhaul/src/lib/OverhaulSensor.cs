namespace missileoverhaul.lib;


public abstract class Sensor
{
    // Parameters
    protected float maxRange = 15000f; // Absolute maximum range of the seeker.
    protected float minRange = 0f; // Minimum range for detection.
    protected float expectedRange = 10000f; // Range at which seeker will attempt to locate target.
    protected float seekerFOV = 20f; // Field of view of the seeker in degrees.
}

public class SensorIR : Sensor
{

}

public class SensorARH : Sensor
{
    protected RadarMode radarMode;

    public enum RadarMode
    {

    }
}