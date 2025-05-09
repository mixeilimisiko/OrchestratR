using Microsoft.Extensions.DependencyInjection;
using OrchestratR.Core;

namespace OrchestratR.Registration
{
    public static class SagaRegistrationExtensions
    {
        public static SagaBuilder<TContext> AddSaga<TContext>(this IServiceCollection services)
            where TContext : SagaContext, new()
        {

            // Return a builder to allow fluent step configuration
            return new SagaBuilder<TContext>(services);
        }
    }
}
