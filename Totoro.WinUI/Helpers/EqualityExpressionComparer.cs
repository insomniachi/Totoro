using System.Diagnostics.CodeAnalysis;

namespace AnimDL.WinUI.Helpers;

internal class EqualityExpressionComparer<T> : IEqualityComparer<T>
    where T : notnull
{
    private readonly Func<T, int> _getHashCode;
    private readonly Func<T, T, bool> _compare;

    private EqualityExpressionComparer(Func<T, T, bool> compare, Func<T, int> getHashCode)
    {
        _compare = compare;
        _getHashCode = getHashCode;
    }

    public static EqualityExpressionComparer<T> Create(Func<T, T, bool> areEqual, Func<T, int> getHashCode = null)
    {
        return new EqualityExpressionComparer<T>(areEqual, getHashCode);
    }

    public bool Equals(T x, T y) => _compare(x, y);

    public int GetHashCode([DisallowNull] T obj)
    {
        if (_getHashCode is null)
        {
            return EqualityComparer<T>.Default.GetHashCode(obj);
        }

        return _getHashCode(obj);
    }
}
