﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

using YACCS.Commands.Attributes;
using YACCS.Commands.Linq;
using YACCS.Preconditions;
using YACCS.Results;

namespace YACCS.Commands.Models
{
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public abstract class Command : EntityBase, ICommand
	{
		public Type? ContextType { get; protected set; }
		public IList<IReadOnlyList<string>> Names { get; set; }
		public IList<IParameter> Parameters { get; set; }
		public IImmutableCommand? Source { get; protected set; }
		IEnumerable<IReadOnlyList<string>> IQueryableCommand.Names => Names;
		private string DebuggerDisplay => $"Name = {Names[0]}, Parameter Count = {Parameters.Count}";

		protected Command(MethodInfo method, IImmutableCommand? source, Type? contextType)
			: base(method)
		{
			Source = source;
			ContextType = contextType;
			Names = new List<IReadOnlyList<string>>();
			Parameters = method.GetParameters().Select(x => new Parameter(x)).ToList<IParameter>();
		}

		public abstract IImmutableCommand MakeImmutable();

		public virtual IEnumerable<IImmutableCommand> MakeMultipleImmutable()
		{
			var immutable = MakeImmutable();
			yield return immutable;

			foreach (var generator in this.Get<ICommandGeneratorAttribute>())
			{
				foreach (var generated in generator.GenerateCommands(immutable))
				{
					yield return generated;
				}
			}
		}

		[DebuggerDisplay("{DebuggerDisplay,nq}")]
		protected abstract class ImmutableCommand : IImmutableCommand
		{
			private readonly Lazy<Func<Task, object>> _TaskResult;

			public IReadOnlyList<object> Attributes { get; }
			public Type? ContextType { get; }
			public int MaxLength { get; }
			public int MinLength { get; }
			public IReadOnlyList<IReadOnlyList<string>> Names { get; }
			public IReadOnlyList<IImmutableParameter> Parameters { get; }
			public IReadOnlyDictionary<string, IReadOnlyList<IPrecondition>> Preconditions { get; }
			public string PrimaryId { get; }
			public int Priority { get; }
			public IImmutableCommand? Source { get; }
			IEnumerable<object> IQueryableEntity.Attributes => Attributes;
			IEnumerable<IReadOnlyList<string>> IQueryableCommand.Names => Names;
			protected Type ReturnType { get; }
			private string DebuggerDisplay => $"Name = {Names?.FirstOrDefault()?.ToString() ?? "NULL"}, Parameter Count = {Parameters.Count}";

			protected ImmutableCommand(Command mutable, Type returnType)
			{
				ReturnType = returnType;
				ContextType = mutable.ContextType;
				Source = mutable.Source;
				_TaskResult = new Lazy<Func<Task, object>>(
					() => ReflectionUtils.CreateDelegate(TaskResult, "task result"));

				{
					var names = ImmutableArray.CreateBuilder<IReadOnlyList<string>>(mutable.Names.Count);
					foreach (var name in mutable.Names)
					{
						names.Add(new ImmutableName(name));
					}
					Names = names.MoveToImmutable();
				}

				{
					var parameters = ImmutableArray.CreateBuilder<IImmutableParameter>(mutable.Parameters.Count);
					for (var i = 0; i < mutable.Parameters.Count; ++i)
					{
						var immutable = mutable.Parameters[i].ToImmutable(this);
						parameters.Add(immutable);

						// Remainder will always be the last parameter
						if (!immutable.Length.HasValue)
						{
							if (i != mutable.Parameters.Count - 1)
							{
								throw new ArgumentException("Remainder must be the final parameter.");
							}

							MaxLength = int.MaxValue;
							break;
						}
						if (!immutable.HasDefaultValue)
						{
							MinLength += immutable.Length.Value;
						}
						MaxLength += immutable.Length.Value;
					}
					Parameters = parameters.MoveToImmutable();
				}

				{
					var attributes = ImmutableArray.CreateBuilder<object>(mutable.Attributes.Count);
					// Use ConcurrentDictionary because it has GetOrAdd by default, not threading reasons
					var preconditions = new ConcurrentDictionary<string, List<IPrecondition>>();
					var p = 0;
					foreach (var attribute in mutable.Attributes)
					{
						attributes.Add(attribute);
						switch (attribute)
						{
							case IPrecondition precondition:
								if (precondition.Groups.Count == 0)
								{
									preconditions
										.GetOrAdd(string.Empty, _ => new List<IPrecondition>())
										.Add(precondition);
								}
								else
								{
									foreach (var group in precondition.Groups)
									{
										preconditions
											.GetOrAdd(group, _ => new List<IPrecondition>())
											.Add(precondition);
									}
								}
								break;

							case IPriorityAttribute priority:
								Priority = priority.ThrowIfDuplicate(x => x.Priority, ref p);
								break;

							case IIdAttribute id:
								PrimaryId ??= id.Id;
								break;
						}
					}
					Attributes = attributes.MoveToImmutable();
					Preconditions = preconditions.ToImmutableDictionary(
						x => x.Key,
						x => (IReadOnlyList<IPrecondition>)x.Value.ToImmutableArray()
					)!;
				}

				PrimaryId ??= Guid.NewGuid().ToString();
			}

			public abstract Task<IResult> ExecuteAsync(IContext context, object?[] args);

			protected async Task<IResult> ConvertValueAsync(object? value)
			{
				// Void method. No value to return, we're done
				if (ReturnType == typeof(void))
				{
					return SuccessResult.Instance.Sync;
				}

				// We're given a task
				if (value is Task task)
				{
					// Let's await it to actually complete it
					await task.ConfigureAwait(false);

					// Not generic? No value to return, we're done
					if (!ReturnType.IsGenericType)
					{
						return SuccessResult.Instance.Sync;
					}

					// It has a value? Ok, let's get it
					value = _TaskResult.Value.Invoke(task);
				}

				// We're given a result, we can just return that
				if (value is IResult result)
				{
					return result;
				}

				// What do I do with random values?
				return new ValueResult(value);
			}

			private Func<Task, object> TaskResult()
			{
				/*
				 *	(Task Task) =>
				 *	{
				 *		return ((Task<T>)Task).Result;
				 *	}
				 */

				var instance = Expression.Parameter(typeof(Task), "Task");

				var instanceCast = Expression.Convert(instance, ReturnType);
				var property = Expression.Property(instanceCast, nameof(Task<object>.Result));
				var propertyCast = Expression.Convert(property, typeof(object));

				var lambda = Expression.Lambda<Func<Task, object>>(
					propertyCast,
					instance
				);
				return lambda.Compile();
			}
		}
	}
}