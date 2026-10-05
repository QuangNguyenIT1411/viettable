namespace SelfRestaurant.Gateway.Api.Infrastructure.Media;

public static class MediaStorageConfiguration
{
    public static void Configure(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["MediaStorage:Provider"] ?? "Local";
        if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IDishMediaStorage, LocalDishMediaStorage>();
        else if (provider.Equals("Supabase", StringComparison.OrdinalIgnoreCase))
        {
            // Validate before serving traffic; values never appear in configuration errors.
            var settings = SupabaseMediaSettings.FromConfiguration(configuration);
            services.AddSingleton(settings);
            services.AddHttpClient<IDishMediaStorage, SupabaseDishMediaStorage>(http =>
                http.Timeout = TimeSpan.FromSeconds(30))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        else throw new InvalidOperationException("Unsupported MediaStorage provider.");
    }
}
