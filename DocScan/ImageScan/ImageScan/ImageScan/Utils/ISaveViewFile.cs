using System.IO;


namespace ImageScan.Utils
{
    public interface ISaveViewFile
    {
        string SaveAndViewAsync(string filename, MemoryStream stream);
    }
}
