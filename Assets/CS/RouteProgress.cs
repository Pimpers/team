// Existing route API backed by RunSession.
public static class RouteProgress
{
    static RunSession S => RunSession.Current;
    public static int SafeChoices => S.SafeChoices;
    public static int HuntChoices => S.HuntChoices;

    public static void Reset() => S.ResetRoutes();

    public static void SetNextRoute(bool hunt)
    {
        S.HasNextRoute = true;
        S.NextRouteHunt = hunt;
    }

    public static bool TryTakeNextRoute(out bool hunt)
    {
        hunt = S.NextRouteHunt;
        if (!S.HasNextRoute) return false;
        S.HasNextRoute = false;
        return true;
    }

    public static void ChooseSafe() => S.SafeChoices++;

    public static bool ChooseHunt()
    {
        S.HuntChoices++;
        if (S.HuntChoices % 2 != 0) return false;
        S.RareDraftCharges++;
        return true;
    }

    public static bool TryConsumeRareDraft(bool canOfferRare)
    {
        if (!canOfferRare || S.RareDraftCharges <= 0) return false;
        S.RareDraftCharges--;
        return true;
    }
}
