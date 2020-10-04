using System;
using System.ComponentModel;
using System.Globalization;
using SIBR.Storage.Data.Query;

namespace SIBR.Storage.API.Utils
{
    public class PageTokenTypeConverter: TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (PageToken.TryParse((string) value, out var pt))
                return pt;
            return base.ConvertFrom(context, culture, value);
        }
    }
}