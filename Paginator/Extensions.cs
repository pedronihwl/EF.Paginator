using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Paginator;

public static class Extensions
{
    public static Page<T> ToPaged<T>(this IQueryable<T> source, Pageable pageable, params Expression<Func<T, object>>[]? includes) where T : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (pageable == null)
            throw new ArgumentNullException(nameof(pageable));

        source = ApplyIncludes(source, includes);
        source = ApplyFiltersAndSorting(source, pageable);

        int total = source.Count();

        var data = source
            .Skip((pageable.Page - 1) * pageable.Size)
            .Take(pageable.Size)
            .AsNoTracking()
            .ToList();

        Page<T> results = new(data)
        {
            Current = pageable.Page,
            Size = pageable.Size,
            Elements = total,
            Pages = (int)Math.Ceiling((double)total / pageable.Size)
        };

        return results;
    }

    public static Page<T> ToPaged<T>(this IQueryable<T> source, Pageable pageable, Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeFunc) where T : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (pageable == null)
            throw new ArgumentNullException(nameof(pageable));

        if (includeFunc != null)
            source = includeFunc(source);

        source = ApplyFiltersAndSorting(source, pageable);

        int total = source.Count();

        var data = source
            .Skip((pageable.Page - 1) * pageable.Size)
            .Take(pageable.Size)
            .AsNoTracking()
            .ToList();

        Page<T> results = new(data)
        {
            Current = pageable.Page,
            Size = pageable.Size,
            Elements = total,
            Pages = (int)Math.Ceiling((double)total / pageable.Size)
        };

        return results;
    }

    public static int CountByFilter<T>(this IQueryable<T> source, Pageable pageable) where T : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (pageable == null)
            throw new ArgumentNullException(nameof(pageable));

        if (string.IsNullOrWhiteSpace(pageable.Filter))
            return source.Count();

        Expression queryExpression = source.Expression;
        queryExpression = Paginator.AddWhereExpression(queryExpression, pageable.UseFilters());

        if (queryExpression.CanReduce)
        {
            queryExpression = queryExpression.Reduce();
        }

        source = source.Provider.CreateQuery<T>(queryExpression);

        return source.Count();
    }

    public static async Task<Page<T>> ToPagedAsync<T>(this IQueryable<T> source, Pageable pageable, params Expression<Func<T, object>>[]? includes) where T : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (pageable == null)
            throw new ArgumentNullException(nameof(pageable));

        source = ApplyIncludes(source, includes);
        source = ApplyFiltersAndSorting(source, pageable);

        int total = await source.CountAsync();

        var data = await source
            .Skip((pageable.Page - 1) * pageable.Size)
            .Take(pageable.Size)
            .AsNoTracking()
            .ToListAsync();

        Page<T> results = new(data)
        {
            Current = pageable.Page,
            Size = pageable.Size,
            Elements = total,
            Pages = (int)Math.Ceiling((double)total / pageable.Size)
        };

        return results;
    }

    public static async Task<Page<T>> ToPagedAsync<T>(this IQueryable<T> source, Pageable pageable, Func<IQueryable<T>, IIncludableQueryable<T, object>>? includeFunc, CancellationToken cancellationToken = default) where T : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (pageable == null)
            throw new ArgumentNullException(nameof(pageable));

        if (includeFunc != null)
            source = includeFunc(source);

        source = ApplyFiltersAndSorting(source, pageable);

        int total = await source.CountAsync(cancellationToken);

        var data = await source
            .Skip((pageable.Page - 1) * pageable.Size)
            .Take(pageable.Size)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Page<T> results = new(data)
        {
            Current = pageable.Page,
            Size = pageable.Size,
            Elements = total,
            Pages = (int)Math.Ceiling((double)total / pageable.Size)
        };

        return results;
    }

    public static async Task<int> CountByFilterAsync<T>(this IQueryable<T> source, Pageable pageable, CancellationToken cancellationToken = default) where T : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (pageable == null)
            throw new ArgumentNullException(nameof(pageable));

        if (string.IsNullOrWhiteSpace(pageable.Filter))
            return await source.CountAsync(cancellationToken);

        Expression queryExpression = source.Expression;
        queryExpression = Paginator.AddWhereExpression(queryExpression, pageable.UseFilters());

        if (queryExpression.CanReduce)
        {
            queryExpression = queryExpression.Reduce();
        }

        source = source.Provider.CreateQuery<T>(queryExpression);

        return await source.CountAsync(cancellationToken);
    }

    private static IQueryable<T> ApplyFiltersAndSorting<T>(IQueryable<T> source, Pageable pageable) where T : class
    {
        Expression queryExpression = source.Expression;

        if (!string.IsNullOrWhiteSpace(pageable.Filter))
        {
            queryExpression = Paginator.AddWhereExpression(queryExpression, pageable.UseFilters());
        }

        if (!string.IsNullOrWhiteSpace(pageable.Sort))
        {
            queryExpression = Paginator.AddOrderExpression(queryExpression, pageable.Sort);
        }

        if (queryExpression.CanReduce)
        {
            queryExpression = queryExpression.Reduce();
        }

        return source.Provider.CreateQuery<T>(queryExpression);
    }

    private static IQueryable<T> ApplyIncludes<T>(IQueryable<T> source, params Expression<Func<T, object>>[]? includes) where T : class
    {
        if (includes == null || includes.Length == 0)
            return source;

        foreach (var include in includes)
        {
            if (include != null)
                source = source.Include(include);
        }

        return source;
    }
}