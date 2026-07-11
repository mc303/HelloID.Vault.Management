using Bogus;
using HelloID.Vault.Services.Anonymization.Models;

namespace HelloID.Vault.Services.Anonymization.Utilities;

public static class FakerFactory
{
    private static readonly Dictionary<string, string[]> ForeignLocaleMap = new()
    {
        { "eastern_eu", new[] { "pl", "ro", "cz", "sk", "hr" } },
        { "western_eu", new[] { "de", "fr", "it", "es", "pt_PT", "nl_BE", "en_GB" } }
    };

    // All European locales using Latin alphabet - combined pool for unique names
    private static readonly string[] AllEuropeanLocales = new[]
    {
        "nl",    // Dutch (primary)
        "de",    // German
        "en_GB", // British English
        "fr",    // French
        "it",    // Italian
        "es",    // Spanish
        "pt_PT", // Portuguese
        "nl_BE", // Belgian Dutch
        "pl",    // Polish
        "ro",    // Romanian
        "cz",    // Czech
        "sk",    // Slovak
        "hr"     // Croatian
    };

    public static Faker CreateFaker(AnonymizationLocale locale)
    {
        return locale switch
        {
            AnonymizationLocale.Dutch => new Faker("nl"),
            AnonymizationLocale.English => new Faker("en"),
            _ => new Faker("nl")
        };
    }

    public static MultiLocaleFaker CreateMultiLocaleFaker(
        AnonymizationLocale primaryLocale,
        int foreignPercentage,
        ForeignNameMix foreignMix)
    {
        var primaryFaker = CreateFaker(primaryLocale);
        var foreignFakers = GetForeignFakers(foreignMix);

        return new MultiLocaleFaker(primaryFaker, foreignFakers, foreignPercentage);
    }

    public static MultiLocaleFaker CreateEuropeanPoolFaker(AnonymizationLocale primaryLocale)
    {
        var primaryFaker = CreateFaker(primaryLocale);
        // Exclude primary locale from European list since it's added to the pool
        var primaryLocaleCode = primaryLocale == AnonymizationLocale.Dutch ? "nl" : "en";
        var europeanFakers = AllEuropeanLocales
            .Where(l => l != primaryLocaleCode)
            .Select(l => new Faker(l))
            .ToList();

        return new MultiLocaleFaker(primaryFaker, europeanFakers, useEuropeanPool: true);
    }

    private static List<Faker> GetForeignFakers(ForeignNameMix mix)
    {
        var fakers = new List<Faker>();

        if (mix.HasFlag(ForeignNameMix.EasternEuropean))
        {
            fakers.AddRange(ForeignLocaleMap["eastern_eu"].Select(l => new Faker(l)));
        }

        if (mix.HasFlag(ForeignNameMix.WesternEuropean))
        {
            fakers.AddRange(ForeignLocaleMap["western_eu"].Select(l => new Faker(l)));
        }

        return fakers;
    }
}

public class MultiLocaleFaker
{
    private readonly Faker _primaryFaker;
    private readonly List<Faker> _foreignFakers;
    private readonly int _foreignPercentage;
    private readonly bool _useEuropeanPool;
    private readonly Random _random;

    public MultiLocaleFaker(Faker primaryFaker, List<Faker> foreignFakers, int foreignPercentage)
        : this(primaryFaker, foreignFakers, foreignPercentage, useEuropeanPool: false)
    {
    }

    public MultiLocaleFaker(Faker primaryFaker, List<Faker> foreignFakers, bool useEuropeanPool)
        : this(primaryFaker, foreignFakers, foreignPercentage: 0, useEuropeanPool)
    {
    }

    private MultiLocaleFaker(Faker primaryFaker, List<Faker> foreignFakers, int foreignPercentage, bool useEuropeanPool)
    {
        _primaryFaker = primaryFaker ?? throw new ArgumentNullException(nameof(primaryFaker));
        _foreignFakers = foreignFakers ?? new List<Faker>();
        _foreignPercentage = Math.Clamp(foreignPercentage, 0, 100);
        _useEuropeanPool = useEuropeanPool;
        _random = new Random();
    }

    public Faker GetFakerForPerson()
    {
        // European pool mode: evenly distribute across all European locales
        if (_useEuropeanPool && _foreignFakers.Count > 0)
        {
            // Include primary faker in the pool for even distribution
            var allFakers = new List<Faker> { _primaryFaker };
            allFakers.AddRange(_foreignFakers);
            return allFakers[_random.Next(allFakers.Count)];
        }

        // Standard mode: use foreign percentage
        if (_foreignFakers.Count == 0 || _foreignPercentage == 0)
        {
            return _primaryFaker;
        }

        if (_random.Next(100) < _foreignPercentage)
        {
            return _foreignFakers[_random.Next(_foreignFakers.Count)];
        }

        return _primaryFaker;
    }

    public Faker Primary => _primaryFaker;
}
