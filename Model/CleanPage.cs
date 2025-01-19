namespace Model;

public class CleanPage(int pageId, string text)
{
    public int PageId {get;set;} = pageId;
    public string Text {get;set;} = text;
}