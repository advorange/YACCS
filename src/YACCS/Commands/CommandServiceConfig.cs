﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace YACCS.Commands
{
	public class CommandServiceConfig : ICommandServiceConfig
	{
		public static ICommandServiceConfig Default { get; }
			= new ImmutableCommandServiceConfig(new CommandServiceConfig());

		public HashSet<char> EndQuotes { get; set; } = new() { CommandServiceUtils.InternallyUsedQuote };
		public bool IgnoreExtraArgs { get; set; }
		public bool IsCaseSensitive { get; set; }
		public MultiMatchHandling MultiMatchHandling { get; set; } = MultiMatchHandling.Best;
		public char Separator { get; set; } = CommandServiceUtils.InternallyUsedSeparator;
		public HashSet<char> StartQuotes { get; set; } = new() { CommandServiceUtils.InternallyUsedQuote };
		IEqualityComparer<string> ICommandServiceConfig.CommandNameComparer
			=> IsCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
		IImmutableSet<char> ICommandServiceConfig.EndQuotes => EndQuotes.ToImmutableHashSet();
		IImmutableSet<char> ICommandServiceConfig.StartQuotes => StartQuotes.ToImmutableHashSet();

		private sealed class ImmutableCommandServiceConfig : ICommandServiceConfig
		{
			public IEqualityComparer<string> CommandNameComparer { get; }
			public IImmutableSet<char> EndQuotes { get; }
			public bool IgnoreExtraArgs { get; }
			public MultiMatchHandling MultiMatchHandling { get; }
			public char Separator { get; }
			public IImmutableSet<char> StartQuotes { get; }

			public ImmutableCommandServiceConfig(ICommandServiceConfig other)
			{
				CommandNameComparer = other.CommandNameComparer;
				EndQuotes = other.EndQuotes;
				IgnoreExtraArgs = other.IgnoreExtraArgs;
				MultiMatchHandling = other.MultiMatchHandling;
				Separator = other.Separator;
				StartQuotes = other.StartQuotes;
			}
		}
	}
}