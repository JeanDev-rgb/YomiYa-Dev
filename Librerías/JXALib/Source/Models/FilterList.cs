namespace YomiYa.Source.Models;

public class FilterList(IEnumerable<Filter<object>> filters)
    : List<Filter<object>>(filters ?? new List<Filter<object>>())
{
    public FilterList(params Filter<object>[] filters) : this(filters.AsEnumerable())
    {
    }
}