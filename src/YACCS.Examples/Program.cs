﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using YACCS.Commands;
using YACCS.Commands.Attributes;
using YACCS.Commands.Interactivity.Input;
using YACCS.Commands.Linq;
using YACCS.Commands.Models;
using YACCS.Help;
using YACCS.TypeReaders;

namespace YACCS.Examples
{
	public sealed class Program
	{
		private readonly ConsoleCommandService _CommandService;
		private readonly ICommandServiceConfig _Config;
		private readonly ConsoleHandler _Console;
		private readonly HelpFormatter _HelpFormatter;
		private readonly ConsoleInput _Input;
		private readonly TypeNameRegistry _Names;
		private readonly IServiceProvider _Services;
		private readonly TagConverter _Tags;
		private readonly TypeReaderRegistry _TypeReaders;

		private Program()
		{
			_Config = CommandServiceConfig.Default;
			_Names = new TypeNameRegistry();
			_TypeReaders = new TypeReaderRegistry();
			_Tags = new TagConverter();

			_Console = new ConsoleHandler(_Names);
			_HelpFormatter = new HelpFormatter(_Names, _Tags);
			_CommandService = new ConsoleCommandService(_Config, _TypeReaders, _Console);
			_Input = new ConsoleInput(_TypeReaders, _Console);

			_Services = new ServiceCollection()
				.AddSingleton<ICommandService>(_CommandService)
				.AddSingleton<IHelpFormatter>(_HelpFormatter)
				.AddSingleton<IInput<IContext, string>>(_Input)
				.AddSingleton<IReadOnlyDictionary<Type, string>>(_Names)
				.AddSingleton<ITagConverter>(_Tags)
				.AddSingleton<IReadOnlyDictionary<Type, ITypeReader>>(_TypeReaders)
				.AddSingleton(_Console)
				.BuildServiceProvider();
		}

		public static Task Main()
			=> new Program().RunAsync();

		private async Task ExecuteAsync()
		{
			await _Console.WaitForBothIOLocksAsync().ConfigureAwait(false);
			_Console.ReleaseIOLocks();

			_Console.WriteLine();
			_Console.WriteLine("Enter a command and its arguments: ");

			var input = await _Console.ReadLineAsync().ConfigureAwait(false);
			if (input is null)
			{
				return;
			}
			var context = new ConsoleContext(_Services.CreateScope(), input);

			var result = await _CommandService.ExecuteAsync(context, input).ConfigureAwait(false);
			if (!result.InnerResult.IsSuccess)
			{
				_Console.ReleaseIOLocks();
			}
			_Console.WriteResult(result);
		}

		private async Task RegisterCommandsAsync()
		{
			var commands = Assembly.GetExecutingAssembly().GetAllCommandsAsync();
			await _CommandService.AddRangeAsync(commands).ConfigureAwait(false);
			_Console.WriteLine($"Successfully registered {_CommandService.Commands.Count} commands.");

#if true
			void DelegateCommand(int i, double d, string s)
				=> _Console.WriteLine($"i am the delegate command: {i} {d} {s}");

			var @delegate = (Action<int, double, string>)DelegateCommand;
			var names = new[] { new ImmutableName(new[] { "delegate" }) };
			for (var i = 0; i < 1000; ++i)
			{
				var command = new DelegateCommand(@delegate, names)
					.AddAttribute(new PriorityAttribute(i))
					.ToImmutable()
					.Single();
				_CommandService.Commands.Add(command);
			}
#endif
		}

		private async Task RunAsync()
		{
			await RegisterCommandsAsync().ConfigureAwait(false);
			_CommandService.CommandExecuted += (e) =>
			{
				_Console.WriteResult(e.Result);
				return Task.CompletedTask;
			};
			_CommandService.CommandExecutedException += (e) =>
			{
				_Console.WriteLine(string.Join(Environment.NewLine, e.Exceptions), ConsoleColor.Red);
				return Task.CompletedTask;
			};

			while (true)
			{
				await ExecuteAsync().ConfigureAwait(false);
			}
		}
	}
}