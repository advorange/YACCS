﻿using System;
using System.Diagnostics;

using YACCS.Commands.Models;
using YACCS.Results;

namespace YACCS.Commands
{
	[DebuggerDisplay(CommandServiceUtils.DEBUGGER_DISPLAY)]
	public class CommandScore : IComparable<CommandScore>, IComparable, ICommandResult
	{
		// This class is a mess
		public static CommandScore CommandNotFound { get; }
			= new(null, null, null!, CommandNotFoundResult.Instance, 0, 0, null);
		public static CommandScore MultiMatch { get; }
			= new(null, null, null!, MultiMatchHandlingErrorResult.Instance, 0, 0, null);
		public static CommandScore QuoteMismatch { get; }
			= new(null, null, null!, QuoteMismatchResult.Instance, 0, 0, null);

		public object?[]? Args { get; }
		public IImmutableCommand? Command { get; }
		public IContext Context { get; }
		public IResult InnerResult { get; }
		public IImmutableParameter? Parameter { get; }
		public int Priority { get; }
		public int Score { get; }
		public CommandStage Stage { get; }
		private string DebuggerDisplay => $"Stage = {Stage}, Score = {Score}, Success = {InnerResult.IsSuccess}";

		/// <summary>
		/// Creates a new <see cref="CommandScore"/>.
		/// </summary>
		/// <param name="command"></param>
		/// <param name="parameter"></param>
		/// <param name="context">
		/// <inheritdoc cref="CommandGroup{TContext}.Context" path="/summary"/>
		/// </param>
		/// <param name="result"></param>
		/// <param name="stage"></param>
		/// <param name="score"></param>
		/// <param name="args"></param>
		protected CommandScore(
			IImmutableCommand? command,
			IImmutableParameter? parameter,
			IContext context,
			IResult result,
			CommandStage stage,
			int score,
			object?[]? args)
		{
			Args = args;
			Command = command;
			Parameter = parameter;
			Context = context;
			Priority = command?.Priority ?? 0;
			InnerResult = result;
			Score = Math.Max(score, 0);
			Stage = stage;
		}

		public static int CompareTo(CommandScore? a, CommandScore? b)
		{
			if (a is null)
			{
				if (b is null)
				{
					return 0;
				}

				return -1;
			}
			if (b is null)
			{
				return 1;
			}

			// If a CanExecute but b cannot, a > b and vice versa
			// The instant a single command can execute, all failed commands are irrelevant
			if (a.Stage != b.Stage)
			{
				if (a.Stage == CommandStage.CanExecute)
				{
					return 1;
				}
				else if (b.Stage == CommandStage.CanExecute)
				{
					return -1;
				}
			}

			static double GetModifier(CommandStage stage)
			{
				return stage switch
				{
					CommandStage.BadContext => 0,
					CommandStage.BadArgCount => 0.1,
					CommandStage.FailedPrecondition => 0.4,
					CommandStage.FailedTypeReader => 0.5,
					CommandStage.FailedParameterPrecondition => 0.6,
					CommandStage.CanExecute => 1,
					_ => throw new ArgumentOutOfRangeException(nameof(stage)),
				};
			}

			var modifierA = GetModifier(a.Stage);
			var modifierB = GetModifier(b.Stage);

			var scoreA = modifierA * (a.Score + a.Priority);
			var scoreB = modifierB * (b.Score + b.Priority);
			return scoreA.CompareTo(scoreB);
		}

		public static CommandScore FromCanExecute(
			IImmutableCommand command,
			IContext context,
			object?[] args,
			int score)
		{
			var result = SuccessResult.Instance;
			const CommandStage STAGE = CommandStage.CanExecute;
			// Subtract start index from int.MaxValue because the more args the less
			// command name parts used, so the less specific the command is
			// i.e. two commands:
			// Echo Colored "text to echo" <-- Score = 2
			// Echo "colored text to echo" <-- Score = 1
			return new(command, null, context, result, STAGE, score, args);
		}

		public static CommandScore FromFailedOptionalArgs(
			IImmutableCommand command,
			IImmutableParameter parameter,
			IContext context,
			int score)
		{
			var result = NotEnoughArgsResult.Instance;
			const CommandStage STAGE = CommandStage.FailedTypeReader;
			return new(command, parameter, context, result, STAGE, score, null);
		}

		public static CommandScore FromFailedParameterPrecondition(
			IImmutableCommand command,
			IImmutableParameter parameter,
			IContext context,
			IResult result,
			int score)
		{
			const CommandStage STAGE = CommandStage.FailedParameterPrecondition;
			return new(command, parameter, context, result, STAGE, score, null);
		}

		public static CommandScore FromFailedPrecondition(
			IImmutableCommand command,
			IContext context,
			IResult result,
			int score)
		{
			const CommandStage STAGE = CommandStage.FailedPrecondition;
			return new(command, null, context, result, STAGE, score, null);
		}

		public static CommandScore FromFailedTypeReader(
			IImmutableCommand command,
			IImmutableParameter parameter,
			IContext context,
			IResult result,
			int score)
		{
			const CommandStage STAGE = CommandStage.FailedTypeReader;
			return new(command, parameter, context, result, STAGE, score, null);
		}

		public static CommandScore FromInvalidContext(
			IImmutableCommand command,
			IContext context,
			int score)
		{
			var result = InvalidContextResult.Instance;
			const CommandStage STAGE = CommandStage.BadContext;
			return new(command, null, context, result, STAGE, score, null);
		}

		public static CommandScore FromNotEnoughArgs(
			IImmutableCommand command,
			IContext context,
			int score)
		{
			var result = NotEnoughArgsResult.Instance;
			const CommandStage STAGE = CommandStage.BadArgCount;
			return new(command, null, context, result, STAGE, score, null);
		}

		public static CommandScore FromTooManyArgs(
			IImmutableCommand command,
			IContext context,
			int score)
		{
			var result = TooManyArgsResult.Instance;
			const CommandStage STAGE = CommandStage.BadArgCount;
			return new(command, null, context, result, STAGE, score, null);
		}

		public static bool operator !=(CommandScore? a, CommandScore? b)
			=> !(a == b);

		public static bool operator <(CommandScore? a, CommandScore? b)
			=> CompareTo(a, b) < 0;

		public static bool operator <=(CommandScore? a, CommandScore? b)
			=> !(a > b);

		public static bool operator ==(CommandScore? a, CommandScore? b)
			=> CompareTo(a, b) == 0;

		public static bool operator >(CommandScore? a, CommandScore? b)
			=> CompareTo(a, b) > 0;

		public static bool operator >=(CommandScore? a, CommandScore? b)
			=> !(a < b);

		/// <inheritdoc />
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
			throw new ArgumentException($"Not a {nameof(CommandScore)}.", nameof(obj));
		}

		/// <inheritdoc />
		public int CompareTo(CommandScore other)
			=> CompareTo(this, other);

		/// <inheritdoc />
		public override bool Equals(object obj)
			=> obj is CommandScore other && CompareTo(other) == 0;

		/// <inheritdoc />
		public override int GetHashCode()
			=> HashCode.Combine(Stage, InnerResult.IsSuccess, Priority, Score);
	}
}