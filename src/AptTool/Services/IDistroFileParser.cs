namespace AptTool.Services
{
    public interface IDistroFileParser
    {
        DistroFile Parse(string file);
    }
}