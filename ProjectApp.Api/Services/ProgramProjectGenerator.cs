using Bogus;
using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Services;

public class ProgramProjectGenerator
{
    private readonly Faker<ProgramProject> _faker;

    public ProgramProjectGenerator()
    {
        _faker = new Faker<ProgramProject>("ru")
            .RuleFor(p => p.Id, f => f.IndexFaker + 1)
            .RuleFor(p => p.ProjectName, f =>
                $"{f.Commerce.ProductName()} {f.Hacker.Noun()} {f.Finance.AccountName()} {f.Lorem.Word()}")
            .RuleFor(p => p.Customer, f =>
                f.Company.CompanyName())
            .RuleFor(p => p.ProjectManager, f =>
                $"{f.Name.LastName()} {f.Name.FirstName()}")
            .RuleFor(p => p.StartDate,
                f => f.Date.PastDateOnly(3))
            .RuleFor(p => p.PlannedEndDate,
                (f, p) => p.StartDate.AddDays(f.Random.Int(30, 730)))
            .RuleFor(p => p.Budget,
                f => Math.Round(f.Finance.Amount(500000, 50000000), 2))
            .RuleFor(p => p.ActualEndDate, (f, p) =>
            {
                var completed = f.Random.Bool(0.4f);

                if (!completed)
                    return null;

                var start = p.StartDate.ToDateTime(TimeOnly.MinValue);
                var end = f.Date.Between(start.AddDays(1), DateTime.Now);

                return DateOnly.FromDateTime(end);
            })
            .RuleFor(p => p.CompletionPercentage, (f, p) =>
            {
                if (p.ActualEndDate != null)
                    return 100;

                return f.Random.Int(0, 99);
            })
            .RuleFor(p => p.ActualCost, (f, p) =>
            {
                var minFactor = Math.Max(0.1m, p.CompletionPercentage / 100m * 0.8m);
                var maxFactor = Math.Min(1.2m, p.CompletionPercentage / 100m * 1.2m);

                var factor = f.Random.Decimal(minFactor, maxFactor);

                return Math.Round(p.Budget * factor, 2);
            });
    }

    public ProgramProject Generate()
    {
        return _faker.Generate();
    }
}