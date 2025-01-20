using Model;

namespace Storage
{
    public interface IWikiStorage
    {
        public void InitStore();

        public bool InsertTitles(List<Title> titles);
        public bool InsertPagesInfo(List<PageInfo> titles);
        public bool InsertCleanedPages(List<CleanPage> pages);
        public bool InsertPageVectors(List<PageVector> pageVectors);
        public List<PageInfo> GetAllPagesInfo();
        public List<int> GetAllUsedPageIds();
        public void TruncateCleanedPages();
        public void TruncatePageVectors();
        public void Vacuum();
        public string GetPageText(int pageId);
    }
}