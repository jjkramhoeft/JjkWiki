namespace Storage
{
    public static class LocalCreateTableSql
    {
        public const string Titles = @"
        CREATE TABLE IF NOT EXISTS titles (
            pageId INTEGER NOT NULL PRIMARY KEY,
            name TEXT NOT NULL
        );";

        public const string PagesInfo = @"
        CREATE TABLE IF NOT EXISTS pagesInfo (
            pageId INTEGER NOT NULL PRIMARY KEY,
            dayNumber INTEGER NOT NULL,
            used INTEGER NOT NULL
        );";

        public const string CleanPages = @"
        CREATE TABLE IF NOT EXISTS cleanPages (
            pageId INTEGER NOT NULL PRIMARY KEY,
            text TEXT NOT NULL
        );";

        public const string PageVectors = @"
        CREATE TABLE IF NOT EXISTS pageVectors (
            pageId INTEGER NOT NULL PRIMARY KEY,
            vector BLOB NOT NULL
        );";
    }
}