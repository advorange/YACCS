﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MorseCode.ITask;

using YACCS.Commands;
using YACCS.Commands.Attributes;
using YACCS.Commands.Linq;
using YACCS.Commands.Models;
using YACCS.Preconditions;
using YACCS.Results;
using YACCS.TypeReaders;

namespace YACCS.Tests.Commands
{
	[TestClass]
	public class CommandService_Commands_Tests
	{
		[TestMethod]
		public void AddAndRemove_Test()
		{
			var commandService = Utils.CreateServices().Get<CommandService>();
			var trie = commandService.Commands;

			var c1 = FakeDelegateCommand.New()
				.AddName(new[] { "1" })
				.ToImmutable()
				.Single();
			trie.Add(c1);
			Assert.AreEqual(1, trie.Count);

			var c2 = FakeDelegateCommand.New()
				.AddName(new[] { "2" })
				.ToImmutable()
				.Single();
			trie.Add(c2);
			Assert.AreEqual(2, commandService.Commands.Count);

			trie.Remove(c1);
			Assert.AreEqual(1, commandService.Commands.Count);

			trie.Remove(c2);
			Assert.AreEqual(0, commandService.Commands.Count);
		}

		[TestMethod]
		public void AddWithParameters_Test()
		{
			var commandService = Utils.CreateServices().Get<CommandService>();
			var trie = commandService.Commands;

			static void Method(Fake x) { }
			var c1 = new DelegateCommand((Action<Fake>)Method, new[] { new[] { "1" } });
			Assert.ThrowsException<ArgumentException>(() =>
			{
				trie.Add(c1.ToImmutable().Single());
			});

			c1.Parameters[0].TypeReader = new TryParseTypeReader<Fake>((string input, out Fake output) =>
			{
				output = null!;
				return false;
			});
			trie.Add(c1.ToImmutable().Single());
			Assert.AreEqual(1, commandService.Commands.Count);
		}

		[TestMethod]
		public void Find_Test()
		{
			var commandService = Utils.CreateServices().Get<ICommandService>();
			var trie = (ITrie<IImmutableCommand>)commandService.Commands;

			var c1 = FakeDelegateCommand.New()
				.AddName(new[] { "1" })
				.ToImmutable()
				.Single();
			trie.Add(c1);
			var c2 = FakeDelegateCommand.New()
				.AddName(new[] { "2" })
				.AddName(new[] { "3" })
				.ToImmutable()
				.Single();
			trie.Add(c2);
			var c3 = FakeDelegateCommand.New()
				.AddName(new[] { "4", "1" })
				.AddName(new[] { "4", "2" })
				.AddName(new[] { "4", "3" })
				.ToImmutable()
				.Single();
			trie.Add(c3);
			var c4 = FakeDelegateCommand.New()
				.AddName(new[] { "4", "1" })
				.ToImmutable()
				.Single();
			trie.Add(c4);

			{
				var found = commandService.Find("");
				Assert.AreEqual(0, found.Count);
			}

			{
				var found = commandService.Find("\"1");
				Assert.AreEqual(0, found.Count);
			}

			{
				var found = commandService.Find("1");
				Assert.AreEqual(1, found.Count);
				Assert.AreSame(c1, found.Single());
			}

			{
				var found = commandService.Find("2");
				Assert.AreEqual(1, found.Count);
				Assert.AreSame(c2, found.Single());
			}

			{
				var found = commandService.Find("3");
				Assert.AreEqual(1, found.Count);
				Assert.AreSame(c2, found.Single());
			}

			{
				var found = commandService.Find("4");
				Assert.AreEqual(2, found.Count);
				Assert.IsTrue(found.Contains(c3));
				Assert.IsTrue(found.Contains(c4));
			}

			{
				var found = commandService.Find("4 1");
				Assert.AreEqual(2, found.Count);
				Assert.IsTrue(found.Contains(c3));
				Assert.IsTrue(found.Contains(c4));
			}

			{
				var found = commandService.Find("4 2");
				Assert.AreEqual(1, found.Count);
				Assert.AreSame(c3, found.Single());
			}

			{
				var found = commandService.Find("asdf not a command");
				Assert.AreEqual(0, found.Count);
			}
		}

		private class Fake
		{
		}
	}

	[TestClass]
	public class CommandService_ExecuteAsync_Tests
	{
		[TestMethod]
		public async Task BestMatchIsDisabled_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			const string input = $"{CommandsGroup._Name} {CommandsGroup._Disabled}";
			var result = await commandService.ExecuteAsync(context, input).ConfigureAwait(false);
			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.AreEqual(DisabledPrecondition._DisabledMessage, result.InnerResult.Response);
		}

		[TestMethod]
		public async Task EmptyInput_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var result = await commandService.ExecuteAsync(context, "").ConfigureAwait(false);
			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.InnerResult, typeof(CommandNotFoundResult));
		}

		[TestMethod]
		public async Task EnsureDisposesContext_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var tcs = new TaskCompletionSource();
			context = new DisposableContext(tcs);

			var shouldNotBeCompleted = new TaskCompletionSource<CommandExecutedEventArgs>();
			Task Logger(CommandExecutedEventArgs e)
			{
				shouldNotBeCompleted.SetResult(e);
				return Task.CompletedTask;
			}
			commandService.CommandExecuted += Logger;
			commandService.CommandExecuted -= Logger;

			const string input = $"{CommandsGroup._Name} {CommandsGroup._Throws}";
			var syncResult = await commandService.ExecuteAsync(context, input).ConfigureAwait(false);
			Assert.IsTrue(syncResult.InnerResult.IsSuccess);

			await tcs.Task.ConfigureAwait(false);
			Assert.IsFalse(shouldNotBeCompleted.Task.IsCompleted);
		}

		[TestMethod]
		public async Task ExecutionDelay_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var tcs = new TaskCompletionSource<CommandExecutedEventArgs>();
			commandService.CommandExecuted += (e) =>
			{
				tcs.SetResult(e);
				return Task.CompletedTask;
			};

			const string input = $"{CommandsGroup._Name} {CommandsGroup._Delay}";
			var sw = new Stopwatch();
			sw.Start();
			var result = await commandService.ExecuteAsync(context, input).ConfigureAwait(false);
			sw.Stop();
			Assert.IsTrue(result.InnerResult.IsSuccess);
			if (sw.ElapsedMilliseconds >= CommandsGroup.DELAY - 50)
			{
				Assert.Fail("ExecuteAsync did not run in the background.");
			}

			var eArgs = await tcs.Task.ConfigureAwait(false);
			var eResult = eArgs.Result;
			Assert.IsFalse(eResult.IsSuccess);
			Assert.AreEqual(CommandsGroup._DelayedMessage, eResult.Response);
		}

		[TestMethod]
		public async Task ExecutionExceptionAfter_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var tcs = new TaskCompletionSource<CommandExecutedEventArgs>();
			commandService.CommandExecuted += (e) =>
			{
				tcs.SetResult(e);
				return Task.CompletedTask;
			};

			const string input = $"{CommandsGroup._Name} {CommandsGroup._ThrowsAfter}";
			var syncResult = await commandService.ExecuteAsync(context, input).ConfigureAwait(false);
			Assert.IsTrue(syncResult.InnerResult.IsSuccess);

			var asyncResult = await tcs.Task.ConfigureAwait(false);
			Assert.IsTrue(asyncResult.Result.IsSuccess);
			Assert.IsInstanceOfType(asyncResult.Result, typeof(SuccessResult));
			Assert.IsNull(asyncResult.DuringException);
			Assert.IsNull(asyncResult.BeforeExceptions);
			Assert.AreEqual(1, asyncResult.AfterExceptions!.Count);
			Assert.IsInstanceOfType(asyncResult.AfterExceptions[0], typeof(InvalidOperationException));
		}

		[TestMethod]
		public async Task ExecutionExceptionBefore_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var tcs = new TaskCompletionSource<CommandExecutedEventArgs>();
			commandService.CommandExecuted += (e) =>
			{
				tcs.SetResult(e);
				return Task.CompletedTask;
			};

			const string input = $"{CommandsGroup._Name} {CommandsGroup._ThrowsBefore}";
			var syncResult = await commandService.ExecuteAsync(context, input).ConfigureAwait(false);
			Assert.IsTrue(syncResult.InnerResult.IsSuccess);

			var asyncResult = await tcs.Task.ConfigureAwait(false);
			Assert.IsTrue(asyncResult.Result.IsSuccess);
			Assert.IsInstanceOfType(asyncResult.Result, typeof(SuccessResult));
			Assert.IsNull(asyncResult.DuringException);
			Assert.IsNull(asyncResult.AfterExceptions);
			Assert.AreEqual(1, asyncResult.BeforeExceptions!.Count);
			Assert.IsInstanceOfType(asyncResult.BeforeExceptions[0], typeof(InvalidOperationException));
		}

		[TestMethod]
		public async Task ExecutionExceptionDuring_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var tcs = new TaskCompletionSource<CommandExecutedEventArgs>();
			commandService.CommandExecuted += (e) =>
			{
				tcs.SetResult(e);
				return Task.CompletedTask;
			};

			const string input = $"{CommandsGroup._Name} {CommandsGroup._Throws}";
			var syncResult = await commandService.ExecuteAsync(context, input).ConfigureAwait(false);
			Assert.IsTrue(syncResult.InnerResult.IsSuccess);

			var asyncResult = await tcs.Task.ConfigureAwait(false);
			Assert.IsFalse(asyncResult.Result.IsSuccess);
			Assert.IsInstanceOfType(asyncResult.Result, typeof(ExceptionDuringCommandResult));
			Assert.IsInstanceOfType(asyncResult.DuringException, typeof(InvalidOperationException));
			Assert.IsNull(asyncResult.BeforeExceptions);
			Assert.IsNull(asyncResult.AfterExceptions);
		}

		[TestMethod]
		public async Task Multimatch_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			const string input = $"{CommandsGroup._Name} 1";
			var result = await commandService.ExecuteAsync(context, input).ConfigureAwait(false);

			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.InnerResult, typeof(MultiMatchHandlingErrorResult));
		}

		[TestMethod]
		public async Task NoCommandsFound_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var result = await commandService.ExecuteAsync(context, "asdf").ConfigureAwait(false);
			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.InnerResult, typeof(CommandNotFoundResult));
		}

		[TestMethod]
		public async Task QuoteMismatch_Test()
		{
			var (commandService, context) = await CreateAsync().ConfigureAwait(false);
			var result = await commandService.ExecuteAsync(context, "\"an end quote is missing").ConfigureAwait(false);
			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.InnerResult, typeof(QuoteMismatchResult));
		}

		private static async Task<(CommandService, FakeContext)> CreateAsync()
		{
			var context = new FakeContext
			{
				Services = Utils.CreateServices(new CommandServiceConfig
				{
					MultiMatchHandling = MultiMatchHandling.Error,
				}),
			};

			var commandService = context.Get<CommandService>();
			var commands = new[]
			{
				 typeof(CommandsGroup),
			}.GetDirectCommandsAsync();
			await commandService.AddRangeAsync(commands).ConfigureAwait(false);

			return (commandService, context);
		}

		public class DisposableContext : FakeContext, IDisposable
		{
			private readonly TaskCompletionSource _Tcs;

			public DisposableContext(TaskCompletionSource tcs)
			{
				_Tcs = tcs;
			}

			public void Dispose()
				=> _Tcs.SetResult();
		}
	}

	[TestClass]
	public class CommandService_GetBestMatchAsync_Tests
	{
		private const int DISALLOWED_VALUE = 1;

		[TestMethod]
		public async Task FailedDefaultValue_Test()
		{
			var (commandService, context, command, parameter) = Create(true, DISALLOWED_VALUE);
			var result = await commandService.ProcessAllPreconditionsAsync(
				new PreconditionCache(context),
				context,
				command.ToImmutable().Single(),
				new[] { DISALLOWED_VALUE.ToString() },
				1
			).ConfigureAwait(false);

			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.AreEqual(CommandStage.FailedTypeReader, result.Stage);
			Assert.AreEqual(0, result.Score);

			Assert.IsTrue(command.Get<WasIReachedPrecondition>().Single().IWasReached);
			Assert.IsFalse(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task FailedParameterPrecondition_Test()
		{
			var (commandService, context, command, parameter) = Create(true, DISALLOWED_VALUE);
			var result = await commandService.ProcessAllPreconditionsAsync(
				new PreconditionCache(context),
				context,
				command.ToImmutable().Single(),
				new[] { DISALLOWED_VALUE.ToString() },
				0
			).ConfigureAwait(false);

			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.AreEqual(CommandStage.FailedParameterPrecondition, result.Stage);
			Assert.AreEqual(0, result.Score);

			Assert.IsTrue(command.Get<WasIReachedPrecondition>().Single().IWasReached);
			Assert.IsFalse(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task FailedPrecondition_Test()
		{
			var (commandService, context, command, parameter) = Create(false, DISALLOWED_VALUE);
			var result = await commandService.ProcessAllPreconditionsAsync(
				new PreconditionCache(context),
				context,
				command.ToImmutable().Single(),
				new[] { DISALLOWED_VALUE.ToString() },
				0
			).ConfigureAwait(false);

			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.AreEqual(CommandStage.FailedPrecondition, result.Stage);
			Assert.AreEqual(0, result.Score);

			Assert.IsFalse(command.Get<WasIReachedPrecondition>().Single().IWasReached);
			Assert.IsFalse(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task FailedTypeReader_Test()
		{
			var (commandService, context, command, parameter) = Create(true, DISALLOWED_VALUE);
			var result = await commandService.ProcessAllPreconditionsAsync(
				new PreconditionCache(context),
				context,
				command.ToImmutable().Single(),
				new[] { "joeba" },
				0
			).ConfigureAwait(false);

			Assert.IsFalse(result.InnerResult.IsSuccess);
			Assert.AreEqual(CommandStage.FailedTypeReader, result.Stage);
			Assert.AreEqual(0, result.Score);

			Assert.IsTrue(command.Get<WasIReachedPrecondition>().Single().IWasReached);
			Assert.IsFalse(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task InvalidContext_Test()
		{
			var (commandService, context, command, _) = Create(true, DISALLOWED_VALUE);
			var score = await commandService.GetCommandScoreAsync(
				new PreconditionCache(context),
				new InvalidContext(),
				command.ToImmutable().Single(),
				Array.Empty<string>(),
				0
			).ConfigureAwait(false);
			Assert.IsFalse(score.InnerResult.IsSuccess);
			Assert.AreEqual(CommandStage.BadContext, score.Stage);
		}

		[TestMethod]
		public async Task InvalidLength_Test()
		{
			var (commandService, context, command, _) = Create(true, DISALLOWED_VALUE);

			// Not enough
			{
				var score = await commandService.GetCommandScoreAsync(
					new PreconditionCache(context),
					context,
					command.ToImmutable().Single(),
					Array.Empty<string>(),
					0
				).ConfigureAwait(false);
				Assert.IsFalse(score.InnerResult.IsSuccess);
				Assert.AreEqual(CommandStage.BadArgCount, score.Stage);
				Assert.IsInstanceOfType(score.InnerResult, typeof(NotEnoughArgsResult));
			}

			// Too many
			{
				var score = await commandService.GetCommandScoreAsync(
					new PreconditionCache(context),
					context,
					command.ToImmutable().Single(),
					new[] { "a", "b", "c", "d", "e", "f" },
					0
				).ConfigureAwait(false);
				Assert.IsFalse(score.InnerResult.IsSuccess);
				Assert.AreEqual(CommandStage.BadArgCount, score.Stage);
				Assert.IsInstanceOfType(score.InnerResult, typeof(TooManyArgsResult));
			}
		}

		[TestMethod]
		public async Task Successful_Test()
		{
			var (commandService, context, command, parameter) = Create(true, DISALLOWED_VALUE);
			var result = await commandService.ProcessAllPreconditionsAsync(
				new PreconditionCache(context),
				context,
				command.ToImmutable().Single(),
				new[] { (DISALLOWED_VALUE + 1).ToString() },
				0
			).ConfigureAwait(false);

			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.AreEqual(CommandStage.CanExecute, result.Stage);
			Assert.AreEqual(1, result.Score);

			Assert.IsTrue(command.Get<WasIReachedPrecondition>().Single().IWasReached);
			Assert.IsTrue(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		private static (CommandService, FakeContext, ICommand, IParameter) Create(bool success, int disallowedValue)
		{
			var context = new FakeContext
			{
				Services = Utils.CreateServices(),
			};

			var command = FakeDelegateCommand.New(typeof(FakeContext))
				.AsContext<FakeContext>()
				.AddPrecondition(new FakePrecondition(success))
				.AddPrecondition(new WasIReachedPrecondition());

			var parameter = new Parameter(typeof(int), "", null)
				.AsType<int>()
				.AddParameterPrecondition(new FakeParameterPrecondition(disallowedValue))
				.AddParameterPrecondition(new WasIReachedParameterPrecondition());
			command.Parameters.Add(parameter);

			return (context.Get<CommandService>(), context, command, parameter);
		}

		public class InvalidContext : IContext
		{
			public Guid Id { get; set; }
			public IServiceProvider Services { get; set; } = EmptyServiceProvider.Instance;
		}
	}

	[TestClass]
	public class CommandService_ParameterPreconditions_Tests
	{
		[TestMethod]
		public async Task EnumerableFailure_Test()
		{
			const int DISALLOWED_VALUE = 1;

			var (commandService, context, parameter) = Create(DISALLOWED_VALUE);
			var result = await commandService.ProcessParameterPreconditionsAsync(
				new PreconditionCache(context),
				parameter,
				new[] { DISALLOWED_VALUE, DISALLOWED_VALUE + 1, DISALLOWED_VALUE + 2 }
			).ConfigureAwait(false);
			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task EnumerableSuccess_Test()
		{
			const int DISALLOWED_VALUE = 1;

			var (commandService, context, parameter) = Create(DISALLOWED_VALUE);
			var result = await commandService.ProcessParameterPreconditionsAsync(
				new PreconditionCache(context),
				parameter,
				new[] { DISALLOWED_VALUE + 1, DISALLOWED_VALUE + 2, DISALLOWED_VALUE + 3 }
			).ConfigureAwait(false);
			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task NonEnumerableFailure_Test()
		{
			const int DISALLOWED_VALUE = 1;

			var (commandService, context, parameter) = Create(DISALLOWED_VALUE);
			var result = await commandService.ProcessParameterPreconditionsAsync(
				new PreconditionCache(context),
				parameter,
				DISALLOWED_VALUE
			).ConfigureAwait(false);
			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task NonEnumerableSuccess_Test()
		{
			const int DISALLOWED_VALUE = 1;

			var (commandService, context, parameter) = Create(DISALLOWED_VALUE);
			var result = await commandService.ProcessParameterPreconditionsAsync(
				new PreconditionCache(context),
				parameter,
				DISALLOWED_VALUE + 1
			).ConfigureAwait(false);
			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(parameter.Get<WasIReachedParameterPrecondition>().Single().IWasReached);
		}

		private static (CommandService, FakeContext, IImmutableParameter) Create(int disallowedValue)
		{
			var context = new FakeContext
			{
				Services = Utils.CreateServices(),
			};

			var parameterBuilder = new Parameter(typeof(int), "", null)
				.AsType<int>()
				.AddParameterPrecondition(new FakeParameterPrecondition(disallowedValue))
				.AddParameterPrecondition(new WasIReachedParameterPrecondition());

			var commandBuilder = FakeDelegateCommand.New();
			commandBuilder.Parameters.Add(parameterBuilder);
			var command = commandBuilder.ToImmutable().Single();
			var parameter = parameterBuilder.ToImmutable(command);

			return (context.Get<CommandService>(), context, parameter);
		}
	}

	[TestClass]
	public class CommandService_Preconditions_Tests
	{
		[TestMethod]
		public async Task ProcessPreconditions_GroupedEndsWithSuccessOR_Test()
		{
			var (commandService, context) = Create();
			var command = FakeDelegateCommand.New()
				.AsContext<IContext>()
				.AddPrecondition(new FakePrecondition(true)
				{
					MutableOp = BoolOp.And,
				})
				.AddPrecondition(new FakePrecondition(false)
				{
					MutableOp = BoolOp.And,
				})
				.AddPrecondition(new WasIReachedPrecondition())
				.AddPrecondition(new FakePrecondition(true)
				{
					MutableOp = BoolOp.Or,
				})
				.ToImmutable()
				.Single();
			var result = await commandService.ProcessPreconditionsAsync(
				new PreconditionCache(context),
				command
			).ConfigureAwait(false);
			Assert.IsTrue(result.IsSuccess);
			Assert.IsFalse(command.Get<WasIReachedPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task ProcessPreconditions_GroupedStartsWithSuccessOR_Test()
		{
			var (commandService, context) = Create();
			var command = FakeDelegateCommand.New()
				.AsContext<IContext>()
				.AddPrecondition(new FakePrecondition(true)
				{
					MutableOp = BoolOp.Or,
				})
				.AddPrecondition(new WasIReachedPrecondition())
				.ToImmutable()
				.Single();
			var result = await commandService.ProcessPreconditionsAsync(
				new PreconditionCache(context),
				command
			).ConfigureAwait(false);
			Assert.IsTrue(result.IsSuccess);
			Assert.IsFalse(command.Get<WasIReachedPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task ProcessPreconditionsFailure_Test()
		{
			var (commandService, context, command) = Create(false);
			var result = await commandService.ProcessPreconditionsAsync(
				new PreconditionCache(context),
				command
			).ConfigureAwait(false);
			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(command.Get<WasIReachedPrecondition>().Single().IWasReached);
		}

		[TestMethod]
		public async Task ProcessPreconditionsSuccess_Test()
		{
			var (commandService, context, command) = Create(true);
			var result = await commandService.ProcessPreconditionsAsync(
				new PreconditionCache(context),
				command
			).ConfigureAwait(false);
			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(command.Get<WasIReachedPrecondition>().Single().IWasReached);
		}

		private static (CommandService, FakeContext) Create()
		{
			var context = new FakeContext
			{
				Services = Utils.CreateServices(),
			};
			return (context.Get<CommandService>(), context);
		}

		private static (CommandService, FakeContext, IImmutableCommand) Create(bool success)
		{
			var (commandService, context) = Create();
			var command = FakeDelegateCommand.New()
				.AsContext<IContext>()
				.AddPrecondition(new FakePrecondition(success))
				.AddPrecondition(new WasIReachedPrecondition())
				.ToImmutable();
			return (commandService, context, command.Single());
		}
	}

	[TestClass]
	public class CommandService_TypeReaders_Tests
	{
		[TestMethod]
		public async Task ProcessTypeReaderMultipleButNotAllValues_Test()
		{
			var value = new[] { 1, 2, 3, 4 };
			var (commandService, context, parameter) = Create<int[]>(4);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				value.Select(x => x.ToString()).Append("joeba").Append("trash").ToArray(),
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.Value, value.GetType());

			var cast = (int[])result.Value!;
			for (var i = 0; i < value.Length; ++i)
			{
				Assert.AreEqual(value[i], cast[i]);
			}
		}

		[TestMethod]
		public async Task ProcessTypeReaderMultipleValuesAllValues_Test()
		{
			var value = new[] { 1, 2, 3, 4 };
			var (commandService, context, parameter) = Create<int[]>(4);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				value.Select(x => x.ToString()).ToArray(),
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.Value, value.GetType());

			var cast = (int[])result.Value!;
			for (var i = 0; i < value.Length; ++i)
			{
				Assert.AreEqual(value[i], cast[i]);
			}
		}

		[TestMethod]
		public async Task ProcessTypeReaderMultipleValuesLongerThanArgs_Test()
		{
			var value = new[] { 1, 2, 3, 4 };
			var (commandService, context, parameter) = Create<int[]>(null);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				value.Select(x => x.ToString()).ToArray(),
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.Value, value.GetType());

			var cast = (int[])result.Value!;
			for (var i = 0; i < value.Length; ++i)
			{
				Assert.AreEqual(value[i], cast[i]);
			}
		}

		[TestMethod]
		public async Task ProcessTypeReaderNotRegistered_Test()
		{
			var (commandService, context, parameter) = Create<CommandService_TypeReaders_Tests>(1);
			await Assert.ThrowsExceptionAsync<KeyNotFoundException>(async () =>
			{
				var result = await commandService.ProcessTypeReadersAsync(
					new PreconditionCache(context),
					parameter,
					new[] { "joeba" },
					0
				).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}

		[TestMethod]
		public async Task ProcessTypeReaderOverridden_Test()
		{
			var (commandService, context, parameter) = Create<char>(1, new CoolCharTypeReader());
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				new[] { "joeba" },
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
		}

		[TestMethod]
		public async Task ProcessTypeReadersCharFailure_Test()
		{
			var (commandService, context, parameter) = Create<char>(1);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				new[] { "joeba" },
				0
			).ConfigureAwait(false);
			Assert.IsFalse(result.InnerResult.IsSuccess);
		}

		[TestMethod]
		public async Task ProcessTypeReaderSingleValue_Test()
		{
			const int VALUE = 2;
			var (commandService, context, parameter) = Create<int>(1);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				new[] { VALUE.ToString() },
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.Value, VALUE.GetType());
			Assert.AreEqual(VALUE, result.Value);
		}

		[TestMethod]
		public async Task ProcessTypeReaderSingleValueWhenMultipleExist_Test()
		{
			const int VALUE = 2;
			var (commandService, context, parameter) = Create<int>(1);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				new[] { VALUE.ToString(), "joeba", "trash" },
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.Value, VALUE.GetType());
			Assert.AreEqual(VALUE, result.Value);
		}

		[TestMethod]
		public async Task ProcessTypeReadersOneInvalidValue_Test()
		{
			var value = new[] { "a", "b", "cee", "d" };
			var (commandService, context, parameter) = Create<char[]>(4);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				value,
				0
			).ConfigureAwait(false);
			Assert.IsFalse(result.InnerResult.IsSuccess);
		}

		[TestMethod]
		public async Task ProcessTypeReadersString_Test()
		{
			const string VALUE = "joeba";
			var (commandService, context, parameter) = Create<string>(1);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				new[] { VALUE },
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.Value, VALUE.GetType());
			Assert.AreEqual(VALUE, result.Value);
		}

		[TestMethod]
		public async Task ProcessTypeReaderZeroLength_Test()
		{
			var (commandService, context, parameter) = Create<IContext>(0);
			var result = await commandService.ProcessTypeReadersAsync(
				new PreconditionCache(context),
				parameter,
				new[] { "doesn't matter" },
				0
			).ConfigureAwait(false);
			Assert.IsTrue(result.InnerResult.IsSuccess);
			Assert.IsInstanceOfType(result.Value, typeof(IContext));
		}

		private static (CommandService, FakeContext, IImmutableParameter) Create<T>(
			int? length,
			ITypeReader? reader = null)
		{
			var context = new FakeContext
			{
				Services = Utils.CreateServices(),
			};
			var parameter = new Parameter(typeof(T), "", null)
			{
				Attributes = new List<object>
				{
					new LengthAttribute(length),
				},
				TypeReader = reader,
			}.ToImmutable(null);
			return ((CommandService)context.Get<ICommandService>(), context, parameter);
		}

		private class CoolCharTypeReader : TypeReader<char>
		{
			public override ITask<ITypeReaderResult<char>> ReadAsync(
				IContext context,
				ReadOnlyMemory<string> input)
				=> TypeReaderResult<char>.FromSuccess('z').AsITask();
		}
	}

	[Command(_Name)]
	public class CommandsGroup : CommandGroup<FakeContext>
	{
		public const string _1 = "c1_id";
		public const string _2 = "c2_id";
		public const string _3 = "c3_id";
		public const string _Delay = "delay";
		public const string _DelayedMessage = "delayed message";
		public const string _Disabled = "disabled";
		public const string _Name = "joeba";
		public const string _Throws = "throws";
		public const string _ThrowsAfter = "throwsafter";
		public const string _ThrowsBefore = "throwsbefore";
		public const int DELAY = 250;

		[Command]
		[Id(_1)]
		public static void CommandOne()
		{
		}

		[Command]
		[Id(_2)]
		[YACCS.Commands.Attributes.Priority(1000)]
		public static void CommandTwo()
		{
		}

		[Command(_Delay)]
		public async Task<IResult> Delay()
		{
			await Task.Delay(DELAY).ConfigureAwait(false);
			return Result.FromError(_DelayedMessage);
		}

		[Command(_Disabled)]
		[DisabledPrecondition]
		public void Disabled()
		{
		}

		[Command]
		public void TestCommand()
		{
		}

		[Command]
		public void TestCommand(int input)
		{
		}

		[Command]
		public void TestCommand([Length(2)] IEnumerable<int> input)
		{
		}

		[Command]
		[Id(_3)]
		[YACCS.Commands.Attributes.Priority(1)]
		public void TestCommandWithRemainder([Remainder] int input)
		{
		}

		[Command(_Throws)]
		public void Throws()
			=> throw new InvalidOperationException();

		[Command(_ThrowsAfter)]
		[FakePreconditionWhichThrowsAfter]
		public void ThrowsAfter()
		{
		}

		[Command(_ThrowsBefore)]
		[FakePreconditionWhichThrowsBefore]
		public void ThrowsBefore()
		{
		}
	}

	public class DisabledPrecondition : PreconditionAttribute
	{
		public const string _DisabledMessage = "lol disabled";

		public override Task<IResult> CheckAsync(IImmutableCommand command, IContext context)
			=> Result.FromError(_DisabledMessage).AsTask();
	}

	public class FakeParameterPrecondition : ParameterPrecondition<FakeContext, int>
	{
		public int DisallowedValue { get; }

		public FakeParameterPrecondition(int value)
		{
			DisallowedValue = value;
		}

		public override Task<IResult> CheckAsync(
			IImmutableParameter parameter,
			FakeContext context,
			[MaybeNull] int value)
			=> value == DisallowedValue ? Result.FromError("lol").AsTask() : SuccessResult.Instance.Task;
	}

	public class FakePrecondition : Precondition<FakeContext>
	{
		private readonly bool _Success;

		public override IReadOnlyList<string> Groups => MutableGroups;
		public List<string> MutableGroups { get; set; } = new List<string>();
		public BoolOp MutableOp { get; set; }
		public override BoolOp Op => MutableOp;

		public FakePrecondition(bool success)
		{
			_Success = success;
			MutableOp = base.Op;
		}

		public override Task<IResult> CheckAsync(IImmutableCommand command, FakeContext context)
			=> _Success ? SuccessResult.Instance.Task : Result.FromError("lol").AsTask();
	}

	public class FakePreconditionWhichThrowsAfter : PreconditionAttribute
	{
		public override Task AfterExecutionAsync(IImmutableCommand command, IContext context, Exception? exception)
			=> throw new InvalidOperationException();

		public override Task<IResult> CheckAsync(IImmutableCommand command, IContext context)
			=> SuccessResult.Instance.Task;
	}

	public class FakePreconditionWhichThrowsBefore : PreconditionAttribute
	{
		public override Task BeforeExecutionAsync(IImmutableCommand command, IContext context)
			=> throw new InvalidOperationException();

		public override Task<IResult> CheckAsync(IImmutableCommand command, IContext context)
			=> SuccessResult.Instance.Task;
	}

	public class WasIReachedParameterPrecondition : ParameterPrecondition<FakeContext, int>
	{
		public bool IWasReached { get; private set; }

		public override Task<IResult> CheckAsync(
			IImmutableParameter parameter,
			FakeContext context,
			[MaybeNull] int value)
		{
			IWasReached = true;
			return SuccessResult.Instance.Task;
		}
	}

	public class WasIReachedPrecondition : Precondition<FakeContext>
	{
		public bool IWasReached { get; private set; }

		public override Task<IResult> CheckAsync(IImmutableCommand command, FakeContext context)
		{
			IWasReached = true;
			return SuccessResult.Instance.Task;
		}
	}
}