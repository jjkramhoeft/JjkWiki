using Model;
using Microsoft.Data.Sqlite;

namespace Storage
{
    public class LocalDb(string dbName) : IWikiStorage
    {
        private readonly string _dbFullName = $"E:\\JjkWikiDb\\{dbName}.db";

        public void InitStore()
        {
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = LocalCreateTableSql.Titles;
            command.ExecuteNonQuery();
            command.CommandText = LocalCreateTableSql.PageInfo;//ToDo rename to PagesInfo
            command.ExecuteNonQuery();
            command.CommandText = LocalCreateTableSql.CleanPages;
            command.ExecuteNonQuery();
        }

        public bool InsertPagesInfo(List<PageInfo> pagesInfo)
        {
            int count = 0;
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO pageInfo (pageId,dayNumber,used)
                VALUES ($pageId,$dayNumber,$used) ";
            var parameterId = command.CreateParameter();
            parameterId.ParameterName = "$pageId";
            command.Parameters.Add(parameterId);
            var parameterDayNumber = command.CreateParameter();
            parameterDayNumber.ParameterName = "$dayNumber";
            command.Parameters.Add(parameterDayNumber);
            var parameterUsed = command.CreateParameter();
            parameterUsed.ParameterName = "$used";
            command.Parameters.Add(parameterUsed);
            foreach (var pageInfoItem in pagesInfo)
            {
                parameterId.Value = pageInfoItem.PageId;
                parameterDayNumber.Value = pageInfoItem.DayNumber;
                parameterUsed.Value = pageInfoItem.Used;
                count += command.ExecuteNonQuery();
            }
            transaction.Commit();
            if (count == pagesInfo.Count)
                return true;
            else
                return false;
        }

        public bool InsertTitles(List<Title> titles)
        {
            int count = 0;
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO titles (pageId,name)
                VALUES ($pageId,$name) ";
            var parameterId = command.CreateParameter();
            parameterId.ParameterName = "$pageId";
            command.Parameters.Add(parameterId);
            var parameterName = command.CreateParameter();
            parameterName.ParameterName = "$name";
            command.Parameters.Add(parameterName);
            foreach (var title in titles)
            {
                parameterId.Value = title.PageId;
                parameterName.Value = title.Name;
                count += command.ExecuteNonQuery();
            }
            transaction.Commit();
            if (count == titles.Count)
                return true;
            else
                return false;
        }

        public List<PageInfo> GetAllPagesInfo()
        {
            List<PageInfo> result = [];
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT pageId, dayNumber, used FROM pageInfo ";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int pageId = reader.GetInt32(0);
                int dayNumber = reader.GetInt32(1);
                int used = reader.GetInt32(2);
                result.Add(new(pageId, dayNumber, used));
            }
            connection.Close();
            return result;
        }

        public bool InsertCleanedPages(List<CleanPage> pages)
        {
            int count = 0;
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO cleanPages (pageId,text)
                VALUES ($pageId,$text) ";
            var parameterId = command.CreateParameter();
            parameterId.ParameterName = "$pageId";
            command.Parameters.Add(parameterId);
            var parameterText = command.CreateParameter();
            parameterText.ParameterName = "$text";
            command.Parameters.Add(parameterText);
            foreach (var page in pages)
            {
                parameterId.Value = page.PageId;
                parameterText.Value = page.Text;
                count += command.ExecuteNonQuery();
            }
            transaction.Commit();
            if (count == pages.Count)
                return true;
            else
                return false;
        }

        public void TruncateCleanedPages()
        {
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM cleanPages ";
            command.ExecuteNonQuery();
        }

        public void Vacuum()
        {
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "VACUUM; ";
            command.ExecuteNonQuery();
        }
    }
}