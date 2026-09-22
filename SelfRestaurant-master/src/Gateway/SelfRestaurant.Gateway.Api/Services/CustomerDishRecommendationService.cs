using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using SelfRestaurant.Gateway.Api.Models;

namespace SelfRestaurant.Gateway.Api.Services;

public sealed class CustomerDishRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomerDishRecommendationService> _logger;

    public CustomerDishRecommendationService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IConfiguration configuration,
        ILogger<CustomerDishRecommendationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CustomerDishRecommendationDto>> GetRecommendationsAsync(
        CustomerSessionUserDto? customer,
        CustomerTableContextDto tableContext,
        MenuResponse menu,
        IReadOnlyList<int> topDishIds,
        ActiveOrderResponse? activeOrder,
        IReadOnlyList<int>? cartDishIds,
        CancellationToken cancellationToken)
    {
        var rankedCandidates = RankCandidates(menu, topDishIds, activeOrder, cartDishIds);
        var fallback = BuildFallbackRecommendations(rankedCandidates);
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return fallback;
        }

        var cacheKey = BuildCacheKey(customer, tableContext, menu, topDishIds, activeOrder, cartDishIds);
        if (_memoryCache.TryGetValue<IReadOnlyList<CustomerDishRecommendationDto>>(cacheKey, out var cachedRecommendations)
            && cachedRecommendations is not null)
        {
            return cachedRecommendations;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(ResolveRecommendationTimeoutMs()));

            var aiRecommendations = await TryGetGeminiRecommendationsAsync(
                apiKey,
                customer,
                tableContext,
                topDishIds,
                activeOrder,
                cartDishIds,
                rankedCandidates,
                timeoutCts.Token);

            var finalRecommendations = aiRecommendations.Count > 0 ? aiRecommendations : fallback;
            CacheRecommendations(cacheKey, finalRecommendations);
            return finalRecommendations;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Gemini recommendations timed out. Using deterministic fallback.");
            CacheRecommendations(cacheKey, fallback);
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini recommendations failed. Using deterministic fallback.");
            CacheRecommendations(cacheKey, fallback);
            return fallback;
        }
    }

    private string ResolveGeminiModel()
        => _configuration["Gemini:Model"] ?? "gemini-1.5-flash";

    private int ResolveRecommendationTimeoutMs()
        => Math.Clamp(_configuration.GetValue<int?>("Gemini:RecommendationTimeoutMs") ?? 2500, 800, 10000);

    private int ResolveCacheTtlMinutes()
        => Math.Clamp(_configuration.GetValue<int?>("Gemini:RecommendationCacheMinutes") ?? 15, 10, 30);

    private int ResolveCacheTimeBucketMinutes()
        => Math.Clamp(_configuration.GetValue<int?>("Gemini:RecommendationCacheBucketMinutes") ?? 15, 10, 30);

    private int ResolveCandidateCount()
        => Math.Clamp(_configuration.GetValue<int?>("Gemini:CandidateCount") ?? 12, 8, 20);

    private async Task<IReadOnlyList<CustomerDishRecommendationDto>> TryGetGeminiRecommendationsAsync(
        string apiKey,
        CustomerSessionUserDto? customer,
        CustomerTableContextDto tableContext,
        IReadOnlyList<int> topDishIds,
        ActiveOrderResponse? activeOrder,
        IReadOnlyList<int>? cartDishIds,
        IReadOnlyList<RankedRecommendationCandidate> rankedCandidates,
        CancellationToken cancellationToken)
    {
        var candidates = rankedCandidates.Take(ResolveCandidateCount()).ToArray();
        if (candidates.Length == 0)
        {
            return Array.Empty<CustomerDishRecommendationDto>();
        }

        var client = _httpClientFactory.CreateClient("Gemini");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1/models/{ResolveGeminiModel()}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);

        var prompt = BuildPrompt(customer, tableContext, topDishIds, activeOrder, cartDishIds, candidates);
        var requestBody = new GeminiGenerateContentRequest(
            [
                new GeminiContentRequest(
                    "user",
                    [
                        new GeminiPartRequest(prompt)
                    ])
            ]);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseText, JsonOptions);
        var rawJson = geminiResponse?.Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .Select(x => x.Text)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return Array.Empty<CustomerDishRecommendationDto>();
        }

        var cleanedJson = ExtractJsonObject(rawJson);
        if (string.IsNullOrWhiteSpace(cleanedJson))
        {
            return Array.Empty<CustomerDishRecommendationDto>();
        }

        var parsed = JsonSerializer.Deserialize<RecommendationEnvelope>(cleanedJson, JsonOptions);
        if (parsed?.Recommendations is null || parsed.Recommendations.Count == 0)
        {
            return Array.Empty<CustomerDishRecommendationDto>();
        }

        return FinalizeRecommendations(parsed.Recommendations, rankedCandidates);
    }

    private static string BuildPrompt(
        CustomerSessionUserDto? customer,
        CustomerTableContextDto tableContext,
        IReadOnlyList<int> topDishIds,
        ActiveOrderResponse? activeOrder,
        IReadOnlyList<int>? cartDishIds,
        IReadOnlyList<RankedRecommendationCandidate> candidates)
    {
        var timeContext = BuildTimeContext(DateTime.Now);
        var normalizedCartDishIds = (cartDishIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().OrderBy(x => x).ToArray();

        var promptPayload = new
        {
            timeContext = new
            {
                timeContext.TimeSlot,
                timeContext.Label,
                timeContext.Hour,
            },
            branch = new
            {
                tableContext.BranchId,
                tableContext.BranchName,
                tableContext.TableId,
                tableContext.TableNumber,
            },
            customer = customer is null
                ? null
                : new
                {
                    customer.CustomerId,
                    customer.LoyaltyPoints,
                },
            topDishIds,
            activeOrder = activeOrder is null
                ? null
                : new
                {
                    activeOrder.OrderId,
                    activeOrder.StatusCode,
                    items = activeOrder.Items.Select(x => new
                    {
                        x.DishId,
                        x.DishName,
                        x.Quantity,
                        x.Note,
                    })
                },
            localCartDishIds = normalizedCartDishIds,
            dishes = candidates.Select(x => new
            {
                x.Dish.DishId,
                x.Dish.Name,
                Description = TrimForPrompt(x.Dish.Description, 120),
                x.Dish.CategoryId,
                x.Dish.CategoryName,
                x.Dish.Price,
                x.Dish.Unit,
                x.Dish.IsVegetarian,
                x.Dish.IsDailySpecial,
                x.Dish.IsTopSeller,
                x.Score,
                x.Reason,
            })
        };

        return
            """
            You are a restaurant assistant helping choose dishes for today.

            Recommend at most 5 dishes from the provided list.

            Goals:
            - Use branch, menu, categories, vegetarian/special flags, best-selling dishes, time of day, active order items, and local cart dish IDs when present.
            - Make the set balanced and diverse.
            - Avoid recommending multiple dishes that feel too similar.
            - Best-selling dishes are only one signal, not the whole answer.

            Return ONLY valid JSON:
            {
              "recommendations": [
                { "dishId": number, "reason": string }
              ]
            }

            Rules:
            - Use only dishId values from the provided dishes list.
            - Return at most 5 dishes.
            - Prefer variety across categories when practical.
            - If the current selection suggests a preference, reflect it without repeating the same dish.
            - Each reason must be short, natural, human-readable, in Vietnamese, and ideally under 90 characters.
            - Do not include markdown or any text outside JSON.

            Data:
            """
            + "\n" + JsonSerializer.Serialize(promptPayload, JsonOptions);
    }

    private static string? ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }

    private static IReadOnlyList<CustomerDishRecommendationDto> BuildFallbackRecommendations(IReadOnlyList<RankedRecommendationCandidate> rankedCandidates)
        => FinalizeRecommendations(Array.Empty<RecommendationItem>(), rankedCandidates);

    private static IReadOnlyList<RankedRecommendationCandidate> RankCandidates(
        MenuResponse menu,
        IReadOnlyList<int> topDishIds,
        ActiveOrderResponse? activeOrder,
        IReadOnlyList<int>? cartDishIds)
    {
        var dishes = menu.Categories
            .SelectMany(category => category.Dishes.Select(dish => new RecommendationCandidate(
                dish.DishId,
                dish.Name,
                dish.Description,
                dish.Image,
                category.CategoryId,
                category.CategoryName,
                dish.Unit ?? string.Empty,
                dish.IsVegetarian,
                dish.IsDailySpecial,
                topDishIds.Contains(dish.DishId),
                dish.Available,
                dish.Price,
                dish.Ingredients)))
            .Where(candidate => candidate.Available)
            .ToList();

        var dishLookup = dishes.ToDictionary(x => x.DishId);
        var categoryLookup = dishes.ToDictionary(x => x.DishId, x => x.CategoryId);
        var activeOrderDishIds = activeOrder?.Items.Select(x => x.DishId).ToArray() ?? Array.Empty<int>();
        var normalizedCartDishIds = (cartDishIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToArray();
        var contextDishIds = activeOrderDishIds.Concat(normalizedCartDishIds).Distinct().ToArray();
        var favoriteCategories = contextDishIds
            .Select(x => categoryLookup.TryGetValue(x, out var categoryId) ? categoryId : 0)
            .Where(x => x > 0)
            .ToHashSet();
        var existingDishIds = contextDishIds.ToHashSet();
        var prefersVegetarian = contextDishIds.Length > 0 && contextDishIds.All(x =>
            dishLookup.TryGetValue(x, out var candidate) &&
            candidate.IsVegetarian);
        var timeContext = BuildTimeContext(DateTime.Now);

        return dishes
            .Select(dish =>
            {
                var score = 0;
                var reason = $"Phù hợp cho {timeContext.Label.ToLowerInvariant()}";

                if (dish.IsTopSeller)
                {
                    score += 5;
                    reason = "Được nhiều khách gọi hôm nay";
                }

                if (dish.IsDailySpecial)
                {
                    score += 4;
                    reason = "Món đặc biệt trong ngày";
                }

                if (favoriteCategories.Contains(dish.CategoryId))
                {
                    score += 3;
                    reason = "Hợp vị với món bạn đang chọn";
                }

                if (prefersVegetarian && dish.IsVegetarian)
                {
                    score += 2;
                    reason = "Phù hợp với lựa chọn món chay";
                }

                if ((timeContext.TimeSlot == "lunch" || timeContext.TimeSlot == "dinner") && !dish.IsVegetarian)
                {
                    score += 1;
                }

                if (existingDishIds.Contains(dish.DishId))
                {
                    score -= 4;
                }

                return new RankedRecommendationCandidate(dish, score, reason);
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Dish.IsTopSeller)
            .ThenByDescending(x => x.Dish.IsDailySpecial)
            .ThenBy(x => x.Dish.Price)
            .ToArray();
    }

    private string BuildCacheKey(
        CustomerSessionUserDto? customer,
        CustomerTableContextDto tableContext,
        MenuResponse menu,
        IReadOnlyList<int> topDishIds,
        ActiveOrderResponse? activeOrder,
        IReadOnlyList<int>? cartDishIds)
    {
        var timeBucket = ResolveTimeBucketStart(DateTime.Now, ResolveCacheTimeBucketMinutes());
        var rawSignature = JsonSerializer.Serialize(new
        {
            tableContext.BranchId,
            timeBucket,
            timeSlot = BuildTimeContext(DateTime.Now).TimeSlot,
            hasCustomer = customer is not null,
            topDishIds,
            menu = menu.Categories.SelectMany(category => category.Dishes.Select(dish => new
            {
                category.CategoryId,
                dish.DishId,
                dish.Price,
                dish.Available,
                dish.IsDailySpecial,
                dish.IsVegetarian
            })),
            activeOrder = activeOrder?.Items.Select(item => new
            {
                item.DishId,
                item.Quantity,
            }),
            cartDishIds = cartDishIds?.Where(x => x > 0).Distinct().OrderBy(x => x),
        }, JsonOptions);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSignature)));
        return $"dish-recommendations:{hash}";
    }

    private void CacheRecommendations(string cacheKey, IReadOnlyList<CustomerDishRecommendationDto> recommendations)
    {
        _memoryCache.Set(
            cacheKey,
            recommendations,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ResolveCacheTtlMinutes())
            });
    }

    private static IReadOnlyList<CustomerDishRecommendationDto> FinalizeRecommendations(
        IReadOnlyList<RecommendationItem> aiRecommendations,
        IReadOnlyList<RankedRecommendationCandidate> rankedCandidates)
    {
        var rankedLookup = rankedCandidates.ToDictionary(x => x.Dish.DishId);
        var selectedDishIds = new HashSet<int>();
        var selectedCategoryIds = new HashSet<int>();
        var selected = new List<CustomerDishRecommendationDto>(6);

        void TryAdd(int dishId, string? reason, bool preferNewCategory)
        {
            if (selected.Count >= 6 || !rankedLookup.TryGetValue(dishId, out var candidate) || selectedDishIds.Contains(dishId))
            {
                return;
            }

            if (preferNewCategory && selectedCategoryIds.Contains(candidate.Dish.CategoryId))
            {
                return;
            }

            selectedDishIds.Add(dishId);
            selectedCategoryIds.Add(candidate.Dish.CategoryId);
            selected.Add(ToRecommendationDto(candidate, reason ?? candidate.Reason));
        }

        foreach (var item in aiRecommendations)
        {
            TryAdd(item.DishId, item.Reason, preferNewCategory: true);
        }

        foreach (var item in aiRecommendations)
        {
            TryAdd(item.DishId, item.Reason, preferNewCategory: false);
        }

        foreach (var candidate in rankedCandidates)
        {
            TryAdd(candidate.Dish.DishId, candidate.Reason, preferNewCategory: true);
        }

        foreach (var candidate in rankedCandidates)
        {
            TryAdd(candidate.Dish.DishId, candidate.Reason, preferNewCategory: false);
        }

        return selected;
    }

    private static CustomerDishRecommendationDto ToRecommendationDto(RankedRecommendationCandidate candidate, string reason)
        => new(
            candidate.Dish.DishId,
            candidate.Dish.Name,
            candidate.Dish.Description,
            candidate.Dish.Price,
            candidate.Dish.Image,
            string.IsNullOrWhiteSpace(candidate.Dish.Unit) ? null : candidate.Dish.Unit,
            candidate.Dish.IsVegetarian,
            candidate.Dish.IsDailySpecial,
            candidate.Dish.Available,
            candidate.Dish.Ingredients,
            candidate.Dish.CategoryId,
            candidate.Dish.CategoryName,
            TrimReasonForUi(reason));

    private static string TrimReasonForUi(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "Phù hợp để thưởng thức hôm nay";
        }

        return trimmed.Length <= 90
            ? trimmed
            : trimmed[..87].TrimEnd() + "...";
    }

    private static string? TrimForPrompt(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static (string TimeSlot, string Label, int Hour) BuildTimeContext(DateTime now)
    {
        var hour = now.Hour;
        if (hour < 11) return ("morning", "Buổi sáng", hour);
        if (hour < 15) return ("lunch", "Buổi trưa", hour);
        if (hour < 18) return ("afternoon", "Buổi chiều", hour);
        return ("dinner", "Buổi tối", hour);
    }

    private static string ResolveTimeBucketStart(DateTime now, int bucketMinutes)
    {
        var minuteBucket = now.Minute / bucketMinutes * bucketMinutes;
        var bucket = new DateTime(now.Year, now.Month, now.Day, now.Hour, minuteBucket, 0, now.Kind);
        return bucket.ToString("yyyy-MM-ddTHH:mm");
    }

    private sealed record RecommendationCandidate(
        int DishId,
        string Name,
        string? Description,
        string? Image,
        int CategoryId,
        string CategoryName,
        string Unit,
        bool IsVegetarian,
        bool IsDailySpecial,
        bool IsTopSeller,
        bool Available,
        decimal Price,
        IReadOnlyList<MenuDishIngredientDto>? Ingredients);

    private sealed record RankedRecommendationCandidate(
        RecommendationCandidate Dish,
        int Score,
        string Reason);

    private sealed record RecommendationEnvelope(List<RecommendationItem> Recommendations);

    private sealed record RecommendationItem(int DishId, string Reason);

    private sealed record GeminiGenerateContentRequest(IReadOnlyList<GeminiContentRequest> Contents);

    private sealed record GeminiContentRequest(string Role, IReadOnlyList<GeminiPartRequest> Parts);

    private sealed record GeminiPartRequest(string Text);

    private sealed record GeminiGenerateContentResponse(IReadOnlyList<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(GeminiContentResponse? Content);

    private sealed record GeminiContentResponse(IReadOnlyList<GeminiPartResponse>? Parts);

    private sealed record GeminiPartResponse(string? Text);
}
