﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using YACCS.Commands;
using YACCS.Commands.Attributes;
using YACCS.Commands.Interactivity.Input;
using YACCS.Commands.Models;
using YACCS.Help;
using YACCS.ParameterPreconditions;
using YACCS.Results;

namespace YACCS.Examples
{
	public class Commands : CommandGroup<ConsoleContext>
	{
		[Command(nameof(Echo))]
		public void Echo([Remainder] string input)
			=> Console.WriteLine(input);

		[Command(nameof(Exit))]
		public void Exit()
			=> Environment.Exit(0);

		[Command(nameof(Result))]
		public IResult Result()
			=> new Result(true, "i give a success result for no reason");

		[Command(nameof(Throws))]
		public void Throws()
			=> throw new InvalidOperationException("i throw for no reason");

		[Command(nameof(Time))]
		public void Time()
			=> Console.WriteLine($"The current time is: {DateTime.UtcNow}");

		[Command(nameof(Help))]
		public class Help : CommandGroup<ConsoleContext>
		{
			public ICommandService CommandService { get; set; }
			public IHelpFormatter HelpFormatter { get; set; }
			public IInput<IContext, string> Input { get; set; }

			[Command]
			public void HelpCommand()
			{
				var i = 0;
				foreach (var command in CommandService.Commands)
				{
					Console.WriteLine($"\t{++i}. {command.Names[0]}");
				}
			}

			[Command]
			public async Task<IResult> HelpCommand(IReadOnlyList<IImmutableCommand> commands)
			{
				var command = commands[0];
				if (commands.Count > 1)
				{
					Console.WriteLine("Enter the position of the command you want to see: ");
					var i = 0;
					foreach (var c in commands)
					{
						Console.WriteLine($"\t{++i}. {c.Names[0]}");
					}

					var options = new InputOptions<IContext, string, int>
					{
						Preconditions = new[]
						{
							new RangeParameterPrecondition(1, commands.Count)
						},
					};
					var result = await Input.GetAsync(Context, options).ConfigureAwait(false);
					if (!result.InnerResult.IsSuccess)
					{
						return result.InnerResult;
					}

					command = commands[result.Value - 1];
				}

				var text = await HelpFormatter.FormatAsync(Context, command).ConfigureAwait(false);
				Console.WriteLine(text);
				return SuccessResult.Instance.Sync;
			}
		}
	}
}