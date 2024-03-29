﻿using System;
using System.ComponentModel;
using System.Globalization;
using Octopus.TinyTypes;

namespace Calamari.Common.Plumbing.Deployment.PackageRetention
{
    public class TinyTypeTypeConverter<T> : TypeConverter where T : CaseInsensitiveStringTinyType
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (!CanConvertFrom(context, value.GetType()))
            {
                throw new Exception($"Cannot convert {value.GetType().Name} to {typeof(T).Name}.");
            }

            return CaseInsensitiveStringTinyType.Create<T>((string)value);
        }
    }
}