using Bogus;
using HelloID.Vault.Services.Anonymization.Models;
using System.Collections;
using System.Diagnostics;

namespace HelloID.Vault.Services.Anonymization.Utilities;

/// <summary>
/// Manages pools of unique names to prevent duplicates.
/// Extends pools automatically when needed.
/// </summary>
public class NamePool
{
    private readonly MultiLocaleFaker _multiLocaleFaker;
    private readonly Faker _primaryFaker;
    private readonly Dictionary<string, int> _lastNameCounts;
    private readonly HashSet<string> _usedFullNames;
    private readonly List<string> _availableFirstNames;
    private readonly List<string> _availableLastNames;
    private readonly List<string> _uniqueLastNames;
    private readonly Random _random;
    private readonly NameSharingMode _nameSharingMode;
    private readonly int _initialPoolSize;
    private int _regenerationAttempts;
    
    private string? _currentGroupLastName;
    private int _currentGroupCount;
    private int _groupSize;
    private int _groupsCreated;
    private readonly int _maxGroups = 2;
    
    private int _totalNamesGenerated;
    private int _poolExtensions;
    private int _skippedNames;

    public NamePool(
        MultiLocaleFaker multiLocaleFaker,
        NameSharingMode nameSharingMode = NameSharingMode.Unique,
        int initialPoolSize = 2000) // Increased for European pool diversity
    {
        _multiLocaleFaker = multiLocaleFaker ?? throw new ArgumentNullException(nameof(multiLocaleFaker));
        _primaryFaker = multiLocaleFaker.Primary;
        _nameSharingMode = nameSharingMode;
        _initialPoolSize = initialPoolSize;
        _random = new Random();
        _regenerationAttempts = 0;
        _lastNameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _usedFullNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _availableFirstNames = new List<string>();
        _availableLastNames = new List<string>();
        _uniqueLastNames = new List<string>();
        _currentGroupLastName = null;
        _currentGroupCount = 0;
        _groupSize = GetGroupSize(nameSharingMode);

        InitializePools();
    }

    private static int GetGroupSize(NameSharingMode mode)
    {
        return mode switch
        {
            NameSharingMode.Pairs => 2,
            NameSharingMode.Triples => 3,
            NameSharingMode.Quadruples => 4,
            _ => 1
        };
    }

    private static int GetMaxDuplicates(NameSharingMode mode)
    {
        return mode switch
        {
            NameSharingMode.Unlimited => 0,
            NameSharingMode.Unique => 1,
            NameSharingMode.Max2 => 2,
            NameSharingMode.Max3 => 3,
            NameSharingMode.Max4 => 4,
            NameSharingMode.Pairs => 2,
            NameSharingMode.Triples => 3,
            NameSharingMode.Quadruples => 4,
            _ => 1
        };
    }

    private void InitializePools()
    {
        GenerateNames(_initialPoolSize);
        RegenerateUniqueNames();
    }

    private void RegenerateUniqueNames()
    {
        // Pre-generate unique last names to avoid expensive retry loops
        // Generate 5x the pool size to ensure we have enough unique names
        var candidateCount = _initialPoolSize * 5;
        var candidates = new List<string>(candidateCount);
        var uniqueSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Generate names until we have enough unique ones or hit max attempts
        int attempts = 0;
        int maxAttempts = candidateCount * 10; // Prevent infinite loop

        while (uniqueSet.Count < _initialPoolSize && attempts < maxAttempts)
        {
            var faker = _multiLocaleFaker.GetFakerForPerson();
            var lastName = faker.Name.LastName();

            // Only add if not already at max duplicates
            var currentCount = _lastNameCounts.GetValueOrDefault(lastName, 0);
            var maxDuplicates = GetMaxDuplicates(_nameSharingMode);

            if (currentCount < maxDuplicates && uniqueSet.Add(lastName))
            {
                // Found a new unique name that isn't at max duplicates
            }

            attempts++;
        }

        // Use the unique names we found
        _uniqueLastNames.Clear();
        _uniqueLastNames.AddRange(uniqueSet);

        Debug.WriteLine($"[NamePool] Pre-generated {_uniqueLastNames.Count} unique last names from {attempts} attempts");
    }

    private void GenerateNames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var faker = _multiLocaleFaker.GetFakerForPerson();
            _availableFirstNames.Add(faker.Name.FirstName());
            _availableLastNames.Add(faker.Name.LastName());
        }
    }

    /// <summary>
    /// Gets a unique first and last name combination.
    /// </summary>
    public (string GivenName, string FamilyName) GetUniqueName()
    {
        int maxAttempts = 100;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            attempts++;

            if (_availableFirstNames.Count == 0)
            {
                ExtendPool(500);
            }

            var firstName = GetRandomFirstName();

            string lastName;
            if (_nameSharingMode == NameSharingMode.Unique)
            {
                lastName = GetUniqueLastName();
                if (string.IsNullOrEmpty(lastName))
                {
                    ExtendPool(500);
                    continue;
                }
            }
            else if (_nameSharingMode == NameSharingMode.Unlimited)
            {
                lastName = GetRandomLastName();
            }
            else if (_nameSharingMode is NameSharingMode.Pairs or NameSharingMode.Triples or NameSharingMode.Quadruples)
            {
                if (_groupsCreated < _maxGroups)
                {
                    lastName = GetGroupedLastName();
                }
                else
                {
                    lastName = GetUniqueLastName();
                    if (string.IsNullOrEmpty(lastName))
                    {
                        ExtendPool(500);
                        continue;
                    }
                }
            }
            else
            {
                lastName = GetRandomLastNameWithMaxDuplicates();
            }

            _totalNamesGenerated++;
            return (firstName, lastName);
        }

        ExtendPool(1000);
        _regenerationAttempts++;

        var fallbackFirst = GetRandomFirstName();
        var fallbackLast = GetRandomLastName();

        _totalNamesGenerated++;
        return (fallbackFirst, fallbackLast);
    }

    private string? GetGroupedLastName()
    {
        if (_currentGroupLastName != null && _currentGroupCount < _groupSize)
        {
            _currentGroupCount++;
            Debug.WriteLine($"[NamePool] Group assign: '{_currentGroupLastName}' to person (count: {_currentGroupCount}/{_groupSize})");
            return _currentGroupLastName;
        }

        if (_groupsCreated >= _maxGroups)
        {
            return null;
        }

        var lastName = GetUniqueLastName();
        if (string.IsNullOrEmpty(lastName))
        {
            ExtendPool(500);
            lastName = GetUniqueLastName();
        }

        if (string.IsNullOrEmpty(lastName))
        {
            return null;
        }

        _groupsCreated++;
        Debug.WriteLine($"[NamePool] New group started ({_groupsCreated}/{_maxGroups}): '{lastName}' in group of {_groupSize}");
        _currentGroupLastName = lastName;
        _currentGroupCount = 1;
        return lastName;
    }

    /// <summary>
    /// Gets a unique partner last name (different from the person's own last name).
    /// </summary>
    public string GetUniquePartnerLastName(string excludeLastName)
    {
        int maxAttempts = 50;
        int attempts = 0;
        var maxDuplicates = GetMaxDuplicates(_nameSharingMode);

        while (attempts < maxAttempts && _availableLastNames.Count > 0)
        {
            attempts++;

            if (_availableLastNames.Count == 0)
            {
                ExtendPool(500);
            }

            var index = _random.Next(_availableLastNames.Count);
            var lastName = _availableLastNames[index];

            if (string.Equals(lastName, excludeLastName, StringComparison.OrdinalIgnoreCase))
            {
                // O(1) removal: swap with last element and remove last
                _availableLastNames[index] = _availableLastNames[^1];
                _availableLastNames.RemoveAt(_availableLastNames.Count - 1);
                continue;
            }

            var currentCount = _lastNameCounts.GetValueOrDefault(lastName, 0);
            if (maxDuplicates == 0 || currentCount < maxDuplicates)
            {
                _lastNameCounts[lastName] = currentCount + 1;
                // O(1) removal: swap with last element and remove last
                _availableLastNames[index] = _availableLastNames[^1];
                _availableLastNames.RemoveAt(_availableLastNames.Count - 1);
                return lastName;
            }

            // O(1) removal: swap with last element and remove last
            _availableLastNames[index] = _availableLastNames[^1];
            _availableLastNames.RemoveAt(_availableLastNames.Count - 1);
        }

        ExtendPool(100);
        var fallback = GetRandomLastName();
        return fallback;
    }

    private string GetRandomFirstName()
    {
        if (_availableFirstNames.Count == 0)
        {
            ExtendPool(500);
        }

        var index = _random.Next(_availableFirstNames.Count);
        var firstName = _availableFirstNames[index];
        _availableFirstNames.RemoveAt(index);
        return firstName;
    }

    private string GetRandomLastName()
    {
        if (_availableLastNames.Count == 0)
        {
            ExtendPool(500);
        }

        var index = _random.Next(_availableLastNames.Count);
        var lastName = _availableLastNames[index];
        _availableLastNames.RemoveAt(index);
        return lastName;
    }

    private string GetRandomLastNameWithMaxDuplicates()
    {
        var maxDuplicates = GetMaxDuplicates(_nameSharingMode);
        int attempts = 0;
        const int maxAttempts = 1000; // Prevent infinite loop

        while (attempts < maxAttempts)
        {
            if (_availableLastNames.Count == 0)
            {
                ExtendPool(500);
            }

            var index = _random.Next(_availableLastNames.Count);
            var lastName = _availableLastNames[index];

            var currentCount = _lastNameCounts.GetValueOrDefault(lastName, 0);
            if (maxDuplicates == 0 || currentCount < maxDuplicates)
            {
                _lastNameCounts[lastName] = currentCount + 1;
                // O(1) removal: swap with last element and remove last
                _availableLastNames[index] = _availableLastNames[^1];
                _availableLastNames.RemoveAt(_availableLastNames.Count - 1);
                Debug.WriteLine($"[NamePool] MaxDuplicates assign: '{lastName}' (count: {currentCount + 1}/{maxDuplicates})");
                return lastName;
            }

            Debug.WriteLine($"[NamePool] MaxDuplicates skip: '{lastName}' at max ({currentCount}/{maxDuplicates})");
            _skippedNames++;
            // O(1) removal: swap with last element and remove last
            _availableLastNames[index] = _availableLastNames[^1];
            _availableLastNames.RemoveAt(_availableLastNames.Count - 1);
            attempts++;
        }

        // Fallback: generate from European pool directly
        Debug.WriteLine("[NamePool] MaxDuplicates - pool exhausted, using direct generation");
        var faker = _multiLocaleFaker.GetFakerForPerson();
        var fallbackName = faker.Name.LastName();
        _lastNameCounts[fallbackName] = _lastNameCounts.GetValueOrDefault(fallbackName, 0) + 1;
        return fallbackName;
    }

    private string GetUniqueLastName()
    {
        var maxDuplicates = GetMaxDuplicates(_nameSharingMode);
        int regenerationCount = 0;
        const int maxRegenerations = 3; // Prevent infinite loop

        while (regenerationCount < maxRegenerations)
        {
            // Use pre-generated unique names list - O(1) selection by swapping
            while (_uniqueLastNames.Count > 0)
            {
                var index = _random.Next(_uniqueLastNames.Count);
                var lastName = _uniqueLastNames[index];

                var currentCount = _lastNameCounts.GetValueOrDefault(lastName, 0);
                if (currentCount < maxDuplicates)
                {
                    _lastNameCounts[lastName] = currentCount + 1;

                    // O(1) removal: swap with last element and remove last
                    _uniqueLastNames[index] = _uniqueLastNames[^1];
                    _uniqueLastNames.RemoveAt(_uniqueLastNames.Count - 1);

                    return lastName;
                }

                // Skip if at max duplicates - O(1) removal by swapping
                _uniqueLastNames[index] = _uniqueLastNames[^1];
                _uniqueLastNames.RemoveAt(_uniqueLastNames.Count - 1);
            }

            // If we get here, all names were at max duplicates - regenerate
            regenerationCount++;

            // Only regenerate if we haven't exceeded max regenerations
            if (regenerationCount < maxRegenerations)
            {
                RegenerateUniqueNames();
            }
        }

        // Fallback: directly generate a name without going through the pool
        // This ensures we always return something even if we're out of "unique" names
        Debug.WriteLine("[NamePool] GetUniqueLastName - pool exhausted, using fallback generation");
        var faker = _multiLocaleFaker.GetFakerForPerson();

        // Generate until we find a name within the actual limit
        int fallbackAttempts = 0;
        while (fallbackAttempts < 500)
        {
            var lastName = faker.Name.LastName();
            var currentCount = _lastNameCounts.GetValueOrDefault(lastName, 0);

            if (currentCount < maxDuplicates)  // Use actual limit, not 100x
            {
                _lastNameCounts[lastName] = currentCount + 1;
                return lastName;
            }

            fallbackAttempts++;
        }

        // Last resort: generate a truly unique name with GUID
        Debug.WriteLine("[NamePool] GetUniqueLastName - fallback exhausted, using GUID-based name");
        return $"Name-{Guid.NewGuid().ToString("N")[..8]}";
    }

    private void ExtendPool(int count)
    {
        _poolExtensions++;
        GenerateNames(count);
    }

    public void LogSummary()
    {
        var uniqueLastNames = _lastNameCounts.Count;
        var avgUsage = uniqueLastNames > 0 ? (double)_totalNamesGenerated / uniqueLastNames : 0;
        var topLastNames = _lastNameCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => $"{kvp.Key}({kvp.Value})");

        Debug.WriteLine($"[NamePool] === Summary ===");
        Debug.WriteLine($"[NamePool] Name sharing mode: {_nameSharingMode}");
        Debug.WriteLine($"[NamePool] Total names generated: {_totalNamesGenerated}");
        Debug.WriteLine($"[NamePool] Pool extensions: {_poolExtensions}");
        Debug.WriteLine($"[NamePool] Skipped names (at max): {_skippedNames}");
        Debug.WriteLine($"[NamePool] Unique last names: {uniqueLastNames}");
        Debug.WriteLine($"[NamePool] Avg last name usage: {avgUsage:F1}");
        Debug.WriteLine($"[NamePool] Top last names: {string.Join(", ", topLastNames)}");
    }

    public NamePoolStatistics GetStatistics()
    {
        return new NamePoolStatistics
        {
            AvailableFirstNames = _availableFirstNames.Count,
            AvailableLastNames = _availableLastNames.Count,
            UsedLastNames = _lastNameCounts.Count,
            UsedFullNames = _usedFullNames.Count,
            RegenerationAttempts = _regenerationAttempts
        };
    }
}

public class NamePoolStatistics
{
    public int AvailableFirstNames { get; set; }
    public int AvailableLastNames { get; set; }
    public int UsedLastNames { get; set; }
    public int UsedFullNames { get; set; }
    public int RegenerationAttempts { get; set; }
}
