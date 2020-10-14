using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace SIBR.Storage.API.Utils
{
    public class LowercaseBooleanConverter: DefaultTypeConverter
    {
        public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value == null) return "";
            return (bool) value ? "true" : "false";
        }
    }
}