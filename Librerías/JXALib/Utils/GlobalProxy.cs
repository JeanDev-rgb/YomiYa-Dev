using System.Net;

namespace YomiYa.Utils;

public static class GlobalProxy
{
    public static IWebProxy? Proxy { get; set; }
}