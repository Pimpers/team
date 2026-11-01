// Deterministic gameplay random stream. Visual-only randomness keeps using UnityEngine.Random.
public static class RunRng
{
    public static float Value => RunSession.Current.NextFloat();
    public static int Range(int minimum, int maximum) => RunSession.Current.Range(minimum, maximum);
    public static float Range(float minimum, float maximum) => RunSession.Current.Range(minimum, maximum);
}
