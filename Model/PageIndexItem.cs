namespace Model;

public class PageIndexItem
{
    public string PageTitle { get; set; } ="";

    public long ByteStart { get; set; }

    public int PageId { get; set; }

    public static char Separator { get; set; } = ':';

    public static PageIndexItem Parse(string text)
    {
        if(string.IsNullOrEmpty(text))
            return new PageIndexItem();
        var parts = text.Split([Separator], 3);
        return new PageIndexItem
        {
            ByteStart = long.Parse(parts[0]),
            PageId = int.Parse(parts[1]),
            PageTitle = parts[2]
        };
    }

    public override string ToString()
    {
        return ByteStart + Separator.ToString() + PageId + Separator + PageTitle;
    }
}