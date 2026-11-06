namespace SelfRestaurant.Catalog.Api.Infrastructure;

/// <summary>Address and map position of a branch, entered by the admin (owner 06/11). Both coordinates or neither.</summary>
public static class BranchLocationRules
{
    public const int MaxAddressLength = 500;

    /// <summary>Null when valid; otherwise the message shown to the admin.</summary>
    public static string? Validate(string? address, decimal? latitude, decimal? longitude)
    {
        if (address is not null && address.Trim().Length > MaxAddressLength) return $"Địa chỉ tối đa {MaxAddressLength} ký tự.";
        if (latitude is null != longitude is null) return "Cần nhập cả vĩ độ và kinh độ, hoặc để trống cả hai.";
        if (latitude is null) return null;
        if (latitude < -90 || latitude > 90) return "Vĩ độ phải từ -90 đến 90.";
        if (longitude < -180 || longitude > 180) return "Kinh độ phải từ -180 đến 180.";
        if (latitude == 0 && longitude == 0) return "Toạ độ 0, 0 không phải vị trí chi nhánh.";
        if (decimal.Round(latitude!.Value, 6) != latitude || decimal.Round(longitude!.Value, 6) != longitude)
            return "Toạ độ tối đa 6 chữ số thập phân.";
        return null;
    }
}
