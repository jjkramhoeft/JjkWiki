using Model;

namespace Storage
{
    public interface IWikiStorage
    {
        public void InitStore();

        public bool InsertTitle(Title title);
    }
}