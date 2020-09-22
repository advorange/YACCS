﻿using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using YACCS.Commands;
using YACCS.Results;

namespace YACCS.ParameterPreconditions
{
	public interface IParameterPrecondition<in TContext, in TValue>
		: IParameterPrecondition
		where TContext : IContext
	{
		Task<IResult> CheckAsync(TContext context, [MaybeNull] TValue value);
	}
}