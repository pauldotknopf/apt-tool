using System;
using System.IO;

namespace AptTool
{
    public class TmpDirSession : IDisposable
    {
        private readonly string _directory;
        
        public TmpDirSession(string baseDirectory)
        {
            _directory = Path.Combine(baseDirectory, Guid.NewGuid().ToString().Replace("-", ""));
            Directory.CreateDirectory(_directory);
        }

        public string Location => _directory;
        
        public void Dispose()
        {
            Directory.Delete(_directory, true);
        }
    }
}