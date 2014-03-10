using System.Globalization;

namespace RedisAspNetProviders
{
    static class Utils
    {
        public static int ParseInt(string rawValue, NumberStyles styles, int defaultValue)
        {
            int parsedValue = string.IsNullOrWhiteSpace(rawValue) ?
                defaultValue :
                int.Parse(rawValue, styles, CultureInfo.InvariantCulture);

            return parsedValue;
        }
    }
}