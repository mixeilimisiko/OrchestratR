using OrchestratR.Core;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;

namespace OrchestratR.Orchestration
{
    public class SagaConfig<TContext> where TContext : SagaContext
    {
        public List<SagaStepDefinition<TContext>> Steps { get; set; } = [];
        public JsonSerializerOptions SerializerOptions { get; set; } = default!;
        public JsonTypeInfo<TContext>? ContextTypeInfo { get; set; }

    }
}
