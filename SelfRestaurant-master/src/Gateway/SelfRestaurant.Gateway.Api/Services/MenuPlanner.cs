namespace SelfRestaurant.Gateway.Api.Services;

/// <summary>
/// "Gợi ý món theo nhóm và ngân sách" (owner 06/11). Uses only dishes the admin tagged with a kind (MAIN, STARTER,
/// SOUP, SIDE, DESSERT, DRINK); vegetarian and spicy only when tagged. Steps: enough main-dish servings for everyone
/// (vegetarian mains first for the vegetarians), then a starter, a soup, drinks and dessert while the total stays in
/// the budget. Totals use the bill's formula (service fee and VAT of the branch, before any discount). Pure.
/// </summary>
public static class MenuPlanner
{
    public sealed record Dish(int DishId, string Name, decimal Price, string? Course, bool IsVegetarian, bool? IsSpicy, int? Servings);

    public sealed record Line(Dish Dish, int Quantity, string Reason, bool ForVegetarians);

    public sealed record Plan(IReadOnlyList<Line> Lines, decimal Subtotal, decimal Total, IReadOnlyList<string> Notes, bool Feasible,
        decimal? MinimumTotal = null);

    public const decimal MainShare = 0.6m;

    /// <summary>The bill's total before discounts: subtotal + round(fee%) + round(VAT% on subtotal+fee), half up.</summary>
    public static decimal Total(decimal subtotal, decimal feePercent, decimal vatPercent)
    {
        static decimal Charge(decimal amount, decimal percent) =>
            amount <= 0 || percent <= 0 ? 0 : Math.Round(amount * percent / 100m, 0, MidpointRounding.AwayFromZero);
        var fee = Charge(subtotal, feePercent);
        return subtotal + fee + Charge(subtotal + fee, vatPercent);
    }

    public static Plan Suggest(IReadOnlyList<Dish> menu, int people, decimal budget, int vegetarians, bool avoidSpicy,
        decimal feePercent, decimal vatPercent)
    {
        var notes = new List<string>();
        var dishes = menu.Where(x => x.Price > 0 && x.Course is not null && (!avoidSpicy || x.IsSpicy == false)).ToArray();
        var untagged = menu.Count(x => x.Course is null);
        if (untagged > 0) notes.Add($"{untagged} món chưa được gắn loại món nên chưa được gợi ý.");
        if (avoidSpicy) notes.Add("Chỉ gợi ý món đã được đánh dấu là không cay.");
        var mains = dishes.Where(x => x.Course == "MAIN").ToArray();
        if (mains.Length == 0)
            return new Plan(Array.Empty<Line>(), 0, 0, notes.Prepend("Chưa có món chính nào được gắn loại món để gợi ý.").ToList(), false);

        var lines = new List<Line>();
        var target = budget * MainShare / Math.Max(1, people); // what one person's main may cost
        // 1. Vegetarians first, from vegetarian mains only.
        var vegCovered = 0;
        if (vegetarians > 0)
        {
            var vegMains = mains.Where(x => x.IsVegetarian).ToArray();
            if (vegMains.Length == 0) notes.Add("Chưa có món chính nào được gắn là món chay.");
            else vegCovered = AddMains(lines, vegMains, vegetarians, target, forVegetarians: true);
        }
        // 2. Everyone else.
        var others = mains.Where(x => !x.IsVegetarian).ToArray();
        if (others.Length == 0) others = mains;
        AddMains(lines, others, Math.Max(0, people - vegCovered), target, forVegetarians: false);

        var subtotal = Sum(lines);
        if (Total(subtotal, feePercent, vatPercent) > budget)
        {
            // The cheapest plan that still feeds everyone, to say how much is needed.
            var cheapest = new List<Line>();
            if (vegetarians > 0 && mains.Any(x => x.IsVegetarian))
                vegCovered = AddMains(cheapest, mains.Where(x => x.IsVegetarian).ToArray(), vegetarians, 0, true);
            else vegCovered = 0;
            AddMains(cheapest, others, Math.Max(0, people - vegCovered), 0, false);
            var minimum = Total(Sum(cheapest), feePercent, vatPercent);
            notes.Insert(0, $"Ngân sách chưa đủ cho {people} người. Cần tối thiểu khoảng {Money(minimum)}.");
            return new Plan(cheapest, Sum(cheapest), minimum, notes, false, minimum);
        }

        // 3. Balance the table while the budget allows.
        void TryAdd(string course, int quantity, string reason)
        {
            var spare = budget - Total(Sum(lines), feePercent, vatPercent);
            if (spare <= 0 || quantity <= 0) return;
            var pick = dishes.Where(x => x.Course == course && lines.All(l => l.Dish.DishId != x.DishId))
                .Where(x => Total(Sum(lines) + x.Price * quantity, feePercent, vatPercent) <= budget)
                .OrderBy(x => Math.Abs(x.Price * quantity - spare / 3)).ThenBy(x => x.DishId).FirstOrDefault();
            if (pick is not null) lines.Add(new Line(pick, quantity, reason, false));
        }
        TryAdd("STARTER", (int)Math.Ceiling(people / 4.0), "Khai vị cho cả bàn");
        if (people >= 3) TryAdd("SOUP", 1, "Canh/súp dùng chung");
        TryAdd("DRINK", people, $"Đồ uống cho {people} người");
        TryAdd("DESSERT", (int)Math.Ceiling(people / 2.0), "Tráng miệng");

        subtotal = Sum(lines);
        return new Plan(lines, subtotal, Total(subtotal, feePercent, vatPercent), notes, true);
    }

    /// <summary>Other dishes of the same kind (vegetarian when the line is for vegetarians) that keep the plan in budget.</summary>
    public static IReadOnlyList<Dish> Alternatives(IReadOnlyList<Dish> menu, Plan plan, Line line, decimal budget, bool avoidSpicy,
        decimal feePercent, decimal vatPercent, int max = 3)
    {
        var rest = plan.Subtotal - line.Dish.Price * line.Quantity;
        return menu.Where(x => x.Course == line.Dish.Course && x.Price > 0 && x.DishId != line.Dish.DishId
                && plan.Lines.All(l => l.Dish.DishId != x.DishId)
                && (!line.ForVegetarians || x.IsVegetarian) && (!avoidSpicy || x.IsSpicy == false))
            .Where(x => Total(rest + x.Price * line.Quantity, feePercent, vatPercent) <= budget)
            .OrderBy(x => Math.Abs(x.Price - line.Dish.Price)).ThenBy(x => x.DishId).Take(max).ToArray();
    }

    /// <summary>Distinct mains (one serving each counts its "servings"), closest to the per-person target; repeats when the
    /// menu has fewer dishes than needed. Returns the servings covered.</summary>
    private static int AddMains(List<Line> lines, IReadOnlyList<Dish> pool, int people, decimal target, bool forVegetarians)
    {
        if (people <= 0 || pool.Count == 0) return 0;
        var covered = 0;
        var used = new HashSet<int>();
        while (covered < people)
        {
            var left = people - covered;
            // Cost per person actually fed: a hotpot for 4 bought for 2 people costs double per person.
            decimal PerPerson(Dish x) => x.Price / Math.Min(Math.Max(1, x.Servings ?? 1), left);
            var choices = pool.Where(x => !used.Contains(x.DishId)).ToArray();
            if (choices.Length == 0) choices = pool.ToArray(); // fewer dishes than needed: repeat
            var dish = choices
                .OrderBy(x => target <= 0 ? PerPerson(x) : Math.Abs(PerPerson(x) - target))
                .ThenBy(x => x.Price).ThenBy(x => x.DishId).First();
            used.Add(dish.DishId);
            var existing = lines.FindIndex(l => l.Dish.DishId == dish.DishId && l.ForVegetarians == forVegetarians);
            if (existing >= 0) lines[existing] = lines[existing] with { Quantity = lines[existing].Quantity + 1 };
            else lines.Add(new Line(dish, 1, forVegetarians ? "Món chay cho người ăn chay" : "Món chính", forVegetarians));
            covered += Math.Max(1, dish.Servings ?? 1);

        }
        for (var k = 0; k < lines.Count; k++)
        {
            var l = lines[k];
            if (l.Dish.Course != "MAIN" || l.ForVegetarians != forVegetarians) continue;
            var serves = Math.Max(1, l.Dish.Servings ?? 1) * l.Quantity;
            lines[k] = l with { Reason = (forVegetarians ? "Món chay" : "Món chính") + $" · đủ cho {serves} người" };
        }
        return covered;
    }

    private static decimal Sum(IEnumerable<Line> lines) => lines.Sum(x => x.Dish.Price * x.Quantity);

    public static string Money(decimal value) =>
        value.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.') + "đ";
}
