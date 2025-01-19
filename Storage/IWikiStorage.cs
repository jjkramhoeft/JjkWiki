using Model;

namespace Storage
{
    public interface IWikiStorage
    {
        public void InitStore();

        public bool InsertTitles(List<Title> titles);
        public bool InsertPagesInfo(List<PageInfo> titles);
        public bool InsertCleanedPages(List<CleanPage> pages);
        public List<PageInfo> GetAllPagesInfo();
        public void TruncateCleanedPages();
        public void Vacuum();
    }
}