// Copyright (c) VerisFlow. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace VerisFlow.VenusAuto.Core.Internal;

/// <summary>
/// String helpers that use the best API available on each target framework.
/// </summary>
internal static class TextExtensions
{
    /// <summary>
    /// Case-insensitive (ordinal) substring test.
    /// </summary>
    public static bool ContainsIgnoreCase(this string text, string value)
#if NETSTANDARD2_0
        => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
#else
        => text.Contains(value, StringComparison.OrdinalIgnoreCase);
#endif
}