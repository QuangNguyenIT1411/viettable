namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>Great-circle distance in km between the guest and a branch; null when either position is unknown.</summary>
public static class GeoDistance
{
    public static double? Km(double? fromLat, double? fromLng, decimal? toLat, decimal? toLng)
    {
        if (fromLat is null || fromLng is null || toLat is null || toLng is null) return null;
        if (fromLat is < -90 or > 90 || fromLng is < -180 or > 180) return null;
        const double earthKm = 6371.0088;
        double rad(double d) => d * Math.PI / 180;
        var lat1 = rad(fromLat.Value); var lat2 = rad((double)toLat.Value);
        var dLat = lat2 - lat1; var dLng = rad((double)toLng.Value - fromLng.Value);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLng / 2), 2);
        return Math.Round(2 * earthKm * Math.Asin(Math.Min(1, Math.Sqrt(a))), 1);
    }
}
