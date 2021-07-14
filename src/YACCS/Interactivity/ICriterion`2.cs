﻿using System.Threading.Tasks;

using YACCS.Commands;
using YACCS.Results;

namespace YACCS.Interactivity
{
	public interface ICriterion<in TContext, in TInput> where TContext : IContext
	{
		ValueTask<IResult> JudgeAsync(TContext context, TInput input);
	}
}