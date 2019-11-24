using System.IO;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace AptTool.Services.Impl
{
    public class DistroFileParser : IDistroFileParser
    {
        public DistroFile Parse(string file)
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<DistroFile>(File.ReadAllText(file));
            return result;
        }
    }
}