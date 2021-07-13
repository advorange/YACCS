﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using YACCS.Commands;
using YACCS.Commands.Models;
using YACCS.Parsing;
using YACCS.TypeReaders;

namespace YACCS.Examples
{
	public class ConsoleCommandService : CommandService
	{
		private readonly IEnumerable<Assembly> _CommandAssemblies;
		private readonly ConsoleHandler _Console;
		private readonly IServiceProvider _Services;
		private bool _IsInitialized;

		public ConsoleCommandService(
			IServiceProvider services,
			ICommandServiceConfig config,
			IArgumentSplitter splitter,
			IReadOnlyDictionary<Type, ITypeReader> readers,
			ConsoleHandler console,
			IEnumerable<Assembly> commandAssemblies)
			: base(config, splitter, readers)
		{
			_CommandAssemblies = commandAssemblies;
			_Console = console;
			_Services = services;
		}

		public Task InitializeAsync()
		{
			if (_IsInitialized)
			{
				return Task.CompletedTask;
			}
			return PrivateInitialize();
		}

		protected override Task DisposeCommandAsync(CommandExecutedEventArgs e)
		{
			_Console.ReleaseIOLocks();
			return base.DisposeCommandAsync(e);
		}

		private async Task AddDelegateCommandsAsync()
		{
			// Example delegate command registration
			static int Add(int a, int b)
				=> a + b;

			var @delegate = (Func<int, int, int>)Add;
			var names = new[] { new ImmutableName(new[] { nameof(Add) }) };
			var add = new DelegateCommand(@delegate, names).ToMultipleImmutableAsync(_Services);
			await this.AddRangeAsync(add).ConfigureAwait(false);
		}

		private async Task PrivateInitialize()
		{
			foreach (var assembly in _CommandAssemblies)
			{
				var commands = assembly.GetAllCommandsAsync(_Services);
				await this.AddRangeAsync(commands).ConfigureAwait(false);
			}
			await AddDelegateCommandsAsync().ConfigureAwait(false);
			Debug.WriteLine($"Registered {Commands.Count} commands for '{CultureInfo.CurrentUICulture}'.");

			CommandExecuted += (e) =>
			{
				_Console.WriteResult(e.Result);
				var exceptions = e.GetAllExceptions();
				if (exceptions.Any())
				{
					_Console.WriteLine(string.Join(Environment.NewLine, exceptions), ConsoleColor.Red);
				}
				return Task.CompletedTask;
			};

			_IsInitialized = true;
		}
	}
}