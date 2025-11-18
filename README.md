# EF.Paginator

A powerful and flexible dynamic pagination library for Entity Framework Core that uses Expression Trees to build queries dynamically.

## Features

- **Dynamic Filtering**: Complex filtering with support for multiple conditions, OR/AND logic
- **Flexible Sorting**: Multi-column sorting with ascending/descending order
- **Eager Loading Support**: Prevent N+1 queries with built-in Include support
- **Date Range Filtering**: Special syntax for date range queries
- **Async/Sync Methods**: Full support for both synchronous and asynchronous operations
- **Count by Filter**: Get counts without loading entities
- **Type-Safe**: Built with C# Expression Trees for compile-time safety
- **Validation**: Comprehensive input validation with meaningful error messages

## Installation

```bash
dotnet add package EF.Paginator
```

## Basic Usage

### Simple Pagination

```csharp
var pageable = new Pageable
{
    Page = 1,
    Size = 10,
    Sort = "createdAt desc",
    Filter = "title[test],status[ACTIVE]"
};

var result = dbContext.Products.ToPaged(pageable);

// Result contains:
// - Data: List of entities
// - Current: Current page number
// - Size: Page size
// - Elements: Total count of elements
// - Pages: Total number of pages
```

### Async Pagination

```csharp
var result = await dbContext.Products.ToPagedAsync(pageable);
```

### With Eager Loading (Prevent N+1)

```csharp
// Using params includes
var result = dbContext.Products.ToPaged(
    pageable,
    p => p.Category,
    p => p.Supplier
);

// Using fluent includes
var result = dbContext.Products.ToPaged(
    pageable,
    query => query
        .Include(p => p.Category)
        .ThenInclude(c => c.Parent)
        .Include(p => p.Supplier)
);
```

### Count by Filter

```csharp
// Synchronous
int count = dbContext.Products.CountByFilter(pageable);

// Asynchronous
int count = await dbContext.Products.CountByFilterAsync(pageable);
```

## Filtering Syntax

### Basic Filtering

The filter parameter uses the format: `property[value1,value2],property2[value]`

- Each property filter is separated by `,` (AND logic)
- Multiple values within `[]` are separated by `,` (OR logic)

**Examples:**

```csharp
// Single condition
Filter = "title[test]"
// SQL: WHERE title LIKE '%test%'

// Multiple values (OR)
Filter = "title[test1,test2]"
// SQL: WHERE (title LIKE '%test1%' OR title LIKE '%test2%')

// Multiple properties (AND)
Filter = "title[test],status[ACTIVE]"
// SQL: WHERE title LIKE '%test%' AND status = 'ACTIVE'

// Nested properties
Filter = "address.city[New York]"
// SQL: WHERE address.city LIKE '%New York%'
```

### Date Filtering

Date filters support special syntax for ranges:

```csharp
// Exact date
Filter = "createdAt[2022-09-21,2022-09-21]"
// SQL: WHERE createdAt >= '2022-09-21' AND createdAt <= '2022-09-21'

// Greater than or equal (use _ as placeholder)
Filter = "createdAt[2022-09-21,_]"
// SQL: WHERE createdAt >= '2022-09-21'

// Less than or equal
Filter = "createdAt[_,2022-09-21]"
// SQL: WHERE createdAt <= '2022-09-21'

// Date range
Filter = "createdAt[2022-09-16,2022-09-21]"
// SQL: WHERE createdAt >= '2022-09-16' AND createdAt <= '2022-09-21'
```

### Enum Filtering

```csharp
Filter = "status[PENDING,ACTIVE]"
// Automatically parses to enum values (case-insensitive)
```

### Collection Filtering

```csharp
Filter = "tags.name[important]"
// SQL: WHERE tags.Any(t => t.name LIKE '%important%')
```

## Sorting Syntax

The sort parameter format: `property1 direction,property2 direction`

- `asc` for ascending (default)
- `desc` for descending
- Multiple sorts separated by `,`

**Examples:**

```csharp
// Single column
Sort = "createdAt desc"

// Multiple columns
Sort = "status asc,createdAt desc"

// Nested properties
Sort = "category.name asc"
```

## Validation

The library includes comprehensive validation:

- **Page**: Must be >= 1
- **Size**: Must be between 1 and 1000
- **Filter Format**: Validates syntax and throws meaningful errors
- **Date Formats**: Validates date strings and formats
- **Property Names**: Validates that properties exist on the entity
- **Type Conversions**: Validates that filter values can be converted to property types

## Advanced Examples

### Complex Filtering

```csharp
var pageable = new Pageable
{
    Page = 1,
    Size = 20,
    Sort = "priority desc,createdAt desc",
    Filter = "title[urgent,important],status[ACTIVE,PENDING],createdAt[2024-01-01,_]"
};

var result = await dbContext.Tasks
    .ToPaged(
        pageable,
        query => query
            .Include(t => t.AssignedUser)
            .Include(t => t.Project)
    );
```

### Pagination with Projection

```csharp
var result = dbContext.Products.ToPaged(pageable);

var dtoPage = result.Convert(product => new ProductDto
{
    Id = product.Id,
    Name = product.Name,
    Price = product.Price
});
```

## Limitations

**Property Depth**: The library intentionally limits property navigation to a maximum depth of 2 levels to maintain performance and simplicity.

Examples:
- ✅ Supported: `name[test]` (1 level)
- ✅ Supported: `address.city[New York]` (2 levels)
- ✅ Supported: `tags.name[important]` (collection + 1 level - allowed exception)
- ❌ Not Supported: `address.city.country[USA]` (3 levels - exceeds limit)
- ❌ Not Supported: `customer.address.city[New York]` (3 levels - exceeds limit)

**Why this limitation?**
- Prevents overly complex queries that can impact performance
- Encourages proper database design with appropriate denormalization
- Keeps the query syntax simple and maintainable
- Collections are treated specially: after a collection property, one additional level is allowed

## Performance Considerations

1. **Use AsNoTracking**: The library automatically uses `AsNoTracking()` for read-only queries
2. **Eager Loading**: Always use the Include parameters to prevent N+1 queries
3. **Indexing**: Ensure database columns used in filters and sorts are properly indexed
4. **Page Size**: The maximum page size is limited to 1000 to prevent performance issues

## Building the Package

```bash
dotnet pack -c Release
```

The package will be created in `bin/Release/`.

## Publishing to NuGet

```bash
dotnet nuget push bin/Release/EF.Paginator.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Requirements

- .NET 10.0 or higher
- Entity Framework Core 9.0 or higher

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
