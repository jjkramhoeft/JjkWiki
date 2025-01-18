using Model;
using Microsoft.Data.Sqlite;

namespace Storage
{
    public class LocalDb(string dbName) : IWikiStorage
    {
        private readonly string _dbFullName = $"E:\\JjkWikiDb\\{dbName}.db";

        public void InitStore()
        {
            throw new NotImplementedException();
        }

        public bool InsertTitle(Title title)
        {
            throw new NotImplementedException();
        }
    }
}