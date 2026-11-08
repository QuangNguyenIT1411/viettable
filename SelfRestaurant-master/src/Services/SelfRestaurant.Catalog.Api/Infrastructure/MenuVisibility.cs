using System.Linq.Expressions;
using SelfRestaurant.Catalog.Api.Persistence.Entities;

namespace SelfRestaurant.Catalog.Api.Infrastructure;

/// <summary>
/// What "on the menu" means (owner 08/11), ONE definition for the app's menu API and the kitchen's pause/resume:
/// an active dish of an active category. Dishes belong to every branch; whether it can be ordered now is
/// <c>Dishes.Available</c>. The branch's dated menu rows are optional bookkeeping, never a condition.
/// </summary>
public static class MenuVisibility
{
    public static readonly Expression<Func<Dishes, bool>> OnMenu =
        d => (d.IsActive ?? true) && (d.Category.IsActive ?? true);

    private static readonly Func<Dishes, bool> OnMenuCompiled = OnMenu.Compile();

    /// <summary>The dish must have its <c>Category</c> loaded.</summary>
    public static bool IsOnMenu(Dishes dish) => OnMenuCompiled(dish);
}
