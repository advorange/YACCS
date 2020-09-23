﻿using System.Threading.Tasks;

using YACCS.Commands;
using YACCS.Commands.Models;
using YACCS.Results;

namespace YACCS.Preconditions
{
	public abstract class Precondition<TContext> : IPrecondition<TContext> where TContext : IContext
	{
		public abstract Task<IResult> CheckAsync(TContext context, IImmutableCommand command);

		public Task<IResult> CheckAsync(IContext context, IImmutableCommand command)
		{
			if (!(context is TContext castedContext))
			{
				return InvalidContextResult.InstanceTask;
			}
			return CheckAsync(castedContext, command);
		}
	}
}