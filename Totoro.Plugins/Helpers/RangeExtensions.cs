namespace Totoro.Plugins.Helpers;

public static class RangeExtensions
{
    public static (int Start, int End) Extract(this Range range, int count)
    {
        if (range.Equals(Range.All))
        {
            return (1, count);
        }

        int start = range.Start.IsFromEnd ? count - range.Start.Value + 1 : range.Start.Value;

        if (range.Start.Equals(range.End))
        {
            return (start, start);
        }

        int end;

        if (range.End.IsFromEnd)
        {
            if (range.End.Value == 0)
            {
                end = count;
            }
            else
            {
                end = count - range.End.Value + 1;
            }
        }
        else
        {
            end = range.End.Value;
        }

        return (start, end);
    }
}
