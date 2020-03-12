using System.Collections.Generic;
using System.Linq;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;

namespace AptTool.Security
{
    public class SecurityDb
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public SecurityDb(string database)
        {
            _dbConnectionFactory = new OrmLiteConnectionFactory(
                database,  
                SqliteOrmLiteDialectProvider.Instance);
        }

        public List<PackageNotes> GetPackageNotesForSuite(string package, string suite)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<PackageNotes>();
                if (!string.IsNullOrEmpty(suite))
                {
                    query.Where(x => x.Release == suite);
                }
                if (!string.IsNullOrEmpty(package))
                {
                    query.Where(x => x.Package == package);
                }
                
                return connection.Select(query);
            }
        }
        
        public List<PackageNotes> GetPackageNotesForPackage(string package)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<PackageNotes>();
                query.Where(x => x.Package == package);
                return connection.Select(query);
            }
        }
        
        public PackageNotes GetPackageNoteForBugInAllSuites(string package, string bugName)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<PackageNotes>();
                query.Where(x => x.Package == package);
                query.Where(x => x.BugName == bugName);
                query.Where(x => x.Release == null || x.Release == "");
                return connection.Single(query);
            }
        }
        
        public PackageNotes GetPackageNoteForBugInSuite(string package, string bugName, string suite)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<PackageNotes>();
                query.Where(x => x.Package == package);
                query.Where(x => x.BugName == bugName);
                query.Where(x => x.Release == suite);
                return connection.Single(query);
            }
        }

        public List<BugNotes> GetBugNotes(string bugName)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<BugNotes>();
                query.Where(x => x.BugName == bugName);
                return connection.Select(query);
            }
        }

        public Bug GetBug(string bugName)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<Bug>();
                query.Where(x => x.Name == bugName);
                return connection.Single(query);
            }
        }

        public NvdData GetNvdData(string cveName)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<NvdData>();
                query.Where(x => x.CveName == cveName);
                return connection.Single(query);
            }
        }
        
        public List<string> GetReferences(string source)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<BugsXref>();
                query.Where(x => x.Source == source);
                return connection.Select(query).Select(x => x.Target).ToList();
            }
        }

        public PackageNotesNoDsa GetNoDsaInfoForPackage(string package, string bugName, string suite)
        {
            using (var connection = _dbConnectionFactory.OpenDbConnection())
            {
                var query = connection.From<PackageNotesNoDsa>();
                query.Where(x => x.Package == package);
                query.Where(x => x.BugName == bugName);
                query.Where(x => x.Release == suite);
                return connection.Single(query);
            }
        }
    }
}