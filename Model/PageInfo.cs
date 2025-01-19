namespace Model;

public class PageInfo(int pageId, int dayNumber, int used)
{
    public int PageId {get;set;} = pageId;
    public int DayNumber {get;set;} = dayNumber;
    public int Used {get;set;} = used;
}