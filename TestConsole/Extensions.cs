using System.Text;

public static class Extensions
{
public static int IndexOf(
    this StringBuilder sb,
    string value,
    int startIndex,
    bool ignoreCase)
{
    int len = value.Length;
    int max = (sb.Length - len) + 1;
    var v1 = (ignoreCase)
        ? value.ToLower() : value;
    var func1 = (ignoreCase)
        ? new Func<char, char, bool>((x, y) => char.ToLower(x) == y)
        : new Func<char, char, bool>((x, y) => x == y);
    for (int i1 = startIndex; i1 < max; ++i1)
        if (func1(sb[i1], v1[0]))
        {
            int i2 = 1;
            while ((i2 < len) && func1(sb[i1 + i2], v1[i2]))
                ++i2;
            if (i2 == len)
                return i1;
        }
    return -1;
}

}