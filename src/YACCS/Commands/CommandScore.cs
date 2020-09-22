﻿using System;
using System.Diagnostics;

using YACCS.Commands.Models;
using YACCS.Results;

namespace YACCS.Commands
{
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class CommandScore : IComparable<CommandScore>, IComparable, INestedResult
	{
		public object?[]? Args { get; }
		public ICommand? Command { get; }
		public IContext? Context { get; }
		public IResult Result { get; }
		public int Score { get; }
		public CommandStage Stage { get; }
		private string DebuggerDisplay => $"Stage = {Stage}, Score = {Score}, Success = {Result.IsSuccess}";

		protected CommandScore(
			ICommand? command,
			IResult result,
			CommandStage stage,
			int score,
			IContext? context,
			object?[]? args)
		{
			Command = command;
			Result = result;
			Score = score + Command?.Priority ?? 0;
			Stage = stage;
			Context = context;
			Args = args;
		}

		public static CommandScore FromCanExecute(
			ICommand command,
			IContext context,
			object?[] args)
		{
			var result = SuccessResult.Instance;
			const CommandStage STAGE = CommandStage.CanExecute;
			return new CommandScore(command, result, STAGE, int.MaxValue, context, args);
		}

		public static CommandScore FromCorrectArgCount(ICommand command, int score)
		{
			var result = SuccessResult.Instance;
			const CommandStage STAGE = CommandStage.CorrectArgCount;
			return new CommandScore(command, result, STAGE, score, null, null);
		}

		public static CommandScore FromFailedParameterPrecondition(
			ICommand command,
			IContext context,
			IResult result,
			int score)
		{
			const CommandStage STAGE = CommandStage.FailedParameterPrecondition;
			return new CommandScore(command, result, STAGE, score, context, null);
		}

		public static CommandScore FromFailedPrecondition(
			ICommand command,
			IContext context,
			IResult result,
			int score)
		{
			const CommandStage STAGE = CommandStage.FailedPrecondition;
			return new CommandScore(command, result, STAGE, score, context, null);
		}

		public static CommandScore FromFailedTypeReader(
			ICommand command,
			IContext context,
			IResult result,
			int score)
		{
			const CommandStage STAGE = CommandStage.FailedTypeReader;
			return new CommandScore(command, result, STAGE, score, context, null);
		}

		public static CommandScore FromNotEnoughArgs(ICommand command, int score)
		{
			var result = NotEnoughArgsResult.Instance;
			const CommandStage STAGE = CommandStage.BadArgCount;
			return new CommandScore(command, result, STAGE, score, null, null);
		}

		public static CommandScore FromNotFound()
		{
			var result = CommandNotFoundResult.Instance;
			const CommandStage STAGE = CommandStage.BadArgCount;
			return new CommandScore(null, result, STAGE, int.MinValue, null, null);
		}

		public static CommandScore FromQuoteMismatch()
		{
			var result = QuoteMismatchResult.Instance;
			const CommandStage STAGE = CommandStage.QuoteMismatch;
			return new CommandScore(null, result, STAGE, int.MinValue, null, null);
		}

		public static CommandScore FromTooManyArgs(ICommand command, int score)
		{
			var result = TooManyArgsResult.Instance;
			const CommandStage STAGE = CommandStage.BadArgCount;
			return new CommandScore(command, result, STAGE, score, null, null);
		}

		public int CompareTo(object obj)
		{
			if (obj is null)
			{
				return 1;
			}
			if (obj is CommandScore other)
			{
				return CompareTo(other);
			}
			throw new ArgumentException($"Object is not a {nameof(CommandScore)}.");
		}

		public int CompareTo(CommandScore other)
		{
			var stage = Stage.CompareTo(other.Stage);
			if (stage != 0)
			{
				return stage;
			}

			var success = Result.IsSuccess.CompareTo(other.Result.IsSuccess);
			if (success != 0)
			{
				return success;
			}

			return Score.CompareTo(other.Score);
		}
	}
}