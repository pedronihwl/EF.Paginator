namespace Paginator;

public class Page<Entity> where Entity : class
{
    public IEnumerable<Entity> Data { get; init; }
    
    public int Pages { get; set; }
    public int Elements { get; set; }
    public int Current { get; set; }
    public int Size { get; set; }

    public Page(IEnumerable<Entity> data)
    {
        Data = data;
    }
    
    public Page<R> Convert<R>(Func<Entity, R> converter) 
        where R : class
    {
        return new Page<R>(Data.Select(converter.Invoke))
        {
            Pages = this.Pages,
            Size = this.Size,
            Elements = this.Elements,
            Current = this.Current
        };
    }
}