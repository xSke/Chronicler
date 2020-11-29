using System;
using System.Globalization;
using System.Text.Json;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;

namespace SIBR.Storage.CLI.Utils
{
    public static class Extensions
    {
        public static T Value<T>(this JsonElement element, string key,
            T fallback = default) =>
            element.TryGetProperty(key, out var value) ? JsonSerializer.Deserialize<T>(value.GetRawText()) : fallback;

        public static string NullIfEmpty(this string value) => string.IsNullOrEmpty(value) ? null : value;

        public static string ParseHexEmoji(this string emoji) => char.ConvertFromUtf32(Convert.ToInt32(emoji, 16));

        public static IServiceCollection AddMessagePackSettings(this IServiceCollection services) =>
            services.AddSingleton(MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(CompositeResolver.Create(
                    NativeGuidResolver.Instance,
                    NativeDateTimeResolver.Instance,
                    StandardResolver.Instance
                )));
    }
}