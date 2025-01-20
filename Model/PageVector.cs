namespace Model;

public class PageVector(int pageId, byte[] blob)
{
    public int PageId {get;set;} = pageId;
    public byte[] Blob {get;set;} = blob;
}