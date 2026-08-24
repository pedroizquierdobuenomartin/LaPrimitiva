using System;

namespace LaPrimitiva.Domain.Models
{
    public static class RssFeedLimits
    {
        public const int MaxBytes = 512 * 1024;
        public const int MaxItems = 100;
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    }
}
