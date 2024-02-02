﻿using System.Collections.Frozen;

using YACCS.Help.Models;
using YACCS.Preconditions;

namespace YACCS.Help;

internal static class HelpUtils
{
	internal static string Format(this IFormatProvider? formatProvider, FormattableString @string)
		=> formatProvider is null ? @string.ToString() : @string.ToString(formatProvider);

	internal static FrozenDictionary<string, ILookup<Op, IHelpItem<T>>> ToImmutablePreconditions<T>(
		this IReadOnlyDictionary<string, IReadOnlyList<T>> preconditions)
		where T : IGroupablePrecondition
	{
		return preconditions.ToFrozenDictionary(
			x => x.Key,
			x => x.Value
				.Select(x => (IHelpItem<T>)new HelpItem<T>(x))
				.ToLookup(x => x.Item.Op)
		);
	}
}