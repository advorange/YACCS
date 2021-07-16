using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MorseCode.ITask;

using YACCS.Commands;
using YACCS.Commands.Attributes;
using YACCS.Commands.Linq;
using YACCS.Commands.Models;
using YACCS.Results;
using YACCS.TypeReaders;

namespace YACCS.Tests.Commands.Models
{
	[TestClass]
	public class CommandBuilding_Tests
	{
		[TestMethod]
		public async Task CommandBuildingThrowWhenGenericTypeDefinitions_Test()
		{
			var declaredCommands = typeof(GroupGeneric<object>).CreateMutableCommands();
			Assert.AreEqual(1, declaredCommands.Count);

			var concreteType = 0;
			await foreach (var actualCommand in typeof(GroupGeneric<object>).GetDirectCommandsAsync(EmptyServiceProvider.Instance))
			{
				++concreteType;
			}
			Assert.AreEqual(1, concreteType);

			await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
			{
				await foreach (var actualCommand in typeof(GroupGeneric<>).GetDirectCommandsAsync(EmptyServiceProvider.Instance))
				{
				}
			}).ConfigureAwait(false);
		}

		[TestMethod]
		public void CommandBuildingThrowWhenStructs_Test()
		{
			Assert.ThrowsException<ArgumentException>(() =>
			{
				_ = typeof(GroupStruct).CreateMutableCommands();
			});
		}

		[TestMethod]
		public async Task CommandDelegateBuilding_Test()
		{
			var @delegate = (Func<IContext, bool>)((IContext arg) => true);
			var names = new[] { new[] { "Joe" } };
			var command = new DelegateCommand(@delegate, names);
			var immutable = command.ToImmutable();
			Assert.AreEqual(names.Length, command.Names.Count);
			Assert.AreEqual(names[0], command.Names[0]);
			Assert.AreEqual(1, command.Parameters.Count);
			Assert.AreEqual(1, command.Attributes.Count);
			Assert.IsInstanceOfType(command.Attributes[0], typeof(DelegateCommandAttribute));

			var args = new object[] { new FakeContext() };
			var result = await immutable.ExecuteAsync(null!, args).ConfigureAwait(false);
			Assert.IsTrue(result.IsSuccess);
			Assert.IsInstanceOfType(result, typeof(ValueResult));
			Assert.IsTrue(result.TryGetValue(out bool value));
			Assert.AreEqual(true, value);
		}

		[TestMethod]
		public async Task CommandMethodInfoBuilding_Test()
		{
			var commands = new List<IImmutableCommand>();
			await foreach (var command in typeof(GroupBase).GetAllCommandsAsync(EmptyServiceProvider.Instance))
			{
				commands.Add(command);
			}
			Assert.AreEqual(2, commands.Count);

			var command1 = commands.ById(GroupBase.INHERITANCE_ALLOWED).SingleOrDefault();
			Assert.IsNotNull(command1);

			var command2 = commands.ById(GroupBase.INHERITANCE_DISALLOWED).SingleOrDefault();
			Assert.IsNotNull(command2);
		}

		[TestMethod]
		public async Task CommandMethodInfoBuildingWithInheritanceInvolved_Test()
		{
			var commands = new List<IImmutableCommand>();
			await foreach (var command in typeof(GroupChild).GetAllCommandsAsync(EmptyServiceProvider.Instance))
			{
				commands.Add(command);
			}
			Assert.AreEqual(1, commands.Count);

			var command1 = commands.ById(GroupBase.INHERITANCE_ALLOWED).SingleOrDefault();
			Assert.IsNotNull(command1);

			var command2 = commands.ById(GroupBase.INHERITANCE_DISALLOWED).SingleOrDefault();
			Assert.IsNull(command2);
		}

		[TestMethod]
		public void OverriddenTypeReader_Test()
		{
			static void Delegate([OverrideTypeReader(typeof(FakeTypeReader))] string value)
			{
			}

			var @delegate = (Action<string>)Delegate;
			var names = new[] { new[] { "Joe" } };
			var command = new DelegateCommand(@delegate, names);
			var immutable = command.ToImmutable();
			Assert.AreEqual(1, immutable.Parameters.Count);
			Assert.IsInstanceOfType(immutable.Parameters[0].TypeReader, typeof(FakeTypeReader));
		}

		[TestMethod]
		public void OverriddenTypeReaderInvalidType_Test()
		{
			static void Delegate([OverrideTypeReader(typeof(FakeTypeReader))] int value)
			{
			}

			var @delegate = (Action<int>)Delegate;
			var names = new[] { new[] { "Joe" } };
			var command = new DelegateCommand(@delegate, names);
			Assert.ThrowsException<ArgumentException>(() =>
			{
				_ = command.ToImmutable();
			});
		}

		[TestMethod]
		public async Task StaticCommandDelegateBuilding_Test()
		{
			static bool Method(IContext arg) => true;

			var @delegate = (Func<IContext, bool>)Method;
			var names = new[] { new[] { "Joe" } };
			var command = new DelegateCommand(@delegate, names);
			var immutable = command.ToImmutable();
			Assert.AreEqual(names.Length, command.Names.Count);
			Assert.AreEqual(names[0], command.Names[0]);
			Assert.AreEqual(1, command.Parameters.Count);
			Assert.AreEqual(2, command.Attributes.Count);
			Assert.IsInstanceOfType(command.Attributes[0], typeof(CompilerGeneratedAttribute));
			Assert.IsInstanceOfType(command.Attributes[1], typeof(DelegateCommandAttribute));

			var args = new object[] { new FakeContext() };
			var result = await immutable.ExecuteAsync(null!, args).ConfigureAwait(false);
			Assert.IsTrue(result.IsSuccess);
			Assert.IsInstanceOfType(result, typeof(ValueResult));
			Assert.IsTrue(result.TryGetValue(out bool value));
			Assert.AreEqual(true, value);
		}

		private struct GroupStruct : ICommandGroup
		{
			public Task AfterExecutionAsync(IImmutableCommand command, IContext context, IResult result)
				=> Task.CompletedTask;

			public Task BeforeExecutionAsync(IImmutableCommand command, IContext context)
				=> Task.CompletedTask;

			[Command(nameof(Command))]
			public void Command()
			{
			}

			public Task OnCommandBuildingAsync(IServiceProvider services, IList<ICommand> commands)
				=> Task.CompletedTask;
		}

		private class FakeTypeReader : TypeReader<IContext, string>
		{
			public const string VALUE = "joe";

			public override ITask<ITypeReaderResult<string>> ReadAsync(
				IContext context,
				ReadOnlyMemory<string> input)
				=> TypeReaderResult<string>.FromSuccess(VALUE).AsITask();
		}

		private class GroupBase : CommandGroup<FakeContext>
		{
			public const string INHERITANCE_ALLOWED = "id_1";
			public const string INHERITANCE_DISALLOWED = "id_2";

			[Command(nameof(InheritanceAllowed), AllowInheritance = true)]
			[Id(INHERITANCE_ALLOWED)]
			public IResult InheritanceAllowed() => SuccessResult.Instance;

			[Command(nameof(InheritanceDisallowed), AllowInheritance = false)]
			[Id(INHERITANCE_DISALLOWED)]
			public IResult InheritanceDisallowed() => SuccessResult.Instance;
		}

		private class GroupChild : GroupBase
		{
		}

		private class GroupGeneric<T> : GroupBase
		{
		}
	}
}