using System.Text.RegularExpressions;

namespace Paginator;

public class Pageable
{
    private const int MinPage = 1;
    private const int MinSize = 1;
    private const int MaxSize = 1000;

    public record FilterItem(string Property, string[] Values);

    private int _page = 1;
    private int _size = 5;

    public int Page
    {
        get => _page;
        set
        {
            if (value < MinPage)
                throw new ArgumentException($"Page must be greater than or equal to {MinPage}.", nameof(Page));
            _page = value;
        }
    }

    public int Size
    {
        get => _size;
        set
        {
            if (value < MinSize)
                throw new ArgumentException($"Size must be greater than or equal to {MinSize}.", nameof(Size));
            if (value > MaxSize)
                throw new ArgumentException($"Size must be less than or equal to {MaxSize}.", nameof(Size));
            _size = value;
        }
    }

    public string? Sort { get; set; }
    public string? Filter { get; set; }

    public IEnumerable<FilterItem> UseFilters()
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return Array.Empty<FilterItem>();

        // aceita N níveis: Insurance.Order.Client.Cnpj
        const string pattern = @"(?<property>\w+(?:\.\w+)*)\[(?<values>[^\]]+)\]";

        var matches = Regex.Matches(Filter, pattern);

        if (matches.Count == 0)
            throw new ArgumentException($"Invalid filter format: '{Filter}'. Expected format: 'property[value]' or 'property[value1,value2]'.", nameof(Filter));

        FilterItem[] filters = new FilterItem[matches.Count];

        for (var i = 0; i < matches.Count; i++)
        {
            var property = matches[i].Groups["property"].Value;
            var valuesString = matches[i].Groups["values"].Value;

            if (string.IsNullOrWhiteSpace(property))
                throw new ArgumentException($"Property name cannot be empty in filter: '{Filter}'.", nameof(Filter));

            var values = valuesString.Split(",", StringSplitOptions.TrimEntries);
            filters[i] = new FilterItem(property, values);
        }

        return filters;
    }
}