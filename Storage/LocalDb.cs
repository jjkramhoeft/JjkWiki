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
            command.CommandText = LocalCreateTableSql.PagesInfo;
            command.ExecuteNonQuery();
            command.CommandText = LocalCreateTableSql.CleanPages;
            command.ExecuteNonQuery();
            command.CommandText = LocalCreateTableSql.PageVectors;
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
                INSERT INTO pagesInfo (pageId,dayNumber,used)
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
            command.CommandText = @"SELECT pageId, dayNumber, used FROM pagesInfo ";
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
        
        public bool InsertPageVectors(List<PageVector> pageVectors)
        {
            int count = 0;
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            using var transaction = connection.BeginTransaction();
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO pageVectors (pageId,vector)
                VALUES ($pageId,$vector) ";
            var parameterId = command.CreateParameter();
            parameterId.ParameterName = "$pageId";
            command.Parameters.Add(parameterId);
            var parameterVector = command.CreateParameter();
            parameterVector.ParameterName = "$vector";
            parameterVector.DbType = System.Data.DbType.Binary;
            command.Parameters.Add(parameterVector);
            foreach (var pageVector in pageVectors)
            {
                parameterId.Value = pageVector.PageId;
                parameterVector.Value = pageVector.Blob;
                count += command.ExecuteNonQuery();
            }
            transaction.Commit();
            if (count == pageVectors.Count)
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


        public void TruncatePageVectors()
        {
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM pageVectors ";
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

        public string GetPageText(int pageId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT text FROM cleanPages WHERE pageId = @pageId ";
            command.Parameters.AddWithValue("@pageId", pageId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string text = reader.GetString(0);
                return text;
            }
            return "";
        }

        public List<int> GetAllUsedPageIds()
        {
           List<int> result = [];
            using var connection = new SqliteConnection($"Data Source={_dbFullName}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT pageId FROM pageInfo WHERE used = 1 ";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetInt32(0));
            }
            connection.Close();
            return result;
        }
    }
}