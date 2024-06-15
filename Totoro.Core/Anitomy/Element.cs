using System.Diagnostics;

namespace Anitomy;

[DebuggerDisplay("{Category} = {Value}")]
public class Element(ElementCategory category, string value)
{
    public ElementCategory Category { get; set; } = category;
    public string Value { get; } = value;

    public override int GetHashCode()
    {
      return -1926371015 + Value.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (this == obj)
        {
        return true;
        }

        if (obj == null || GetType() != obj.GetType())
        {
        return false;
        }

        var other = (Element) obj;
        return Category.Equals(other.Category);
    }
}
