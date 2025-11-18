using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Paginator;

public static class Paginator
{
    private static LambdaExpression BuildLambdaExpression(Type sourceType, ParameterExpression parameter, Expression field, Pageable.FilterItem filter)
    {
        if (filter.Values.Length == 0)
            throw new ArgumentException($"Filter values cannot be empty for property '{filter.Property}'.", nameof(filter));

        Expression? aggregate = null;

        if (field.Type == typeof(DateTime))
        {
            if (filter.Values.Length < 2)
                throw new ArgumentException($"DateTime filter requires at least 2 values (use '_' as placeholder). Property: '{filter.Property}'.", nameof(filter));

            if (!string.IsNullOrWhiteSpace(filter.Values[0]) && filter.Values[0] != "_")
            {
                if (!DateTime.TryParse(filter.Values[0], out DateTime minDate))
                    throw new ArgumentException($"Invalid DateTime format for property '{filter.Property}': '{filter.Values[0]}'.", nameof(filter));

                ConstantExpression minor = Expression.Constant(minDate, field.Type);
                BinaryExpression bMinor = Expression.GreaterThanOrEqual(field, minor);

                aggregate = bMinor;
            }

            if (!string.IsNullOrWhiteSpace(filter.Values[1]) && filter.Values[1] != "_")
            {
                if (!DateTime.TryParse(filter.Values[1], out DateTime maxDate))
                    throw new ArgumentException($"Invalid DateTime format for property '{filter.Property}': '{filter.Values[1]}'.", nameof(filter));

                ConstantExpression major = Expression.Constant(maxDate, field.Type);
                BinaryExpression bMajor = Expression.LessThanOrEqual(field, major);

                aggregate = aggregate is null ? bMajor : Expression.AndAlso(aggregate, bMajor);
            }

            if (aggregate is null)
                throw new ArgumentException($"DateTime filter must have at least one valid date value for property '{filter.Property}'.", nameof(filter));
        }
        else
        {
            foreach (var value in filter.Values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (field.Type == typeof(string))
                {
                    ConstantExpression constant = Expression.Constant($"%{value}%");

                    MethodInfo method = typeof(DbFunctionsExtensions).GetMethod("Like", new[] { typeof(DbFunctions), typeof(string), typeof(string) })
                        ?? throw new InvalidOperationException("EF.Functions.Like method not found.");

                    var callExpression = Expression.Call(method, Expression.Constant(EF.Functions), field, constant);

                    aggregate = aggregate == null ? callExpression : Expression.Or(aggregate, callExpression);
                }
                else
                {
                    try
                    {
                        object convertedValue = field.Type.IsEnum
                            ? Enum.Parse(field.Type, value, ignoreCase: true)
                            : Convert.ChangeType(value, field.Type);

                        ConstantExpression constant = Expression.Constant(convertedValue);
                        var equalExpression = Expression.Equal(field, constant);
                        aggregate = aggregate == null ? equalExpression : Expression.Or(aggregate, equalExpression);
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException)
                    {
                        throw new ArgumentException($"Cannot convert value '{value}' to type '{field.Type.Name}' for property '{filter.Property}'.", nameof(filter), ex);
                    }
                }
            }

            if (aggregate is null)
                throw new ArgumentException($"Filter must have at least one valid value for property '{filter.Property}'.", nameof(filter));
        }

        Type lambdaType = typeof(Func<,>).MakeGenericType(sourceType, typeof(bool));

        return Expression.Lambda(lambdaType, aggregate, parameter);
    }

    public static Expression AddWhereExpression(Expression source, IEnumerable<Pageable.FilterItem> filters)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (filters == null)
            throw new ArgumentNullException(nameof(filters));

        Type sourceType = source.Type.GetGenericArguments().FirstOrDefault()
            ?? throw new ArgumentException("Source expression must be a generic queryable type.", nameof(source));

        ParameterExpression parameter = Expression.Parameter(sourceType, "entity");

        var expressions = new List<LambdaExpression>();

        foreach (var filter in filters)
        {
            if (filter == null)
                throw new ArgumentException("Filter item cannot be null.", nameof(filters));

            MemberExpression field = BuildPropertyExpression(parameter, filter.Property, out var cleanPath);

            if (typeof(IEnumerable).IsAssignableFrom(field.Type) && field.Type != typeof(string))
            {
                Type fieldType = field.Type.GetGenericArguments().FirstOrDefault()
                    ?? throw new ArgumentException($"Collection property '{filter.Property}' must be a generic type.", nameof(filter));

                ParameterExpression inner = Expression.Parameter(fieldType, "inner");

                Expression prop = inner.Type == typeof(string) || inner.Type.IsPrimitive ? inner : Expression.Property(inner, cleanPath);
                var innerLambda = BuildLambdaExpression(fieldType, inner, prop, filter);

                MethodInfo anyMethod = typeof(Enumerable).GetMethods()
                    .Single(m => m.Name == "Any" && m.GetParameters().Length == 2)
                    .MakeGenericMethod(fieldType);

                Type wrapperLambda = typeof(Func<,>).MakeGenericType(sourceType, typeof(bool));
                expressions.Add(Expression.Lambda(wrapperLambda, Expression.Call(anyMethod, field, innerLambda), parameter));
            }
            else
            {
                expressions.Add(BuildLambdaExpression(sourceType, parameter, field, filter));
            }
        }

        if (expressions.Count == 0)
            return source;

        LambdaExpression final = CombineLambdasWithAnd(expressions);

        return Expression.Call(typeof(Queryable), "Where", new[] { sourceType }, source, final);
    }

    private static LambdaExpression CombineLambdasWithAnd(IReadOnlyList<LambdaExpression> lambdas)
    {
        if (lambdas == null || lambdas.Count == 0)
            throw new ArgumentException("Lambda expressions list cannot be null or empty.", nameof(lambdas));

        ParameterExpression parameter = lambdas[0].Parameters[0];

        Expression body = lambdas[0].Body;
        for (int i = 1; i < lambdas.Count; i++)
        {
            body = Expression.AndAlso(body, lambdas[i].Body);
        }

        return Expression.Lambda(body, parameter);
    }

    public static Expression AddOrderExpression(Expression source, string orderBy)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (string.IsNullOrWhiteSpace(orderBy))
            throw new ArgumentException("Order by string cannot be null or empty.", nameof(orderBy));

        string[] orders = orderBy.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (orders.Length == 0)
            throw new ArgumentException("Order by string must contain at least one valid property.", nameof(orderBy));

        for (int i = 0; i < orders.Length; i++)
        {
            source = BuildOrderExpression(source, orders[i], i);
        }

        return source;
    }

    private static Expression BuildOrderExpression(Expression source, string orderBy, int index)
    {
        string[] orderByParams = orderBy.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        string orderByMethodName = index == 0 ? "OrderBy" : "ThenBy";

        string parameterPath = orderByParams[0];

        if (orderByParams.Length > 1 && orderByParams[1].Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            orderByMethodName += "Descending";
        }

        Type sourceType = source.Type.GetGenericArguments().First();

        ParameterExpression parameterExpression = Expression.Parameter(sourceType, "entity");

        Expression orderByExpression = BuildPropertyExpression(parameterExpression, parameterPath, out _);

        Type orderByFuncType = typeof(Func<,>).MakeGenericType(sourceType, orderByExpression.Type);

        LambdaExpression orderByLambda = Expression.Lambda(orderByFuncType, orderByExpression, parameterExpression);

        return Expression.Call(typeof(Queryable), orderByMethodName, new[] { sourceType, orderByExpression.Type }, source, orderByLambda);
    }

    private static MemberExpression BuildPropertyExpression(Expression source, string path, out string cleanPath)
    {
        Expression field = source;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
            throw new ArgumentException($"Invalid path '{path}'");

        // Limitação proposital: suporta apenas até o segundo nível de profundidade
        // Exceção: se houver uma coleção no meio, permite um nível adicional após a coleção
        if (segments.Length > 2)
        {
            // Verifica se há uma coleção no caminho que justifique mais de 2 níveis
            bool hasCollection = false;
            Expression tempField = source;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                var prop = tempField.Type.GetProperty(segments[i],
                    BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);

                if (prop != null)
                {
                    var propType = prop.PropertyType;
                    if (typeof(IEnumerable).IsAssignableFrom(propType) && propType != typeof(string))
                    {
                        hasCollection = true;
                        break;
                    }
                    tempField = Expression.Property(tempField, prop);
                }
            }

            if (!hasCollection)
                throw new ArgumentException(
                    $"Property path '{path}' exceeds maximum depth of 2 levels. " +
                    $"Example: 'address.city' is valid, but 'address.city.country' is not.");
        }

        cleanPath = segments[^1];

        foreach (var s in segments)
        {
            var property = field.Type.GetProperty(s,
                BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public)
                ?? throw new KeyNotFoundException(
                    $"Cannot find property '{s}' on type '{field.Type.Name}' for path '{path}'");

            field = Expression.Property(field, property);

            if (typeof(IEnumerable).IsAssignableFrom(field.Type) && field.Type != typeof(string))
                break;
        }

        return (MemberExpression)field;
    }
}