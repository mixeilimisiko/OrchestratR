using OrchestratR.Core;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;

namespace OrchestratR.Orchestration
{
    public class SagaConfig<TContext> where TContext : SagaContext
    {
        internal List<SagaStepDefinition<TContext>> Steps { get; set; } = [];
        internal JsonSerializerOptions SerializerOptions { get; set; } = default!;
        internal JsonTypeInfo<TContext>? ContextTypeInfo { get; set; }

    }
}
