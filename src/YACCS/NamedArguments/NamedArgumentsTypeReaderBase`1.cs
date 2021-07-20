﻿using System;
using System.Collections.Generic;

using MorseCode.ITask;

using YACCS.Commands;
using YACCS.Commands.Models;
using YACCS.Results;
using YACCS.TypeReaders;

namespace YACCS.NamedArguments
{
	public abstract class NamedArgumentsTypeReaderBase<T> : TypeReader<IContext, T>
		where T : new()
	{
		private static readonly char[] _TrimEndChars = new[] { ':' };
		private static readonly char[] _TrimStartChars = new[] { '/', '-' };
		protected abstract IReadOnlyDictionary<string, IImmutableParameter> Parameters { get; }

		public override ITask<ITypeReaderResult<T>> ReadAsync(
			IContext context,
			ReadOnlyMemory<string> input)
		{
			if (input.Length % 2 != 0)
			{
				return CachedResults<T>.NamedArgBadCountTask;
			}

			var result = TryCreateDict(input.Span, out var dict);
			if (!result.IsSuccess)
			{
				return TypeReaderResult<T>.FromError(result).AsITask();
			}

			return ReadDictIntoInstanceAsync(context, dict!);
		}

		protected abstract void Setter(T instance, string property, object? value);

		protected virtual IResult TryCreateDict(
			ReadOnlySpan<string> input,
			out IDictionary<string, string> dict)
		{
			dict = new Dictionary<string, string>();
			for (var i = 0; i < input.Length; i += 2)
			{
				var name = input[i].TrimStart(_TrimStartChars).TrimEnd(_TrimEndChars);
				if (!Parameters.TryGetValue(name, out var parameter))
				{
					return new NamedArgNonExistentResult(name);
				}

				var key = parameter.ParameterName;
				if (dict.ContainsKey(key))
				{
					return new NamedArgDuplicateResult(key);
				}
				dict.Add(key, input[i + 1]);
			}
			return SuccessResult.Instance;
		}

		private async ITask<ITypeReaderResult<T>> ReadDictIntoInstanceAsync(
			IContext context,
			IDictionary<string, string> dict)
		{
			var registry = context.Services.GetRequiredService<IReadOnlyDictionary<Type, ITypeReader>>();

			var instance = new T();
			foreach (var (property, input) in dict)
			{
				var parameter = Parameters[property];

				var reader = registry.GetTypeReader(parameter);
				var result = await reader.ReadAsync(context, input).ConfigureAwait(false);
				if (!result.InnerResult.IsSuccess)
				{
					return TypeReaderResult<T>.FromError(result.InnerResult);
				}

				Setter(instance, parameter.OriginalParameterName, result.Value);
			}
			return TypeReaderResult<T>.FromSuccess(instance);
		}
	}
}