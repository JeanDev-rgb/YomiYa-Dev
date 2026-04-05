namespace YomiYa.Source.Models;

public abstract class Filter<T>(string name, T state)
{
    public string Name { get; } = name;
    public T State { get; set; } = state;

    public class Header(string name) : Filter<object>(name, 0)
    {
    }

    public class Separator(string name = "") : Filter<object>(name, 0)
    {
    }

    public abstract class Select<V>(string name, V[] values, int state) : Filter<int>(name, state)
    {
        public V[] Values { get; } = values;
    }

    public abstract class Text(string name, string state = "") : Filter<string>(name, state)
    {
    }

    public abstract class CheckBox(string name, bool state = false) : Filter<bool>(name, state)
    {
    }

    public abstract class TriState(string name, int state = TriState.STATE_IGNORE) : Filter<int>(name, state)
    {
        private const int STATE_IGNORE = 0;
        private const int STATE_INCLUDE = 1;
        private const int STATE_EXCLUDE = 2;

        public bool IsIgnore()
        {
            return State == STATE_IGNORE;
        }

        public bool IsInclude()
        {
            return State == STATE_INCLUDE;
        }

        public bool IsExclude()
        {
            return State == STATE_EXCLUDE;
        }
    }

    public abstract class Group<V>(string name, List<V> state) : Filter<List<V>>(name, state)
    {
    }

    public abstract class Sort(string name, string[] values, Sort.Selection? state = null)
        : Filter<Sort.Selection?>(name, state)
    {
        public string[] Values { get; } = values;

        public record Selection(int Index, bool Ascending)
        {
        }
    }
}